using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using System.IO.Ports;

namespace KSPConMod
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public sealed class KSPConModAddon : MonoBehaviour
    {

        // communication protocol:
        // what can go wrong?
        // - disconnection in the middle of the flight
        //      - lock messages
        // - lost/spurious bytes
        //
        // Goal: states must be eventually consistent
        //
        // Receiver must ack reception of message
        // SET -> ACK
        // Sender will repeat SET until it is acknowledged by the receiver
        //
        // 
        // Possible issue:
        // S(H->D) SET A=0
        // S(D->H) SET A=1
        // R(D)    SET A=0
        // - unack'd SET A=1, device has priority (Pd>Ph for A), so ignore
        // R(H)    SET A=1 
        // - unack'd SET A=0, host does not have priority (Ph<Pd), abandon, accept and send ACK
        // S(H->D) ACK A=1
        // R(D)    ACK A=1
        // 
        // The device has priority for some states, notably all states that are defined by a latched switch.
        //
        // Disconnection:
        // S(H->D) SET B=1
        // (no ack after 20ms?)
        // S(H->D) SET B=1
        // (reconnection)
        // R(D) SET B=1, SET B=1
        // S(H->D) ACK B=1

        // "Address space"
        // not necessarily contiguous in physical memory
        // 
        // 0: Ctrl0

        const int NREG = 32;

        byte[] mem;
        bool[] pendingACK;
        byte[] priority;
        int semaphore = 5;


        public enum Address : byte
        {
            Ctrl0 = 0, // SAS RCS  AP MODE   LGT    CABIN LGT 
            Ctrl1 = 1, // ??? L/G DN  CTRL STG LOCK ??? ENG THRUST BRAKE ???
            State0 = 2, // L/G UNLK  DOCK  LDG G-F WARN
            CustomActionGroup = 3, // AG 8-16
            Stage = 4,
            Camera = 5,
            Gear = 6,
            Light = 7,
            RCS = 8,
            SAS = 9,
            Brakes = 10,
            Abort = 11,
            Landed = 12,
            Autopilot = 13
        }

        // 64 addresses
        // 0-16: 1 byte
        // 16-32: 2 bytes
        // 32-48: 4 bytes
        // 48-64: 0 bytes ("pulse" commands)
        public int GetRegisterSize(byte reg)
        {
            switch ((Address)reg)
            {
                case Address.Ctrl0: return 1;
                case Address.Ctrl1: return 1;
                case Address.State0: return 1;
                case Address.CustomActionGroup: return 1;
                case Address.Stage: return 1;
                case Address.Camera: return 1;
                default: return 0;
            }
        }

        byte readCtrl0()
        {
            int sas = curActionState[KSPActionGroup.SAS] ? 0 : 1;
            int rcs = curActionState[KSPActionGroup.RCS] ? 0 : 1;
            return (byte)(sas << 0 | rcs << 1);
        }

        public uint ReadRegister(byte reg)
        {
            switch (reg)
            {
                case (byte)Address.Ctrl0: return readCtrl0();
                case (byte)Address.Ctrl1: return 0; // TODO
                case (byte)Address.State0: return 0;    // TODO
                case (byte)Address.CustomActionGroup: return 0; // TODO
                case (byte)Address.Stage: return 0; // TODO
                case (byte)Address.Camera: return 0;    // TODO
                default: return 0;
            }
        }

        /*public uint WriteRegister(byte reg, uint val)
        {
            switch (reg)
            {
                case (byte)Address.Ctrl0: 
                    break;
                case (byte)Address.Ctrl1:
                    break;
                case (byte)Address.State0:
                    break;
                case (byte)Address.CustomActionGroup:
                    break;
                case (byte)Address.Stage:
                    break;
                case (byte)Address.Camera:
                    break;
                default: return 0;
            }
        }*/

        public void ReceiveMessage(byte[] msg)
        {
            byte ty = (byte)(msg[0] >> 6);
            byte reg = (byte)(msg[0] & 0b00111111);
            int regSize = GetRegisterSize(reg);
            int payloadSize = msg.Length - 1;
            if (payloadSize != regSize)
            {
                return;
            }


            switch (ty)
            {
                // SET 
                case 0:

                    break;

                // ACK
                case 1:
                    if (pendingACK[reg])
                    {
                        //if ()
                    }
                    break;

            }
        }


        public enum Verb : byte
        {
            Set,
            Ack,
        }

        public static KSPConModAddon Instance { get; private set; }
        public static KSPConModConfig Config;

        private SerialPort serialPort;

        private bool initialized = false;
        private ActionGroupList curActionState;
        private VesselAutopilot.AutopilotMode curAutopilotMode;
        private bool curLandedState = false;

        public void Start()
        {
            Debug.Log("[KSPConMod] Start()");
            Config = new KSPConModConfig();
            serialPort = new SerialPort(Config.PortName, Config.BaudRate);
            serialPort.Open();
            serialPort.Write("START\n");
        }

        public void Awake()
        {
            Debug.Log("[KSPConMod] Awake()");
        }


        public void OnDestroy()
        {
            Debug.Log("[KSPConMod] OnDestroy()");
            serialPort.Close();
        }

        public void Update()
        {
            UpdateState();
        }

        int packetSize = -1;
        bool escaped = false;
        const int MaxPacketSize = 8;
        byte[] packet = new byte[MaxPacketSize];

        private int ReadPacket(byte[] outPacket)
        {
            while (serialPort.BytesToRead > 0)
            {
                var b = serialPort.ReadByte();
                if (packetSize == -1)
                {
                    if (b == 0x7E) {
                        packetSize = 0;
                    }
                    continue;
                }

                if (packetSize >= MaxPacketSize)
                {
                    // packet too big, reset
                    packetSize = -1;
                    continue;
                }

                if (b == 0x7D)
                {
                    escaped = true;
                }
                else if (b == 0x7E)
                {
                    if (packetSize != 0)
                    {
                        // full packet received
                        packet.CopyTo(outPacket, 0);
                        var size = packetSize;
                        packetSize = 0;
                        return size;
                    }
                }
                else if (escaped)
                {
                    packet[packetSize++] = (byte)(0x20 ^ b);
                }
                else
                {
                    packet[packetSize++] = (byte)b;
                }
            }
            return 0;
        }

        private void WritePacket(byte[] packet)
        {
            Debug.Assert(packet.Length < MaxPacketSize);
            byte[] buf = new byte[packet.Length + 2];
            buf[0] = 0x7E;
            packet.CopyTo(buf, 1);
            buf[packet.Length + 1] = 0x7E;
            serialPort.Write(buf, 0, packet.Length + 2);
        }


        private bool ReadCommand(ref Verb verb, ref Address address, ref uint data)
        {
            byte[] packet = new byte[MaxPacketSize];

            while (true)
            {
                var size = ReadPacket(packet);
                if (size == 0) return false; 
                if (size < 3) continue; // invalid command packet, but there might be more data
                verb = (Verb)packet[0];
                address = (Address)packet[1];
                data = packet[2];
                Debug.Log(string.Format("[KSPConMod] ReadCommand(): verb={0} address={1} data={2}", verb, address, data));
                return true;
            }
        }

        void WriteCommand(Verb verb, Address address, uint payload)
        {
            byte[] packet = new byte[3]
            {
                (byte)verb,
                (byte)address,
                (byte)(payload & 0xFF)
            };
            WritePacket(packet);
        }

        private void UpdateState()
        {
            // fetch new states
            var newActionState = FlightGlobals.ActiveVessel.ActionGroups;
            var newAutopilotMode = FlightGlobals.ActiveVessel.Autopilot.Mode;
            var newLandedState = FlightGlobals.ActiveVessel.Landed;

            if (serialPort.IsOpen)
            {
                // if initialized == false, the addon was just loaded.
                // unconditionally send updates for all states to get the controller in a known state.

                // send action group states changes
                if (!initialized || curActionState[KSPActionGroup.Stage] != newActionState[KSPActionGroup.Stage])
                {
                    WriteCommand(Verb.Set, Address.Stage, newActionState[KSPActionGroup.Stage] ? 1u : 0u);
                }
                if (!initialized || curActionState[KSPActionGroup.Gear] != newActionState[KSPActionGroup.Gear])
                {
                    WriteCommand(Verb.Set, Address.Gear, newActionState[KSPActionGroup.Gear] ? 1u : 0u);
                }
                if (!initialized || curActionState[KSPActionGroup.Light] != newActionState[KSPActionGroup.Light])
                {
                    WriteCommand(Verb.Set, Address.Light, newActionState[KSPActionGroup.Light] ? 1u : 0u);
                }
                if (!initialized || curActionState[KSPActionGroup.RCS] != newActionState[KSPActionGroup.RCS])
                {
                    WriteCommand(Verb.Set, Address.RCS, newActionState[KSPActionGroup.RCS] ? 1u : 0u);
                }
                if (!initialized || curActionState[KSPActionGroup.SAS] != newActionState[KSPActionGroup.SAS])
                {
                    WriteCommand(Verb.Set, Address.SAS, newActionState[KSPActionGroup.SAS] ? 1u : 0u);
                }
                if (!initialized || curActionState[KSPActionGroup.Brakes] != newActionState[KSPActionGroup.Brakes])
                {
                    WriteCommand(Verb.Set, Address.Brakes, newActionState[KSPActionGroup.Brakes] ? 1u : 0u);
                }
                if (!initialized || curActionState[KSPActionGroup.Abort] != newActionState[KSPActionGroup.Abort])
                {
                    WriteCommand(Verb.Set, Address.Abort, newActionState[KSPActionGroup.Abort] ? 1u : 0u);
                }

                // send autopilot changes (SAS mode)
                if (!initialized || curAutopilotMode != newAutopilotMode)
                {
                    WriteCommand(Verb.Set, Address.Autopilot, (uint)newAutopilotMode);
                }

                // landing changes 
                if (!initialized || newLandedState != curLandedState)
                {
                    WriteCommand(Verb.Set, Address.Landed, newLandedState ? 1u : 0u);
                }
            }

            Verb verb = Verb.Set;
            Address address = Address.Abort;
            uint data = 0;
            while (ReadCommand(ref verb, ref address, ref data))
            {
                if (verb == Verb.Set)
                {
                    switch (address)
                    {
                        case Address.Autopilot:
                            FlightGlobals.ActiveVessel.Autopilot.SetMode((VesselAutopilot.AutopilotMode)data);
                            break;
                        case Address.SAS:
                            newActionState.SetGroup(KSPActionGroup.SAS, (data & 1) == 1 ? true : false);
                            break;
                        case Address.RCS:
                            newActionState.SetGroup(KSPActionGroup.RCS, (data & 1) == 1 ? true : false);
                            break;
                    }
                }
            }

            // update states
            if (curActionState == null) { curActionState = new ActionGroupList(FlightGlobals.ActiveVessel); }
            curActionState.CopyFrom(newActionState);
            curAutopilotMode = newAutopilotMode;
            curLandedState = newLandedState;
            initialized = true;
        }

    }
}

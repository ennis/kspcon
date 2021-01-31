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


        public enum Address : byte
        {
            // Outbound (host->device) addresses
            Led0 = 0,                // 0:SAS, 1:RCS, 2..5:???, 6:LGT, 7:CABIN LGT
            Led1 = 1,                // 0:???, 1: L/G DN, 2:CTRL, 3:STG LOCK, 4:???, 5:ENG THRUST, 6:BRAKE, 7:???
            Led2 = 2,                // 0-7:AP MODE
            Led3 = 3,                // 0-1:AP MODE
            CustomActionGroup = 4,   // AG 8-16
            Unused5 = 5,
            Unused6 = 6,
            Unused7 = 7,
            // Inbound (device->host) addresses
            Autopilot = 8,
            Throttle = 9,
            SAS = 10,
            RCS = 11
        }

        public enum Verb : byte
        {
            Set,
            Ack,
            Init,
            InitAck,
        }

        /*// 64 addresses
        // 0-16: 1 byte
        // 16-32: 2 bytes
        // 32-48: 4 bytes
        // 48-64: 0 bytes ("pulse" commands)
        public int GetRegisterSize(byte reg)
        {
            switch ((Address)reg)
            {
                case Address.Led0: return 1;
                case Address.Led1: return 1;
                case Address.State0: return 1;
                case Address.CustomActionGroup: return 1;
                case Address.Stage: return 1;
                case Address.Camera: return 1;
                default: return 0;
            }
        }*/


        public static KSPConModAddon Instance { get; private set; }
        public static KSPConModConfig Config;

        private SerialPort serialPort;
        // device register states
        private bool initialized = false;
        private byte curLed0;
        private byte curLed1;
        private byte curLed2;
        private byte curLed3;

        public void Start()
        {
            Debug.Log("[KSPConMod] Start()");
            Config = new KSPConModConfig();
            serialPort = new SerialPort(Config.PortName, Config.BaudRate);
            serialPort.Open();
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

        int packetSize = -1;
        bool escaped = false;
        const byte PTPEscapeByte = 0x7D;
        const byte PTPFrameFlag = 0x7E;
        const byte PTPMaxPayloadSize = 16;
        const int PTPMaxPacketSize = 2 + PTPMaxPayloadSize;
        const int PTPMaxEscapedPacketSize = PTPMaxPacketSize * 2;
        const int PTPMaxFrameSize = 2 + PTPMaxEscapedPacketSize;

        byte[] packet = new byte[PTPMaxPacketSize];

        private int ReadPacket(byte[] outPacket)
        {
            while (serialPort.BytesToRead > 0)
            {
                var b = serialPort.ReadByte();
                if (packetSize == -1)
                {
                    if (b == PTPFrameFlag)
                    {
                        packetSize = 0;
                    }
                    continue;
                }

                if (packetSize >= PTPMaxPacketSize)
                {
                    // packet too big, reset
                    packetSize = -1;
                    escaped = false;
                    continue;
                }

                if (b == PTPEscapeByte)
                {
                    escaped = true;
                }
                else if (b == PTPFrameFlag)
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
                    escaped = false;
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
            Debug.Assert(packet.Length < PTPMaxPacketSize);

            int wptr = 0;
            byte[] buf = new byte[PTPMaxFrameSize];
            buf[wptr++] = PTPFrameFlag;
            for (int i = 0; i < packet.Length; ++i)
            {
                if (packet[i] == PTPEscapeByte || packet[i] == PTPFrameFlag)
                {
                    buf[wptr++] = PTPEscapeByte;
                    buf[wptr++] = (byte)(packet[i] ^ 0x20);
                }
                else
                {
                    buf[wptr++] = packet[i];
                }
            }
            buf[wptr++] = PTPFrameFlag;
            serialPort.Write(buf, 0, wptr);
        }


        private bool ReadCommand(ref Verb verb, ref Address address, ref uint data)
        {
            byte[] packet = new byte[PTPMaxPacketSize];

            while (true)
            {
                var size = ReadPacket(packet);
                if (size == 0) return false;
                verb = (Verb)packet[0];
                if ((verb == Verb.Set) || (verb == Verb.Ack))
                {
                    if (size < 3) continue; // invalid command packet, but there might be more data
                    address = (Address)packet[1];
                    var payloadSize = size - 2;
                    data = 0;
                    for (int i = 0; i < payloadSize; ++i)
                    {
                        data |= (uint)(packet[2 + i] << 8 * i);
                    }

                    Debug.Log(string.Format("[KSPConMod] ReadCommand(): verb={0} address={1} data={2}", verb, address, data));
                }
                else if (verb == Verb.Init)
                {
                    Debug.Log(string.Format("[KSPConMod] ReadCommand(): INIT"));
                }
                else
                {
                    Debug.Log(string.Format("[KSPConMod] ReadCommand(): Unknown verb {0}", packet[0]));
                }
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

        private void ProcessInput()
        {
            Verb verb = Verb.Set;
            Address address = Address.Led0;
            uint data = 0;
            while (ReadCommand(ref verb, ref address, ref data))
            {
                switch (verb)
                {
                    case Verb.Set:
                        switch (address)
                        {
                            case Address.Autopilot:
                                FlightGlobals.ActiveVessel.Autopilot.SetMode((VesselAutopilot.AutopilotMode)data);
                                break;
                            case Address.SAS:
                                FlightGlobals.ActiveVessel.ActionGroups.SetGroup(KSPActionGroup.SAS, (data & 1) == 1 ? true : false);
                                break;
                            case Address.RCS:
                                FlightGlobals.ActiveVessel.ActionGroups.SetGroup(KSPActionGroup.RCS, (data & 1) == 1 ? true : false);
                                break;
                            case Address.Throttle:
                                FlightCtrlState ctrlState = new FlightCtrlState();
                                FlightGlobals.ActiveVessel.GetControlState(ctrlState);
                                ctrlState.mainThrottle = data / 1024.0f;
                                FlightGlobals.ActiveVessel.SetControlState(ctrlState);
                                break;
                        }
                        break;
                    case Verb.Init:
                        initialized = false;
                        WriteCommand(Verb.InitAck, Address.Led0, 0);
                        break;
                }
            }
        }

        private void Update()
        {
            if (!serialPort.IsOpen)
            {
                return;
            }

            ProcessInput();
            uint SASFlag = FlightGlobals.ActiveVessel.ActionGroups[KSPActionGroup.SAS] ? 1u : 0u;
            uint RCSFlag = FlightGlobals.ActiveVessel.ActionGroups[KSPActionGroup.RCS] ? 1u : 0u;
            uint lightFlag = FlightGlobals.ActiveVessel.ActionGroups[KSPActionGroup.Light] ? 1u : 0u;
            uint brakesFlag = FlightGlobals.ActiveVessel.ActionGroups[KSPActionGroup.Brakes] ? 1u : 0u;

            byte newLed0 = (byte)(SASFlag << 0 | RCSFlag << 1 | lightFlag << 6);
            byte newLed1 = (byte)(brakesFlag << 6);
            int apModeMask = 1 << (int)FlightGlobals.ActiveVessel.Autopilot.Mode;
            byte newLed2 = (byte)(apModeMask & 0xff);
            byte newLed3 = (byte)((apModeMask >> 8) & 0xff);

            if (!initialized || newLed0 != curLed0)
            {
                WriteCommand(Verb.Set, Address.Led0, newLed0);
                curLed0 = newLed0;
            }

            if (!initialized || newLed1 != curLed1)
            {
                WriteCommand(Verb.Set, Address.Led1, newLed1);
                curLed1 = newLed1;
            }

            if (!initialized || newLed2 != curLed2)
            {
                WriteCommand(Verb.Set, Address.Led2, newLed2);
                curLed2 = newLed2;
            }

            if (!initialized || newLed3 != curLed3)
            {
                WriteCommand(Verb.Set, Address.Led3, newLed3);
                curLed3 = newLed3;
            }

            initialized = true;
        }
    }
}

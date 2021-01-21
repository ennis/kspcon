using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KSPConMod
{
    public class KSPConModConfig
    {
        [Persistent]
        public string PortName;
        [Persistent]
        public int BaudRate;

        public static readonly string SettingsFileName = "Settings.cfg";
        public static readonly string SettingsNodeName = "KSPConMod";
        public static readonly string SettingsFilePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), SettingsFileName).Replace("\\", "/");

        public KSPConModConfig()
        {
            if (!LoadSettings())
            {
                CreateDefaultSettings();
            }
           
        }

        private void CreateDefaultSettings()
        {
            PortName = "COM5";
            BaudRate = 115200;
            Save();
        }

        void Save()
        {
            try
            {
                var cfgNode = new ConfigNode(SettingsNodeName);
                cfgNode = ConfigNode.CreateConfigFromObject(this, cfgNode);
                var cfgWrapper = new ConfigNode(SettingsNodeName);
                cfgWrapper.AddNode(cfgNode);
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsFilePath));
                cfgWrapper.Save(SettingsFilePath);
            }
            catch (Exception e)
            {
                Debug.Log(String.Format("[KSPConMod] Settings file `{0}` could not be saved: {1}", SettingsFilePath, e));
            }
        }


        private bool LoadSettings()
        {
            if (File.Exists(SettingsFilePath))
            {
                try
                {
                    var cfgRoot = ConfigNode.Load(SettingsFilePath);
                    var cfgNode = cfgRoot.GetNode(SettingsNodeName);
                    ConfigNode.LoadObjectFromConfig(this, cfgNode);
                    return true;
                }
                catch (Exception e)
                {
                    Debug.Log(String.Format("[KSPConMod] Settings file `{0}` could not be read: {1}", SettingsFilePath, e));
                }
            }

            Debug.Log("[KSPConMod] Settings file not found");
            return false;
        }
    }
}

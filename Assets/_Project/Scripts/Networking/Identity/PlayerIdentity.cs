using System;
using UnityEngine;

namespace MiniBrawl.Networking.Identity
{
    /// <summary>
    /// Who this player is, independent of any connection (§2.7). A connection id changes the moment
    /// Wi-Fi drops; this does not, which is what makes rejoining with your score intact possible.
    /// </summary>
    public static class PlayerIdentity
    {
        const string k_IdKey = "minibrawl.playerId";
        const string k_NameKey = "minibrawl.playerName";

        static string s_Id;
        static string s_NameOverride;
        static bool s_ParsedCommandLine;

        /// <summary>Generated once per install and kept.</summary>
        public static string Id
        {
            get
            {
                if (!string.IsNullOrEmpty(s_Id)) return s_Id;

                ParseCommandLine();
                if (!string.IsNullOrEmpty(s_Id)) return s_Id;

                s_Id = PlayerPrefs.GetString(k_IdKey, "");
                if (string.IsNullOrEmpty(s_Id))
                {
                    s_Id = Guid.NewGuid().ToString("N");
                    PlayerPrefs.SetString(k_IdKey, s_Id);
                    PlayerPrefs.Save();
                }
                return s_Id;
            }
        }

        /// <summary>Display name; defaults to the device's own name until the player picks one.</summary>
        public static string Name
        {
            get
            {
                ParseCommandLine();
                if (!string.IsNullOrWhiteSpace(s_NameOverride)) return s_NameOverride;

                string stored = PlayerPrefs.GetString(k_NameKey, "");
                if (!string.IsNullOrWhiteSpace(stored)) return stored;

                string device = SystemInfo.deviceName;
                return string.IsNullOrWhiteSpace(device) || device == "<unknown>" ? "Player" : device;
            }
            set
            {
                PlayerPrefs.SetString(k_NameKey, value ?? "");
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// -playerid / -playername override the stored identity. Two processes on one machine share
        /// PlayerPrefs, so without this they claim the same seat and look like one reconnecting
        /// player — which is exactly what happened the first time this was tested.
        /// </summary>
        static void ParseCommandLine()
        {
            if (s_ParsedCommandLine) return;
            s_ParsedCommandLine = true;

            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-playerid") s_Id = args[i + 1];
                else if (args[i] == "-playername") s_NameOverride = args[i + 1];
            }
        }
    }
}

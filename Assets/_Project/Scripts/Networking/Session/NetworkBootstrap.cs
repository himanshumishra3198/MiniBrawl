using System;
using FishNet;
using FishNet.Managing;
using FishNet.Transporting.Tugboat;
using MiniBrawl.Config;
using UnityEngine;

namespace MiniBrawl.Networking.Session
{
    /// <summary>
    /// Starts the session. Reads the command line so two headless processes can be driven from a
    /// terminal (-server / -client / -host, -address, -port); in the Editor, or with no arguments,
    /// it falls back to the inspector mode so pressing Play just works.
    /// </summary>
    public sealed class NetworkBootstrap : MonoBehaviour
    {
        public enum StartMode { None, Host, Server, Client }

        [Tooltip("Used when no -server/-client/-host argument is present.")]
        public StartMode DefaultMode = StartMode.Host;

        public string DefaultAddress = "127.0.0.1";

        NetworkManager m_Manager;
        StartMode m_Mode;
        string m_Address;
        ushort m_Port;

        public StartMode Mode => m_Mode;
        public string Address => m_Address;
        public ushort Port => m_Port;

        /// <summary>Set by -autopilot: players drive themselves, for headless testing.</summary>
        public static bool Autopilot { get; private set; }

        void Awake()
        {
            // Parsed before any player spawns, so the motor knows which input source to use.
            ParseCommandLine(out m_Mode, out m_Address, out m_Port);
        }

        void Start()
        {
            m_Manager = InstanceFinder.NetworkManager;
            if (m_Manager == null)
            {
                Debug.LogError("[NetworkBootstrap] No NetworkManager in the scene.");
                return;
            }

            if (m_Manager.TransportManager.Transport is Tugboat tugboat)
            {
                tugboat.SetPort(m_Port);
                tugboat.SetClientAddress(m_Address);
            }

            m_Manager.TimeManager.SetTickRate(NetworkConstants.SimulationTickRate);

            switch (m_Mode)
            {
                case StartMode.Host:
                    m_Manager.ServerManager.StartConnection();
                    m_Manager.ClientManager.StartConnection();
                    break;
                case StartMode.Server:
                    m_Manager.ServerManager.StartConnection();
                    break;
                case StartMode.Client:
                    m_Manager.ClientManager.StartConnection();
                    break;
            }

            Debug.Log($"[NetworkBootstrap] mode={m_Mode} address={m_Address} port={m_Port} " +
                      $"tick={NetworkConstants.SimulationTickRate}Hz autopilot={Autopilot}");
        }

        void ParseCommandLine(out StartMode mode, out string address, out ushort port)
        {
            mode = DefaultMode;
            address = DefaultAddress;
            port = NetworkConstants.GamePort;

            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-host": mode = StartMode.Host; break;
                    case "-server": mode = StartMode.Server; break;
                    case "-client": mode = StartMode.Client; break;
                    case "-autopilot": Autopilot = true; break;
                    case "-address" when i + 1 < args.Length: address = args[++i]; break;
                    case "-port" when i + 1 < args.Length && ushort.TryParse(args[i + 1], out ushort p):
                        port = p;
                        i++;
                        break;
                }
            }
        }
    }
}

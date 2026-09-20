using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MiniBrawl.Platform
{
    /// <summary>
    /// Finds the address other devices should connect to. Enumerates interfaces rather than asking
    /// a DNS server, because the whole point is working with no internet (§4.1).
    /// </summary>
    public static class LocalIpResolver
    {
        public static string Resolve()
        {
            string best = null;
            int bestScore = -1;

            foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus != OperationalStatus.Up) continue;
                if (adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                foreach (UnicastIPAddressInformation info in adapter.GetIPProperties().UnicastAddresses)
                {
                    if (info.Address.AddressFamily != AddressFamily.InterNetwork) continue;

                    string address = info.Address.ToString();
                    if (address.StartsWith("127.") || address.StartsWith("169.254.")) continue;

                    int score = Score(address, adapter.NetworkInterfaceType);
                    if (score <= bestScore) continue;

                    bestScore = score;
                    best = address;
                }
            }

            return best ?? "127.0.0.1";
        }

        static int Score(string address, NetworkInterfaceType type)
        {
            // An Android hotspot hands the host 192.168.43.1, so prefer that range, then other
            // private ranges, and prefer wireless over everything else.
            int score = 0;
            if (address.StartsWith("192.168.")) score += 3;
            else if (address.StartsWith("10.")) score += 2;
            else if (address.StartsWith("172.")) score += 1;

            if (type == NetworkInterfaceType.Wireless80211) score += 4;
            return score;
        }
    }
}

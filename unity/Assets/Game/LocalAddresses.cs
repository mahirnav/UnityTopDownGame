using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Rats.Game
{
    /// <summary>This machine's IPv4 addresses, so the host can tell the other player what to type.</summary>
    static class LocalAddresses
    {
        public static List<string> Describe()
        {
            var result = new List<string>();
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                foreach (var addr in nic.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    var b = addr.Address.GetAddressBytes();
                    bool tailscale = b[0] == 100 && b[1] >= 64 && b[1] <= 127; // 100.64.0.0/10
                    if (tailscale) result.Insert(0, $"{addr.Address}  (Tailscale: use this one)");
                    else result.Add($"{addr.Address}  (local network)");
                }
            }
            return result;
        }
    }
}

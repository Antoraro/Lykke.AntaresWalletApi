using System.Net;

namespace AntaresWalletApi.Common.Configuration
{
    public class MeConfig
    {
        public string Host { get; set; }
        public int Port { get; set; }

        public IPEndPoint GetIpEndPoint()
        {
            if (IPAddress.TryParse(Host, out var ipAddress))
                return new IPEndPoint(ipAddress, Port);

            var addresses = Dns.GetHostAddressesAsync(Host).Result;
            return new IPEndPoint(addresses[0], Port);
        }
    }
}

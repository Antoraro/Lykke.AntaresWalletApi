using Swisschain.Lykke.AntaresWalletApi.ApiClient.Common;
using Swisschain.Lykke.AntaresWalletApi.ApiContract;

namespace Swisschain.Lykke.AntaresWalletApi.ApiClient
{
    public class AntaresWalletApiClient : BaseGrpcClient, IAntaresWalletApiClient
    {
        public AntaresWalletApiClient(string serverGrpcUrl) : base(serverGrpcUrl)
        {
            Monitoring = new Monitoring.MonitoringClient(Channel);
        }

        public Monitoring.MonitoringClient Monitoring { get; }
    }
}

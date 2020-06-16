using Swisschain.Lykke.AntaresWalletApi.ApiContract;

namespace Swisschain.Lykke.AntaresWalletApi.ApiClient
{
    public interface IAntaresWalletApiClient
    {
        Monitoring.MonitoringClient Monitoring { get; }
    }
}

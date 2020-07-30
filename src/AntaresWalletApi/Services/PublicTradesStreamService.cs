using Lykke.Common.Log;
using Swisschain.Lykke.AntaresWalletApi.ApiContract;

namespace AntaresWalletApi.Services
{
    public class PublicTradesStreamService: StreamServiceBase<PublicTrade>
    {
        public PublicTradesStreamService(ILogFactory logFactory, bool needPing = false) : base(logFactory, needPing)
        {
        }
    }
}

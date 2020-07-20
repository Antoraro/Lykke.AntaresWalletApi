using Lykke.Common.Log;
using Swisschain.Lykke.AntaresWalletApi.ApiContract;

namespace AntaresWalletApi.Services
{
    public class CandlesStreamService : StreamServiceBase<CandleUpdate>
    {
        public CandlesStreamService(ILogFactory logFactory, bool needPing = false) : base(logFactory, needPing)
        {
        }
    }
}

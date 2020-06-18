using System.Linq;
using Grpc.Core;

namespace AntaresWalletApi.Extensions
{
    public static class ServerCallContextExtensions
    {
        public static string GetToken(this ServerCallContext context)
        {
            return context.RequestHeaders.FirstOrDefault(x => x.Key == "authorization")?.Value;
        }
    }
}

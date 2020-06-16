using System;
using Grpc.Net.Client;

namespace Swisschain.Lykke.AntaresWalletApi.ApiClient.Common
{
    public class BaseGrpcClient : IDisposable
    {
        protected GrpcChannel Channel { get; }

        public BaseGrpcClient(string serverGrpcUrl)
        {
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

            Channel = GrpcChannel.ForAddress(serverGrpcUrl);
        }

        public void Dispose()
        {
            Channel?.Dispose();
        }
    }
}

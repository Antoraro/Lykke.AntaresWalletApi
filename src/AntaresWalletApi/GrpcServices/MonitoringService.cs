using System;
using System.Threading.Tasks;
using Grpc.Core;
using Swisschain.Sdk.Server.Common;
using Swisschain.Lykke.AntaresWalletApi.ApiContract;

namespace AntaresWalletApi.GrpcServices
{
    public class MonitoringService : Monitoring.MonitoringBase
    {
        public override Task<IsAliveResponce> IsAlive(IsAliveRequest request, ServerCallContext context)
        {
            var result = new IsAliveResponce
            {
                Name = ApplicationInformation.AppName,
                Version = ApplicationInformation.AppVersion,
                StartedAt = ApplicationInformation.StartedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                Env = ApplicationEnvironment.Environment ?? string.Empty,
                Hostname = Environment.MachineName
            };

            return Task.FromResult(result);
        }
    }
}

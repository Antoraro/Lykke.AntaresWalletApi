using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using AntaresWalletApi.Common.Configuration;
using Swisschain.Sdk.Server.Common;

namespace AntaresWalletApi.Worker
{
    public sealed class Startup : SwisschainStartup<AppConfig>
    {
        public Startup(IConfiguration configuration)
            : base(configuration)
        {
        }

        protected override void ConfigureServicesExt(IServiceCollection services)
        {
            base.ConfigureServicesExt(services);

            services.AddHttpClient();
        }
    }
}

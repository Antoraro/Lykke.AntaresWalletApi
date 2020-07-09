using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using AntaresWalletApi.Common.Configuration;
using AntaresWalletApi.Worker.Modules;
using Autofac;
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

        protected override void ConfigureContainerExt(ContainerBuilder builder)
        {
            builder.RegisterModule(new AutofacModule(Config));
            builder.RegisterModule(new AutoMapperModule());
        }
    }
}

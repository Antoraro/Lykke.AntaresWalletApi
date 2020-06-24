using System;
using AntaresWalletApi.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using AntaresWalletApi.Common.Configuration;
using AntaresWalletApi.GrpcServices;
using AntaresWalletApi.Modules;
using Autofac;
using Swisschain.Sdk.Server.Common;

namespace AntaresWalletApi
{
    public sealed class Startup : SwisschainStartup<AppConfig>
    {
        public Startup(IConfiguration configuration)
            : base(configuration)
        {
        }

        protected override void ConfigureServicesExt(IServiceCollection services)
        {
            services.AddHttpClient(HttpClientNames.WalletApiV1, client =>
            {
                client.BaseAddress = new Uri(Config.Services.WalletApiv1Url);
            });
        }

        protected override void ConfigureContainerExt(ContainerBuilder builder)
        {
            builder.RegisterModule(new AutofacModule(Config));
            builder.RegisterModule(new AutoMapperModule());
        }

        protected override void RegisterEndpoints(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGrpcService<MonitoringService>();
            endpoints.MapGrpcService<ApiService>();
        }
    }
}

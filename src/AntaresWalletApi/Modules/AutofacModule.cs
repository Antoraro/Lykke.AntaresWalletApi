using System.Net.Http;
using AntaresWalletApi.Common;
using AntaresWalletApi.Common.Configuration;
using Autofac;
using Lykke.ApiClients.V1;

namespace AntaresWalletApi.Modules
{
    public class AutofacModule : Module
    {
        private readonly AppConfig _config;

        public AutofacModule(AppConfig config)
        {
            _config = config;
        }

        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterInstance(_config.Token);
            builder.Register(ctx =>
            {
                var factory = ctx.Resolve<IHttpClientFactory>();
                return new LykkeWalletAPIv1Client(factory.CreateClient(HttpClientNames.WalletApiV1));
            }).As<ILykkeWalletAPIv1Client>();
        }
    }
}

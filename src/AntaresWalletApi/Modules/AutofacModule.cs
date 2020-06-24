using System;
using System.Net.Http;
using AntaresWalletApi.Common;
using AntaresWalletApi.Common.Configuration;
using AntaresWalletApi.Common.Domain.MyNoSqlEntities;
using Autofac;
using Lykke.ApiClients.V1;
using MyNoSqlServer.Abstractions;
using MyNoSqlServer.DataReader;
using Swisschain.Sdk.Server.Common;

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
            builder.Register(ctx =>
            {
                var factory = ctx.Resolve<IHttpClientFactory>();
                return new LykkeWalletAPIv1Client(factory.CreateClient(HttpClientNames.WalletApiV1));
            }).As<ILykkeWalletAPIv1Client>();

            builder.Register(ctx =>
            {
                var client = new MyNoSqlTcpClient(() => _config.MyNoSqlServer.ReaderServiceUrl, $"{ApplicationInformation.AppName}-{Environment.MachineName}");
                client.Start();
                return client;
            }).AsSelf().SingleInstance();

            builder.Register(ctx =>
                new MyNoSqlReadRepository<PriceEntity>(ctx.Resolve<MyNoSqlTcpClient>(), _config.MyNoSqlServer.PricesTableName)
            ).As<IMyNoSqlServerDataReader<PriceEntity>>().SingleInstance();
        }
    }
}

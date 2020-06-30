using System;
using System.Net.Http;
using AntaresWalletApi.Common;
using AntaresWalletApi.Common.Configuration;
using AntaresWalletApi.Common.Domain.MyNoSqlEntities;
using AntaresWalletApi.Common.Domain.Services;
using AntaresWalletApi.Services;
using Autofac;
using Lykke.ApiClients.V1;
using Lykke.Common.Log;
using Microsoft.Extensions.Logging;
using MyNoSqlServer.Abstractions;
using MyNoSqlServer.DataReader;
using Swisschain.Lykke.AntaresWalletApi.ApiContract;
using Swisschain.LykkeLog.Adapter;
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
                var logger = ctx.Resolve<ILoggerFactory>();
                return logger.ToLykke();
            }).As<ILogFactory>();

            builder.Register(ctx =>
            {
                var client = new MyNoSqlTcpClient(() => _config.MyNoSqlServer.ReaderServiceUrl, $"{ApplicationInformation.AppName}-{Environment.MachineName}");
                client.Start();
                return client;
            }).AsSelf().SingleInstance();

            builder.Register(ctx =>
                new MyNoSqlReadRepository<PriceEntity>(ctx.Resolve<MyNoSqlTcpClient>(), _config.MyNoSqlServer.PricesTableName)
            ).As<IMyNoSqlServerDataReader<PriceEntity>>().SingleInstance();

            builder.RegisterType<StreamService<PriceUpdate>>()
                .WithParameter(TypedParameter.From(true))
                .As<IStreamService<PriceUpdate>>()
                .SingleInstance();

            builder.RegisterType<ApplicationManager>()
                .As<IStartable>()
                .AutoActivate()
                .SingleInstance();
        }
    }
}

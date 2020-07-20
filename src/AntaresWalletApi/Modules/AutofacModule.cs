using System;
using System.Net.Http;
using AntaresWalletApi.Common;
using AntaresWalletApi.Common.Configuration;
using AntaresWalletApi.Common.Domain.MyNoSqlEntities;
using AntaresWalletApi.Common.Domain.Services;
using AntaresWalletApi.Infrastructure.Authentication;
using AntaresWalletApi.Services;
using Autofac;
using Lykke.ApiClients.V1;
using Lykke.ApiClients.V2;
using Lykke.Common.Log;
using Lykke.Service.Assets.Client;
using Lykke.Service.Balances.Client;
using Lykke.Service.CandlesHistory.Client;
using Lykke.Service.ClientAccount.Client;
using Lykke.Service.Session.Client;
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
                })
                .As<ILykkeWalletAPIv1Client>();

            builder.Register(ctx =>
                {
                    var factory = ctx.Resolve<IHttpClientFactory>();
                    return new LykkeWalletAPIv2Client(factory.CreateClient(HttpClientNames.WalletApiV2));
                })
                .As<ILykkeWalletAPIv2Client>();

            builder.RegisterClientSessionClient(
                new SessionServiceClientSettings {ServiceUrl = _config.Services.SessionServiceUrl});

            builder.RegisterType<GrpcPrincipal>().As<IGrpcPrincipal>().InstancePerLifetimeScope();

            builder.Register(ctx =>
                {
                    var logger = ctx.Resolve<ILoggerFactory>();
                    return logger.ToLykke();
                })
                .As<ILogFactory>();

            builder.Register(ctx =>
                {
                    var client = new MyNoSqlTcpClient(() => _config.MyNoSqlServer.ReaderServiceUrl,
                        $"{ApplicationInformation.AppName}-{Environment.MachineName}");
                    client.Start();
                    return client;
                })
                .AsSelf()
                .SingleInstance();

            builder.Register(ctx =>
                    new MyNoSqlReadRepository<PriceEntity>(ctx.Resolve<MyNoSqlTcpClient>(),
                        _config.MyNoSqlServer.PricesTableName)
                )
                .As<IMyNoSqlServerDataReader<PriceEntity>>()
                .SingleInstance();

            builder.Register(ctx =>
                    new MyNoSqlReadRepository<CandleEntity>(ctx.Resolve<MyNoSqlTcpClient>(),
                        _config.MyNoSqlServer.CandlesTableName)
                )
                .As<IMyNoSqlServerDataReader<CandleEntity>>()
                .SingleInstance();

            builder.RegisterType<StreamService<PriceUpdate>>()
                .WithParameter(TypedParameter.From(true))
                .As<IStreamService<PriceUpdate>>()
                .SingleInstance();

            builder.RegisterType<StreamService<CandleUpdate>>()
                .WithParameter(TypedParameter.From(true))
                .As<IStreamService<CandleUpdate>>()
                .SingleInstance();

            builder.RegisterType<ApplicationManager>()
                .As<IStartable>()
                .AutoActivate()
                .SingleInstance();

            builder.RegisterInstance(
                    new Candleshistoryservice(new Uri(_config.Services.CandlesHistoryServiceUrl), new HttpClient())
                )
                .As<ICandleshistoryservice>();

            builder.RegisterAssetsClient(AssetServiceSettings.Create(new Uri(_config.Services.AssetsServiceUrl),
                TimeSpan.FromMinutes(60)));

            builder.RegisterBalancesClient(_config.Services.BalancesServiceUrl);
            builder.RegisterMeClient(_config.MatchingEngine.GetIpEndPoint());
            builder.RegisterClientAccountClient(_config.Services.ClientAccountServiceUrl);

            builder.RegisterType<AssetsHelper>().AsSelf().SingleInstance();
            builder.RegisterType<ValidationService>().AsSelf().SingleInstance();
            builder.RegisterInstance(_config.WalletApi);
        }
    }
}

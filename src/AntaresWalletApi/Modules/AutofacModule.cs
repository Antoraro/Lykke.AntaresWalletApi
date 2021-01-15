using System;
using System.Net.Http;
using System.Threading;
using AntaresWalletApi.Common;
using AntaresWalletApi.Common.Configuration;
using AntaresWalletApi.Common.Domain.MyNoSqlEntities;
using AntaresWalletApi.Infrastructure.Authentication;
using AntaresWalletApi.RabbitSubscribers;
using AntaresWalletApi.Services;
using Autofac;
using Lykke.ApiClients.V1;
using Lykke.ApiClients.V2;
using Lykke.Common.Log;
using Lykke.Service.Assets.Client;
using Lykke.Service.Balances.Client;
using Lykke.Service.CandlesHistory.Client;
using Lykke.Service.ClientAccount.Client;
using Lykke.Service.PushNotifications.Client;
using Lykke.Service.RateCalculator.Client;
using Lykke.Service.Registration;
using Lykke.Service.Session.Client;
using Lykke.Service.TradesAdapter.Client;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Redis;
using Microsoft.Extensions.Logging;
using MyNoSqlServer.Abstractions;
using MyNoSqlServer.DataReader;
using Swisschain.LykkeLog.Adapter;
using Swisschain.Sdk.Server.Common;

namespace AntaresWalletApi.Modules
{
    public class AutofacModule : Module
    {
        private readonly AppConfig _config;
        private ILogFactory _logFactory;

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
                    _logFactory = logger.ToLykke();
                    return _logFactory;
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

            builder.Register(ctx =>
                    new MyNoSqlReadRepository<TickerEntity>(ctx.Resolve<MyNoSqlTcpClient>(),
                        _config.MyNoSqlServer.TickersTableName)
                )
                .As<IMyNoSqlServerDataReader<TickerEntity>>()
                .SingleInstance();

            builder.Register(ctx =>
                new MyNoSqlReadRepository<OrderbookEntity>(ctx.Resolve<MyNoSqlTcpClient>(),
                    _config.MyNoSqlServer.OrderbooksTableName)
            ).As<IMyNoSqlServerDataReader<OrderbookEntity>>().SingleInstance();

            builder.Register(ctx =>
                new MyNoSqlReadRepository<SessionEntity>(ctx.Resolve<MyNoSqlTcpClient>(),
                    _config.MyNoSqlServer.SessionsTableName)
            ).As<IMyNoSqlServerDataReader<SessionEntity>>().SingleInstance();

            builder.Register(ctx =>
            {
                return new MyNoSqlServer.DataWriter.MyNoSqlServerDataWriter<SessionEntity>(() =>
                        _config.MyNoSqlServer.WriterServiceUrl,
                    _config.MyNoSqlServer.SessionsTableName);
            }).As<IMyNoSqlServerDataWriter<SessionEntity>>().SingleInstance();

            builder.RegisterType<PricesStreamService>()
                .WithParameter(TypedParameter.From(true))
                .WithParameter("jobPeriod", TimeSpan.FromSeconds(1))
                .AsSelf()
                .SingleInstance();

            builder.RegisterType<CandlesStreamService>()
                .WithParameter(TypedParameter.From(true))
                .AsSelf()
                .SingleInstance();

            builder.RegisterType<OrderbookStreamService>()
                .WithParameter(TypedParameter.From(true))
                .AsSelf()
                .SingleInstance();

            builder.RegisterType<PublicTradesStreamService>()
                .WithParameter(TypedParameter.From(true))
                .AsSelf()
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
            builder.RegisterRateCalculatorClient(_config.Services.RateCalculatorServiceUrl);
            var cache = new RedisCache(new RedisCacheOptions
            {
                Configuration = _config.Redis.RedisConfiguration,
                InstanceName = _config.Redis.InstanceName
            });

            builder.RegisterInstance(cache)
                .As<IDistributedCache>()
                .SingleInstance();

            builder.RegisterType<OrderbooksService>()
                .WithParameter(TypedParameter.From(_config.Redis.OrderBooksCacheKeyPattern))
                .AsSelf()
                .SingleInstance();

            builder.Register(ctx =>
                    new TradesAdapterClient(_config.Services.TradesAdapterServiceUrl,
                        ctx.Resolve<ILogFactory>().CreateLog(nameof(TradesAdapterClient)))
                )
                .As<ITradesAdapterClient>()
                .SingleInstance();

            builder.RegisterRegistrationServiceClient(new RegistrationServiceClientSettings{ServiceUrl = _config.Services.RegistrationServiceUrl});

            builder.RegisterType<SessionService>()
                .WithParameter(TypedParameter.From(_config.SessionLifetimeInMins))
                .AsSelf()
                .SingleInstance();

            builder.RegisterType<PublicTradesSubscriber>()
                .As<IStartable>()
                .AutoActivate()
                .WithParameter("connectionString", _config.RabbitMq.PublicTrades.ConnectionString)
                .WithParameter("exchangeName", _config.RabbitMq.PublicTrades.ExchangeName)
                .SingleInstance();

            builder.RegisterPushNotificationsClient(_config.Services.PushNotificationsServiceUrl);
        }
    }
}

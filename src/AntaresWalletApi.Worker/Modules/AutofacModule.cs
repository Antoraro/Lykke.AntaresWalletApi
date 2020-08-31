using AntaresWalletApi.Common.Configuration;
using AntaresWalletApi.Common.Domain.MyNoSqlEntities;
using AntaresWalletApi.Worker.RabbitSubscribers;
using Autofac;
using Lykke.Common.Log;
using Microsoft.Extensions.Logging;
using MyNoSqlServer.Abstractions;
using Swisschain.LykkeLog.Adapter;

namespace AntaresWalletApi.Worker.Modules
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
                var logger = ctx.Resolve<ILoggerFactory>();
                return logger.ToLykke();
            }).As<ILogFactory>();

            builder.RegisterType<CandlesSubscriber>()
                .As<IStartable>()
                .AutoActivate()
                .WithParameter("connectionString", _config.RabbitMq.Candles.ConnectionString)
                .WithParameter("exchangeName", _config.RabbitMq.Candles.ExchangeName)
                .SingleInstance();

            builder.Register(ctx =>
            {
                return new MyNoSqlServer.DataWriter.MyNoSqlServerDataWriter<CandleEntity>(() =>
                        _config.MyNoSqlServer.WriterServiceUrl,
                    _config.MyNoSqlServer.CandlesTableName);
            }).As<IMyNoSqlServerDataWriter<CandleEntity>>().SingleInstance();
        }
    }
}

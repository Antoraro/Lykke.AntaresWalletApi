using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AntaresWalletApi.Common.Domain.MyNoSqlEntities;
using Autofac;
using AutoMapper;
using JetBrains.Annotations;
using Lykke.Common.Log;
using Lykke.Job.CandlesProducer.Contract;
using Lykke.RabbitMqBroker;
using Lykke.RabbitMqBroker.Subscriber;
using MyNoSqlServer.Abstractions;

namespace AntaresWalletApi.Worker.RabbitSubscribers
{
    [UsedImplicitly]
    public class CandlesSubscriber : IStartable, IDisposable
    {
        private readonly string _connectionString;
        private readonly string _exchangeName;
        private readonly IMyNoSqlServerDataWriter<CandleEntity> _candlesWriter;
        private readonly IMapper _mapper;
        private readonly ILogFactory _logFactory;
        private RabbitMqSubscriber<CandlesUpdatedEvent> _subscriber;

        public CandlesSubscriber(
            string connectionString,
            string exchangeName,
            IMyNoSqlServerDataWriter<CandleEntity> candlesWriter,
            IMapper mapper,
            ILogFactory logFactory)
        {
            _connectionString = connectionString;
            _exchangeName = exchangeName;
            _candlesWriter = candlesWriter;
            _mapper = mapper;
            _logFactory = logFactory;
        }

        public void Start()
        {
            var settings = RabbitMqSubscriptionSettings
                .ForSubscriber(_connectionString, _exchangeName, $"antares-api-{nameof(CandlesSubscriber)}");

            settings.DeadLetterExchangeName = null;

            _subscriber = new RabbitMqSubscriber<CandlesUpdatedEvent>(_logFactory,
                    settings,
                    new ResilientErrorHandlingStrategy(_logFactory, settings, TimeSpan.FromSeconds(10)))
                .SetMessageDeserializer(new MessagePackMessageDeserializer<CandlesUpdatedEvent>())
                .Subscribe(ProcessMessageAsync)
                .CreateDefaultBinding()
                .Start();
        }

        private async Task ProcessMessageAsync(CandlesUpdatedEvent message)
        {
            if (message.Candles == null || !message.Candles.Any())
                return;

            var intervalToSkip = new List<CandleTimeInterval>
            {
                CandleTimeInterval.Unspecified,
                CandleTimeInterval.Sec,
                CandleTimeInterval.Minute
            };

            var candlesToStore = message.Candles.Where(x =>
                    !string.IsNullOrEmpty(x.AssetPairId) && x.IsLatestChange &&
                    !intervalToSkip.Contains(x.TimeInterval) && x.PriceType != CandlePriceType.Unspecified)
                .ToList();

            var candles = _mapper.Map<List<CandleEntity>>(candlesToStore);

            await _candlesWriter.BulkInsertOrReplaceAsync(candles);

            Task.Run(async () =>
            {
                await _candlesWriter.CleanAndKeepMaxPartitions(0);
            });
        }

        public void Dispose()
        {
            _subscriber?.Dispose();
        }
    }
}

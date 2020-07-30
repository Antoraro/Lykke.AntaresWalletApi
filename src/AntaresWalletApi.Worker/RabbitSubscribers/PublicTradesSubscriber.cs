using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AntaresWalletApi.Common.Domain.MyNoSqlEntities;
using Autofac;
using AutoMapper;
using JetBrains.Annotations;
using Lykke.Common.Log;
using Lykke.RabbitMqBroker;
using Lykke.RabbitMqBroker.Subscriber;
using Lykke.Service.TradesAdapter.Contract;
using MyNoSqlServer.Abstractions;

namespace AntaresWalletApi.Worker.RabbitSubscribers
{
    [UsedImplicitly]
    public class PublicTradesSubscriber : IStartable, IDisposable
    {
        private readonly string _connectionString;
        private readonly string _exchangeName;
        private readonly IMyNoSqlServerDataWriter<PublicTradeEntity> _publicTradesWriter;
        private readonly IMapper _mapper;
        private readonly ILogFactory _logFactory;
        private RabbitMqSubscriber<List<Trade>> _subscriber;

        public PublicTradesSubscriber(
            string connectionString,
            string exchangeName,
            IMyNoSqlServerDataWriter<PublicTradeEntity> publicTradesWriter,
            IMapper mapper,
            ILogFactory logFactory)
        {
            _connectionString = connectionString;
            _exchangeName = exchangeName;
            _publicTradesWriter = publicTradesWriter;
            _mapper = mapper;
            _logFactory = logFactory;
        }

        public void Start()
        {
            var settings = RabbitMqSubscriptionSettings
                .ForSubscriber(_connectionString, _exchangeName, $"antares-api-{nameof(PublicTradesSubscriber)}");

            settings.DeadLetterExchangeName = null;

            _subscriber = new RabbitMqSubscriber<List<Trade>>(_logFactory,
                    settings,
                    new ResilientErrorHandlingStrategy(_logFactory, settings, TimeSpan.FromSeconds(10)))
                .SetMessageDeserializer(new MessagePackMessageDeserializer<List<Trade>>())
                .Subscribe(ProcessMessageAsync)
                .CreateDefaultBinding()
                .Start();
        }

        private async Task ProcessMessageAsync(List<Trade> message)
        {
            if (!message.Any())
                return;

            var trades = _mapper.Map<List<PublicTradeEntity>>(message);

            await _publicTradesWriter.BulkInsertOrReplaceAsync(trades);

            Task.Run(async () =>
            {
                await _publicTradesWriter.CleanAndKeepMaxPartitions(0);
            });
        }

        public void Dispose()
        {
            _subscriber?.Dispose();
        }
    }
}

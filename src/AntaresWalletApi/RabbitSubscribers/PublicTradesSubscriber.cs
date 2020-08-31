using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AntaresWalletApi.Services;
using Autofac;
using AutoMapper;
using JetBrains.Annotations;
using Lykke.Common.Log;
using Lykke.RabbitMqBroker;
using Lykke.RabbitMqBroker.Subscriber;
using Lykke.Service.TradesAdapter.Contract;
using Swisschain.Lykke.AntaresWalletApi.ApiContract;

namespace AntaresWalletApi.RabbitSubscribers
{
    [UsedImplicitly]
    public class PublicTradesSubscriber : IStartable, IDisposable
    {
        private readonly string _connectionString;
        private readonly string _exchangeName;
        private readonly PublicTradesStreamService _publicTradesStreamService;
        private readonly IMapper _mapper;
        private readonly ILogFactory _logFactory;
        private RabbitMqSubscriber<List<Trade>> _subscriber;

        public PublicTradesSubscriber(
            string connectionString,
            string exchangeName,
            PublicTradesStreamService publicTradesStreamService,
            IMapper mapper,
            ILogFactory logFactory)
        {
            _connectionString = connectionString;
            _exchangeName = exchangeName;
            _publicTradesStreamService = publicTradesStreamService;
            _mapper = mapper;
            _logFactory = logFactory;
        }

        public void Start()
        {
            var settings = RabbitMqSubscriptionSettings
                .ForSubscriber(_connectionString, _exchangeName, $"antares-api-{nameof(PublicTradesSubscriber)}-{Environment.MachineName}");

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

            var tradesByAssetId = message.GroupBy(x => x.AssetPairId);
            var tasks = new List<Task>();

            foreach (var tradeByAsset in tradesByAssetId)
            {
                var tradesUpdate = new PublicTradeUpdate();
                tradesUpdate.Trades.AddRange( _mapper.Map<List<PublicTrade>>(tradeByAsset.ToList()));
                tasks.Add(_publicTradesStreamService.WriteToStreamAsync(tradesUpdate, tradeByAsset.Key));
            }

            await Task.WhenAll(tasks);
        }

        public void Dispose()
        {
            _subscriber?.Dispose();
        }
    }
}

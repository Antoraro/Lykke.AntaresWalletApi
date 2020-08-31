using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using AntaresWalletApi.Common.Domain.MyNoSqlEntities;
using Autofac;
using AutoMapper;
using Google.Protobuf.WellKnownTypes;
using MyNoSqlServer.Abstractions;
using MyNoSqlServer.DataReader;
using Swisschain.Lykke.AntaresWalletApi.ApiContract;

namespace AntaresWalletApi.Services
{
    public class ApplicationManager : IStartable, IDisposable
    {
        private readonly MyNoSqlTcpClient _noSqlTcpClient;
        private readonly IMyNoSqlServerDataReader<PriceEntity> _pricesReader;
        private readonly IMyNoSqlServerDataReader<CandleEntity> _candlesReader;
        private readonly IMyNoSqlServerDataReader<TickerEntity> _tickersReader;
        private readonly IMyNoSqlServerDataReader<OrderbookEntity> _orderbooksReader;
        private readonly IMyNoSqlServerDataReader<PublicTradeEntity> _publicTradesReader;
        private readonly IMyNoSqlServerDataReader<SessionEntity> _sessionsReader;
        private readonly PricesStreamService _priceStream;
        private readonly CandlesStreamService _candlesStream;
        private readonly OrderbookStreamService _orderbookStream;
        private readonly PublicTradesStreamService _publicTradesStream;
        private readonly IMapper _mapper;

        public ApplicationManager(
            MyNoSqlTcpClient noSqlTcpClient,
            IMyNoSqlServerDataReader<PriceEntity> pricesReader,
            IMyNoSqlServerDataReader<CandleEntity> candlesReader,
            IMyNoSqlServerDataReader<TickerEntity> tickersReader,
            IMyNoSqlServerDataReader<OrderbookEntity> orderbooksReader,
            IMyNoSqlServerDataReader<PublicTradeEntity> publicTradesReader,
            IMyNoSqlServerDataReader<SessionEntity> sessionsReader,
            PricesStreamService priceStream,
            CandlesStreamService candlesStream,
            OrderbookStreamService orderbookStream,
            PublicTradesStreamService publicTradesStream,
            IMapper mapper
            )
        {
            _noSqlTcpClient = noSqlTcpClient;
            _pricesReader = pricesReader;
            _candlesReader = candlesReader;
            _tickersReader = tickersReader;
            _orderbooksReader = orderbooksReader;
            _publicTradesReader = publicTradesReader;
            _sessionsReader = sessionsReader;
            _priceStream = priceStream;
            _candlesStream = candlesStream;
            _orderbookStream = orderbookStream;
            _publicTradesStream = publicTradesStream;
            _mapper = mapper;
        }

        public void Start()
        {
            _pricesReader.SubscribeToChanges(prices =>
            {
                var tasks = new List<Task>();

                foreach (var price in prices)
                {
                    tasks.Add(_priceStream.WriteToStreamAsync(_mapper.Map<PriceUpdate>(price), price.AssetPairId));
                }

                Task.WhenAll(tasks).GetAwaiter().GetResult();
            });

            _candlesReader.SubscribeToChanges(candles =>
            {
                var tasks = new List<Task>();

                foreach (var candle in candles)
                {
                    var key = $"{candle.AssetPairId}_{candle.PriceType}_{candle.TimeInterval}";
                    tasks.Add(_candlesStream.WriteToStreamAsync(_mapper.Map<CandleUpdate>(candle), key));
                }

                Task.WhenAll(tasks).GetAwaiter().GetResult();
            });

            _tickersReader.SubscribeToChanges(tickers =>
            {
                var tasks = new List<Task>();

                foreach (var ticker in tickers)
                {
                    var priceEntity = _pricesReader.Get(PriceEntity.GetPk(), ticker.AssetPairId);

                    var priceUpdate = new PriceUpdate
                    {
                        AssetPairId = ticker.AssetPairId,
                        VolumeBase24H = ticker.VolumeBase.ToString(CultureInfo.InvariantCulture),
                        VolumeQuote24H = ticker.VolumeQuote.ToString(CultureInfo.InvariantCulture),
                        PriceChange24H = ticker.PriceChange.ToString(CultureInfo.InvariantCulture),
                        Timestamp = Timestamp.FromDateTime(ticker.UpdatedDt.ToUniversalTime())
                    };

                    if (priceEntity != null)
                    {
                        priceUpdate.Ask = priceEntity.Ask.ToString(CultureInfo.InvariantCulture);
                        priceUpdate.Bid = priceEntity.Bid.ToString(CultureInfo.InvariantCulture);
                    }

                    tasks.Add(_priceStream.WriteToStreamAsync(priceUpdate, priceUpdate.AssetPairId));
                }

                Task.WhenAll(tasks).GetAwaiter().GetResult();
            });

            _orderbooksReader.SubscribeToChanges(orderbooks =>
            {
                var tasks = new List<Task>();

                foreach (var orderbook in orderbooks)
                {
                    var item = _mapper.Map<Orderbook>(orderbook);
                    item.Asks.AddRange(_mapper.Map<List<Orderbook.Types.PriceVolume>>(orderbook.Asks));
                    item.Bids.AddRange(_mapper.Map<List<Orderbook.Types.PriceVolume>>(orderbook.Bids));
                    tasks.Add(_orderbookStream.WriteToStreamAsync(item, orderbook.AssetPairId));
                }

                Task.WhenAll(tasks).GetAwaiter().GetResult();
            });

            _publicTradesReader.SubscribeToChanges(trades =>
            {
                var tradesByAssetId = trades.GroupBy(x => x.AssetPairId);
                var tasks = new List<Task>();

                foreach (var tradeByAsset in tradesByAssetId)
                {
                    var tradesUpdate = new PublicTradeUpdate();
                    tradesUpdate.Trades.AddRange( _mapper.Map<List<PublicTrade>>(tradeByAsset.ToList()));
                    tasks.Add(_publicTradesStream.WriteToStreamAsync(tradesUpdate, tradeByAsset.Key));
                }

                Task.WhenAll(tasks).GetAwaiter().GetResult();
            });

            _sessionsReader.SubscribeToChanges(sessions => { });

            Console.WriteLine("Stream services started.");
        }

        public void Dispose()
        {
            _priceStream.Stop();
            _noSqlTcpClient.Stop();
            Console.WriteLine("Stream services stopped.");
        }
    }
}

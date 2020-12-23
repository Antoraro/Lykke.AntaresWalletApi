using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AntaresWalletApi.Common.Domain;
using AntaresWalletApi.Common.Domain.MyNoSqlEntities;
using Grpc.Core;
using Swisschain.Lykke.AntaresWalletApi.ApiContract;

namespace AntaresWalletApi.GrpcServices
{
    public partial class ApiService
    {
        public override async Task GetPriceUpdates(PriceUpdatesRequest request, IServerStreamWriter<PriceUpdate> responseStream, ServerCallContext context)
        {
            Console.WriteLine($"New price stream connect. peer:{context.Peer}");

            var entities = _pricesReader.Get(PriceEntity.GetPk());

            var prices = _mapper.Map<List<PriceUpdate>>(entities);

            if (request.AssetPairIds.Any())
                prices = prices.Where(x => request.AssetPairIds.Contains(x.AssetPairId)).ToList();

            var streamInfo = new StreamInfo<PriceUpdate>
            {
                Stream = responseStream,
                Peer = context.Peer,
                Keys = request.AssetPairIds.ToArray(),
                CancelationToken = context.CancellationToken
            };

            var task = await _priceStreamService.RegisterStreamAsync(streamInfo, prices);
            await task;
        }

        public override async Task GetCandleUpdates(CandleUpdatesRequest request, IServerStreamWriter<CandleUpdate> responseStream, ServerCallContext context)
        {
            Console.WriteLine($"New candles stream connect. peer:{context.Peer}");

            var streamInfo = new StreamInfo<CandleUpdate>
            {
                Stream = responseStream,
                Peer = context.Peer,
                Keys = new [] {$"{request.AssetPairId}_{request.Type}_{request.Interval}"},
                CancelationToken = context.CancellationToken
            };

            var task = _candlesStreamService.RegisterStreamAsync(streamInfo);
            await task;
        }

        public override async Task GetOrderbookUpdates(OrderbookUpdatesRequest request,
            IServerStreamWriter<Orderbook> responseStream,
            ServerCallContext context)
        {
            Console.WriteLine($"New orderbook stream connect. peer:{context.Peer}");

            var data = await _orderbooksService.GetAsync(request.AssetPairId);

            var orderbooks = new List<Orderbook>();

            foreach (var item in data)
            {
                var orderbook = _mapper.Map<Orderbook>(item);
                orderbook.Asks.AddRange(_mapper.Map<List<Orderbook.Types.PriceVolume>>(item.Asks));
                orderbook.Bids.AddRange(_mapper.Map<List<Orderbook.Types.PriceVolume>>(item.Bids));
                orderbooks.Add(orderbook);
            }

            var streamInfo = new StreamInfo<Orderbook>
            {
                Stream = responseStream,
                CancelationToken = context.CancellationToken,
                Keys = new [] {request.AssetPairId},
                Peer = context.Peer
            };

            var task = await _orderbookStreamService.RegisterStreamAsync(streamInfo, orderbooks);
            await task;
        }

        public override async Task GetPublicTradeUpdates(PublicTradesUpdatesRequest request,
            IServerStreamWriter<PublicTradeUpdate> responseStream,
            ServerCallContext context)
        {
            Console.WriteLine($"New public trades stream connect. peer:{context.Peer}");

            var data = await _tradesAdapterClient.GetTradesByAssetPairIdAsync(request.AssetPairId, 0, 50);

            var trades = _mapper.Map<List<PublicTrade>>(data.Records);

            var initData = new PublicTradeUpdate();
            initData.Trades.AddRange(trades);

            var streamInfo = new StreamInfo<PublicTradeUpdate>
            {
                Stream = responseStream,
                CancelationToken = context.CancellationToken,
                Keys = new [] {request.AssetPairId},
                Peer = context.Peer
            };

            var task = await _publicTradesStreamService.RegisterStreamAsync(streamInfo, new List<PublicTradeUpdate>{initData});
            await task;
        }
    }
}

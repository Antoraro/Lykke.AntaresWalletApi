using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using AntaresWalletApi.Common.Domain.MyNoSqlEntities;
using AutoMapper;
using Common;
using Google.Protobuf.WellKnownTypes;
using Lykke.Service.Assets.Client;
using Microsoft.Extensions.Caching.Distributed;
using MyNoSqlServer.Abstractions;
using Newtonsoft.Json;
using Swisschain.Lykke.AntaresWalletApi.ApiContract;

namespace AntaresWalletApi.Services
{
    public class OrderbooksService
    {
        private readonly IDistributedCache _redisCache;
        private readonly IAssetsService _assetsService;
        private readonly IMyNoSqlServerDataReader<OrderbookEntity> _orderbooksReader;
        private readonly string _orderBooksCacheKeyPattern;
        private readonly IMapper _mapper;

        public OrderbooksService(
            IDistributedCache redisCache,
            IAssetsService assetsService,
            IMyNoSqlServerDataReader<OrderbookEntity> orderbooksReader,
            string orderBooksCacheKeyPattern,
            IMapper mapper
            )
        {
            _redisCache = redisCache;
            _assetsService = assetsService;
            _orderbooksReader = orderbooksReader;
            _orderBooksCacheKeyPattern = orderBooksCacheKeyPattern;
            _mapper = mapper;
        }

        public async Task<IReadOnlyCollection<Orderbook>> GetAsync(string assetPairId = null)
        {
            var orderbooks = new List<Orderbook>();

            if (assetPairId == null)
            {
                var assetPairs = await _assetsService.GetAssetPairsAsync();

                var results = await Task.WhenAll(assetPairs.Select(pair => GetOrderbookAsync(pair.Id)));

                orderbooks = results.ToList();
            }
            else
            {
                var orderbook = await GetOrderbookAsync(assetPairId);
                orderbooks.Add(orderbook);
            }

            return orderbooks;
        }

        public Orderbook GetOrderbookUpdates(Orderbook oldOrderbook, Orderbook newOrderbook)
        {
            var result = JsonConvert.DeserializeObject<Orderbook>(newOrderbook.ToJson());

            var asks = MergeLevels(oldOrderbook.Asks.Select(x =>
                new VolumePriceModel{Volume = Convert.ToDecimal(x.V), Price = Convert.ToDecimal(x.P)}).ToList(),
                newOrderbook.Asks.Select(x =>
                    new VolumePriceModel{Volume = Convert.ToDecimal(x.V), Price = Convert.ToDecimal(x.P)}).ToList()).OrderBy(x => x.Price).ToList();
            var bids = MergeLevels(oldOrderbook.Bids.Select(x =>
                new VolumePriceModel{Volume = Convert.ToDecimal(x.V), Price = Convert.ToDecimal(x.P)}).ToList(),
                newOrderbook.Bids.Select(x =>
                    new VolumePriceModel{Volume = Convert.ToDecimal(x.V), Price = Convert.ToDecimal(x.P)}).ToList()).OrderByDescending(x => x.Price).ToList();

            result.Asks.Clear();
            result.Bids.Clear();

            result.Asks.AddRange(asks.Select(x => new Orderbook.Types.PriceVolume{P = x.Price.ToString(CultureInfo.InvariantCulture), V = x.Volume.ToString(CultureInfo.InvariantCulture)}));
            result.Bids.AddRange(bids.Select(x => new Orderbook.Types.PriceVolume{P = x.Price.ToString(CultureInfo.InvariantCulture), V = x.Volume.ToString(CultureInfo.InvariantCulture)}));

            return result;
        }

        private List<VolumePriceModel> MergeLevels(List<VolumePriceModel> oldLevels, List<VolumePriceModel> newLevels)
        {
            var result = new List<VolumePriceModel>();

            foreach (var level in oldLevels)
            {
                var existingLevel = newLevels.FirstOrDefault(x => x.Price == level.Price);

                if (existingLevel == null)
                    result.Add(new VolumePriceModel{Price = level.Price, Volume = 0});
            }

            foreach (var level in newLevels)
            {
                var existingLevel = oldLevels.FirstOrDefault(x => x.Price == level.Price && x.Volume == level.Volume);

                if (existingLevel == null)
                    result.Add(level);
            }

            return result;
        }

        private async Task<Orderbook> GetOrderbookAsync(string assetPairId)
        {
            var orderbookEntity = _orderbooksReader.Get(OrderbookEntity.GetPk(), assetPairId);

            if (orderbookEntity != null)
            {
                var orderbook = _mapper.Map<Orderbook>(orderbookEntity);
                orderbook.Bids.AddRange(_mapper.Map<List<Orderbook.Types.PriceVolume>>(orderbookEntity.Bids));
                orderbook.Asks.AddRange(_mapper.Map<List<Orderbook.Types.PriceVolume>>(orderbookEntity.Asks));
                return orderbook;
            }

            var buyBook = GetOrderbook(assetPairId, true);
            var sellBook = GetOrderbook(assetPairId, false);

            await Task.WhenAll(buyBook, sellBook);

            var result = new Orderbook
            {
                AssetPairId = assetPairId,
                Timestamp = buyBook.Result.Timestamp > sellBook.Result.Timestamp
                    ? Timestamp.FromDateTime(buyBook.Result.Timestamp.ToUniversalTime())
                    : Timestamp.FromDateTime(sellBook.Result.Timestamp.ToUniversalTime()),
            };

            result.Bids.AddRange(buyBook.Result.Prices.Select(x => new Orderbook.Types.PriceVolume
                {
                    V = Math.Abs(x.Volume).ToString(CultureInfo.InvariantCulture),
                    P = x.Price.ToString(CultureInfo.InvariantCulture)
                }));

            result.Asks.AddRange(sellBook.Result.Prices.Select(x => new Orderbook.Types.PriceVolume
            {
                V = Math.Abs(x.Volume).ToString(CultureInfo.InvariantCulture),
                P = x.Price.ToString(CultureInfo.InvariantCulture)
            }));

            return result;
        }

        private async Task<OrderbookModel> GetOrderbook(string assetPair, bool buy)
        {
            var orderBook = await _redisCache.GetStringAsync(GetKeyForOrderBook(assetPair, buy));
            return orderBook != null
                ? JsonConvert.DeserializeObject<OrderbookModel>(orderBook)
                : new OrderbookModel { AssetPair = assetPair, Timestamp = DateTime.UtcNow };
        }

        private string GetKeyForOrderBook(string assetPairId, bool isBuy)
        {
            return string.Format(_orderBooksCacheKeyPattern, assetPairId, isBuy);
        }

        private class OrderbookModel
        {
            public string AssetPair { get; set; }
            public bool IsBuy { get; set; }
            public DateTime Timestamp { get; set; }
            public IReadOnlyCollection<VolumePriceModel> Prices { get; set; } = Array.Empty<VolumePriceModel>();
        }

        private class VolumePriceModel
        {
            public decimal Volume { get; set; }
            public decimal Price { get; set; }
        }
    }
}

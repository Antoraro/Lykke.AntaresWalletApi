using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using AntaresWalletApi.Common.Domain.MyNoSqlEntities;
using AntaresWalletApi.Extensions;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Lykke.ApiClients.V2;
using Lykke.Service.Assets.Client;
using Lykke.Service.CandlesHistory.Client;
using Lykke.Service.CandlesHistory.Client.Models;
using Newtonsoft.Json;
using Swisschain.Lykke.AntaresWalletApi.ApiContract;
using ApiExceptionV1 = Lykke.ApiClients.V1.ApiException;
using ApiExceptionV2 = Lykke.ApiClients.V2.ApiException;
using Candle = Swisschain.Lykke.AntaresWalletApi.ApiContract.Candle;

namespace AntaresWalletApi.GrpcServices
{
    public partial class ApiService
    {
        public override async Task<AssetsDictionaryResponse> AssetsDictionary(Empty request, ServerCallContext context)
        {
            var result = new AssetsDictionaryResponse();

            var categories = await _assetsService.AssetCategoryGetAllAsync();

            string clientId = context.GetClientId();
            string partnerId = context.GetParnerId();

            var assets = await _assetsHelper.GetAssetsAvailableToClientAsync(clientId, partnerId, true);

            result.Body = new AssetsDictionaryResponseBody();

            result.Body.Categories.AddRange(_mapper.Map<List<AssetCategory>>(categories));
            result.Body.Assets.AddRange(_mapper.Map<List<Asset>>(assets));

            var popularAssetPairs = await _assetsHelper.GetPopularPairsAsync(assets.Select(x => x.Id).ToList());

            foreach (var asset in result.Body.Assets)
            {
                if (popularAssetPairs.ContainsKey(asset.Id))
                    asset.PopularPairs.AddRange(popularAssetPairs[asset.Id]);
            }

            return result;
        }

        public override async Task<BaseAssetResponse> GetBaseAsset(Empty request, ServerCallContext context)
        {
            var result = new BaseAssetResponse();

            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV2Client.GetBaseAssetAsync(token);

                if (response != null)
                {
                    result.BaseAsset = new BaseAssetResponse.Types.BaseAsset
                    {
                        AssetId = response.BaseAssetId
                    };
                }

                return result;
            }
            catch (ApiExceptionV2 ex)
            {
                if (ex.StatusCode == 401)
                    throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid token"));

                if (ex.StatusCode == 400)
                {
                    result.Error = JsonConvert.DeserializeObject<ErrorV2>(ex.Response);
                    return result;
                }

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        public override async Task<EmptyResponseV2> SetBaseAsset(BaseAssetUpdateRequest request, ServerCallContext context)
        {
            var result = new EmptyResponseV2();

            try
            {
                var token = context.GetBearerToken();
                await _walletApiV2Client.SetBaseAssetAsync(new BaseAssetUpdateModel{BaseAssetId = request.BaseAssetId}, token);
                return result;
            }
            catch (ApiExceptionV2 ex)
            {
                if (ex.StatusCode == 401)
                    throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid token"));

                if (ex.StatusCode == 400)
                {
                    result.Error = JsonConvert.DeserializeObject<ErrorV2>(ex.Response);
                    return result;
                }

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        public override async Task<AssetPairsResponse> GetAssetPairs(Empty request, ServerCallContext context)
        {
            var result = new AssetPairsResponse();

            try
            {
                var response = await _walletApiV2Client.GetAssetPairsAsync();

                if (response != null)
                {
                    result.AssetPairs.AddRange(_mapper.Map<List<AssetPair>>(response.AssetPairs));
                }

                return result;
            }
            catch (ApiExceptionV2 ex)
            {
                if (ex.StatusCode == 401)
                    throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid token"));

                if (ex.StatusCode == 400)
                {
                    result.Error = JsonConvert.DeserializeObject<ErrorV2>(ex.Response);
                    return result;
                }

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        public override Task<PricesResponse> GetPrices(PricesRequest request, ServerCallContext context)
        {
            var entities = _pricesReader.Get(PriceEntity.GetPk());

            var result = new List<PriceUpdate>();

            if (entities.Any())
            {
                result = _mapper.Map<List<PriceUpdate>>(entities);
            }

            if (request.AssetPairIds.Any())
            {
                result = result.Where(x =>
                        request.AssetPairIds.Contains(x.AssetPairId, StringComparer.InvariantCultureIgnoreCase))
                    .ToList();
            }

            var response = new PricesResponse();

            response.Prices.AddRange(result);

            return Task.FromResult(response);
        }

        public override async Task<CandlesResponse> GetCandles(CandlesRequest request, ServerCallContext context)
        {
            var candles = await _candlesHistoryService.GetCandlesHistoryAsync(
                request.AssetPairId,
                _mapper.Map<CandlePriceType>(request.Type),
                _mapper.Map<CandleTimeInterval>(request.Interval),
                request.From.ToDateTime().ToUniversalTime(),
                request.To.ToDateTime().ToUniversalTime());

            var result = new CandlesResponse();

            result.Candles.AddRange(_mapper.Map<List<Candle>>(candles.History));

            return result;
        }

        public override async Task<BalancesResponse> GetBalances(Empty request, ServerCallContext context)
        {
            var clientId = context.GetClientId();
            var balances = await _balancesClient.GetClientBalances(clientId);

            var res = new BalancesResponse();
            res.Payload.AddRange(_mapper.Map<List<Balance>>(balances));

            return res;
        }

        public override async Task<Orderbook> GetOrderbook(OrderbookRequest request, ServerCallContext context)
        {
            var orderbook = (await _orderbooksService.GetAsync(request.AssetPairId)).FirstOrDefault();
            return orderbook;
        }

        public override async Task<MarketsResponse> GetMarkets(MarketsRequest request, ServerCallContext context)
        {
            var result = new MarketsResponse();

            try
            {
                var token = context.GetBearerToken();

                if (request.OptionalAssetPairIdCase == MarketsRequest.OptionalAssetPairIdOneofCase.None || string.IsNullOrEmpty(request.AssetPairId))
                {
                    var response = await _walletApiV2Client.GetMarketsAsync();
                    result.Markets.AddRange(_mapper.Map<List<MarketsResponse.Types.MarketModel>>(response));
                }
                else
                {
                    var response = await _walletApiV2Client.GetMarketsByAssetPairIdAsync(request.AssetPairId);

                    if (response != null)
                    {
                        result.Markets.Add(_mapper.Map<MarketsResponse.Types.MarketModel>(response));
                    }
                }

                return result;
            }
            catch (ApiExceptionV2 ex)
            {
                if (ex.StatusCode == 401)
                    throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid token"));

                if (ex.StatusCode == 400)
                {
                    result.Error = JsonConvert.DeserializeObject<ErrorV2>(ex.Response);
                    return result;
                }

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        public override async Task<AmountInBaseAssetResponse> GetAmountInBaseAsset(AmountInBaseRequest request, ServerCallContext context)
        {
            var assets = await _assetsHelper.GetAllAssetsAsync(false);

            var result = new AmountInBaseAssetResponse();

            var records = assets.Where(x => x.Id != request.AssetId)
                .Select(x => new Lykke.Service.RateCalculator.Client.AutorestClient.Models.BalanceRecord(1, x.Id))
                .ToList();

            var valuesInBase = await _rateCalculatorClient.FillBaseAssetDataAsync(records, request.AssetId);

            result.Values.AddRange(valuesInBase.Where(x => x.AmountInBase.HasValue && x.AmountInBase.Value > 0).Select(x => new AmountInBaseAssetResponse.Types.AmountInBasePayload{AssetId = x.AssetId, AmountInBase = x.AmountInBase.Value.ToString(CultureInfo.InvariantCulture)}));

            return result;
        }
    }
}

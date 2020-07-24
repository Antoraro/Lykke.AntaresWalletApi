using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using AntaresWalletApi.Common.Configuration;
using AntaresWalletApi.Common.Domain;
using AntaresWalletApi.Common.Domain.MyNoSqlEntities;
using AntaresWalletApi.Extensions;
using AntaresWalletApi.Services;
using AutoMapper;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Lykke.ApiClients.V1;
using Lykke.ApiClients.V2;
using Lykke.MatchingEngine.Connector.Abstractions.Services;
using Lykke.MatchingEngine.Connector.Models.Api;
using Lykke.Service.Assets.Client;
using Lykke.Service.Balances.Client;
using Lykke.Service.CandlesHistory.Client;
using Lykke.Service.CandlesHistory.Client.Models;
using Lykke.Service.ClientAccount.Client;
using Lykke.Service.ClientAccount.Client.Models;
using Lykke.Service.RateCalculator.Client;
using MyNoSqlServer.Abstractions;
using Newtonsoft.Json;
using Swisschain.Lykke.AntaresWalletApi.ApiContract;
using ApiExceptionV1 = Lykke.ApiClients.V1.ApiException;
using ApiExceptionV2 = Lykke.ApiClients.V2.ApiException;
using Candle = Swisschain.Lykke.AntaresWalletApi.ApiContract.Candle;
using CashOutFee = Swisschain.Lykke.AntaresWalletApi.ApiContract.CashOutFee;
using Enum = System.Enum;
using LimitOrderModel = Swisschain.Lykke.AntaresWalletApi.ApiContract.LimitOrderModel;
using LimitOrderRequest = Swisschain.Lykke.AntaresWalletApi.ApiContract.LimitOrderRequest;
using MarketOrderRequest = Swisschain.Lykke.AntaresWalletApi.ApiContract.MarketOrderRequest;
using Status = Grpc.Core.Status;
using UpgradeRequest = Swisschain.Lykke.AntaresWalletApi.ApiContract.UpgradeRequest;

namespace AntaresWalletApi.GrpcServices
{
    public class ApiService : Swisschain.Lykke.AntaresWalletApi.ApiContract.ApiService.ApiServiceBase
    {
        private readonly ILykkeWalletAPIv1Client _walletApiV1Client;
        private readonly ILykkeWalletAPIv2Client _walletApiV2Client;
        private readonly IAssetsService _assetsService;
        private readonly AssetsHelper _assetsHelper;
        private readonly IMyNoSqlServerDataReader<PriceEntity> _pricesReader;
        private readonly PricesStreamService _priceStreamService;
        private readonly CandlesStreamService _candlesStreamService;
        private readonly ICandleshistoryservice _candlesHistoryService;
        private readonly ValidationService _validationService;
        private readonly IMatchingEngineClient _matchingEngineClient;
        private readonly IBalancesClient _balancesClient;
        private readonly IClientAccountClient _clientAccountClient;
        private readonly IRateCalculatorClient _rateCalculatorClient;
        private readonly WalletApiConfig _walletApiConfig;
        private readonly IMapper _mapper;

        public ApiService(
            ILykkeWalletAPIv1Client walletApiV1Client,
            ILykkeWalletAPIv2Client walletApiV2Client,
            IAssetsService assetsService,
            AssetsHelper assetsHelper,
            IMyNoSqlServerDataReader<PriceEntity> pricesReader,
            PricesStreamService priceStreamService,
            CandlesStreamService candlesStreamService,
            ICandleshistoryservice candlesHistoryService,
            ValidationService validationService,
            IMatchingEngineClient matchingEngineClient,
            IBalancesClient balancesClient,
            IClientAccountClient clientAccountClient,
            IRateCalculatorClient rateCalculatorClient,
            WalletApiConfig walletApiConfig,
            IMapper mapper
        )
        {
            _walletApiV1Client = walletApiV1Client;
            _walletApiV2Client = walletApiV2Client;
            _assetsService = assetsService;
            _assetsHelper = assetsHelper;
            _pricesReader = pricesReader;
            _priceStreamService = priceStreamService;
            _candlesStreamService = candlesStreamService;
            _candlesHistoryService = candlesHistoryService;
            _validationService = validationService;
            _matchingEngineClient = matchingEngineClient;
            _balancesClient = balancesClient;
            _clientAccountClient = clientAccountClient;
            _rateCalculatorClient = rateCalculatorClient;
            _walletApiConfig = walletApiConfig;
            _mapper = mapper;
        }

        public override async Task<AssetsDictionaryResponse> AssetsDictionary(Empty request, ServerCallContext context)
        {
            var result = new AssetsDictionaryResponse();

            var categories = await _assetsService.AssetCategoryGetAllAsync();

            string clientId = context.GetClientId();
            string partnerId = context.GetParnerId();

            var assets = await _assetsHelper.GetAssetsAvailableToClientAsync(clientId, partnerId, true);

            result.Categories.AddRange(_mapper.Map<List<AssetCategory>>(categories));
            result.Assets.AddRange(_mapper.Map<List<Asset>>(assets));

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

        public override async Task<PricesResponse> GetPrices(PricesRequest request, ServerCallContext context)
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

            return response;
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

        public override async Task<PendingActionsResponse> GetPendingActions(Empty request, ServerCallContext context)
        {
            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV1Client.ClientGetPendingActionsAsync(token);

                var result = new PendingActionsResponse();

                if (response.Result != null)
                {
                    result.Result = _mapper.Map<PendingActionsResponse.Types.PendingActionsPayload>(response.Result);
                }

                if (response.Error != null)
                {
                    result.Error = _mapper.Map<ErrorV1>(response.Error);
                }

                return result;
            }
            catch (ApiExceptionV1 ex)
            {
                if (ex.StatusCode == 401)
                    throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid token"));

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

        public override async Task<LimitOrdersResponse> GetOrders(LimitOrdersRequest request, ServerCallContext context)
        {
            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV1Client.OffchainLimitListAsync(request.AssetPairId, token);

                var result = new LimitOrdersResponse();

                if (response.Result != null)
                {
                    result.Result = new LimitOrdersResponse.Types.OrdersPayload();
                    result.Result.Orders.AddRange(_mapper.Map<List<LimitOrderModel>>(response.Result.Orders));
                }

                if (response.Error != null)
                {
                    result.Error = _mapper.Map<ErrorV1>(result.Error);
                }

                return result;
            }
            catch (ApiExceptionV1 ex)
            {
                if (ex.StatusCode == 401)
                    throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid token"));

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        public override async Task<PlaceOrderResponse> PlaceLimitOrder(LimitOrderRequest request, ServerCallContext context)
        {
            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV1Client.HotWalletPlaceLimitOrderAsync(
                    new HotWalletLimitOperation
                    {
                        AssetPair = request.AssetPairId,
                        AssetId = request.AssetId,
                        Price = request.Price,
                        Volume = request.Volume
                    },
                    token,
                    _walletApiConfig.Secret);

                var result = new PlaceOrderResponse();

                if (response.Result != null)
                {
                    result = _mapper.Map<PlaceOrderResponse>(response);
                }

                if (response.Error != null)
                {
                    result.Error = _mapper.Map<ErrorV1>(response.Error);
                }

                return result;
            }
            catch (ApiExceptionV1 ex)
            {
                if (ex.StatusCode == 401)
                    throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid token"));

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        public override async Task<PlaceOrderResponse> PlaceMarketOrder(MarketOrderRequest request, ServerCallContext context)
        {
            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV1Client.HotWalletPlaceMarketOrderAsync(
                    new HotWalletOperation
                    {
                        AssetPair = request.AssetPairId,
                        AssetId = request.AssetId,
                        Volume = request.Volume
                    },
                    token,
                    _walletApiConfig.Secret);

                return _mapper.Map<PlaceOrderResponse>(response);
            }
            catch (ApiExceptionV1 ex)
            {
                if (ex.StatusCode == 401)
                    throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid token"));

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        public override async Task<CancelOrderResponse> CancelAllOrders(CancelOrdersRequest request, ServerCallContext context)
        {
            MeResponseModel response = null;

            if (!string.IsNullOrEmpty(request.AssetPairId))
            {
                var result = await _validationService.ValidateAssetPairAsync(request.AssetPairId);

                if (result != null)
                {
                    return new CancelOrderResponse
                    {
                        Error = new Error
                        {
                            Message = result.Message
                        }
                    };
                }

                bool? isBuy;

                if (request.OptionalSideCase == CancelOrdersRequest.OptionalSideOneofCase.None)
                {
                    isBuy = null;
                }
                else
                {
                    switch (request.Side)
                    {
                        case Side.Buy:
                            isBuy = true;
                            break;
                        case Side.Sell:
                            isBuy = false;
                            break;
                        default:
                            isBuy = null;
                            break;
                    }
                }

                var model = new LimitOrderMassCancelModel
                {
                    Id = Guid.NewGuid().ToString(),
                    AssetPairId = request.AssetPairId,
                    ClientId = context.GetClientId(),
                    IsBuy = isBuy
                };

                response = await _matchingEngineClient.MassCancelLimitOrdersAsync(model);
            }
            else
            {
                var orders = await GetOrders(new LimitOrdersRequest(), context);

                if (orders.Result.Orders.Any())
                {
                    var orderIds = orders.Result.Orders.Select(x => x.Id).ToList();
                    response = await _matchingEngineClient.CancelLimitOrdersAsync(orderIds);
                }
                else
                {
                    response = new MeResponseModel{Status = MeStatusCodes.Ok};
                }
            }

            if (response == null)
            {
                return new CancelOrderResponse
                {
                    Error = new Error
                    {
                        Message = ErrorMessages.MeNotAvailable
                    }
                };
            }

            if (response.Status == MeStatusCodes.Ok)
                return new CancelOrderResponse
                {
                    Payload = true
                };

            return new CancelOrderResponse
            {
                Error = new Error
                {
                    Message = response.Message ?? response.Status.ToString()
                }
            };
        }

        public override async Task<CancelOrderResponse> CancelOrder(CancelOrderRequest request, ServerCallContext context)
        {
            MeResponseModel response = await _matchingEngineClient.CancelLimitOrderAsync(request.OrderId);

            if (response == null)
            {
                return new CancelOrderResponse
                {
                    Error = new Error
                    {
                        Message = ErrorMessages.MeNotAvailable
                    }
                };
            }

            if (response.Status == MeStatusCodes.Ok)
                return new CancelOrderResponse
                {
                    Payload = true
                };

            return new CancelOrderResponse
            {
                Error = new Error
                {
                    Message = response.Message ?? response.Status.ToString()
                }
            };
        }

        public override async Task<TradesResponse> GetTrades(TradesRequest request, ServerCallContext context)
        {
            var result = new TradesResponse();

            try
            {
                var token = context.GetBearerToken();
                var wallets = await _clientAccountClient.Wallets.GetClientWalletsFilteredAsync(context.GetClientId(), WalletType.Trading);

                var walletId = wallets.FirstOrDefault()?.Id;

                var response = await _walletApiV2Client.GetTradesByWalletIdAsync(
                    walletId, request.AssetPairId, request.Take, request.Skip,
                    request.OptionalFromDateCase == TradesRequest.OptionalFromDateOneofCase.None ? (DateTimeOffset?) null : request.From.ToDateTimeOffset(),
                    request.OptionalToDateCase == TradesRequest.OptionalToDateOneofCase.None ? (DateTimeOffset?) null : request.To.ToDateTimeOffset(),
                    request.OptionalTradeTypeCase == TradesRequest.OptionalTradeTypeOneofCase.None ? null : (TradeType?)Enum.Parse(typeof(TradeType?), request.TradeType),
                    token);

                if (response != null)
                {
                    result.Trades.AddRange(_mapper.Map<List<TradesResponse.Types.TradeModel>>(response));
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

        public override async Task<WatchlistsResponse> GetWatchlists(Empty request, ServerCallContext context)
        {
            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV1Client.WatchListsGetListAsync(token);

                var result = new WatchlistsResponse();

                if (response.Result != null)
                {
                    foreach (var watchlist in response.Result)
                    {
                        var list = _mapper.Map<Watchlist>(watchlist);
                        list.AssetIds.AddRange(watchlist.AssetIds);

                        result.Result.Add(list);
                    }
                }

                if (response.Error != null)
                {
                    result.Error = _mapper.Map<ErrorV1>(response.Error);
                }

                return result;
            }
            catch (ApiExceptionV1 ex)
            {
                if (ex.StatusCode == 401)
                    throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid token"));

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        public override async Task<WatchlistResponse> GetWatchlist(WatchlistRequest request, ServerCallContext context)
        {
            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV1Client.WatchListsGetAsync(request.Id, token);

                var result = new WatchlistResponse();

                if (response.Result != null)
                {
                    result.Result = _mapper.Map<Watchlist>(response.Result);
                    result.Result.AssetIds.AddRange(response.Result.AssetIds);
                }

                if (response.Error != null)
                {
                    result.Error = _mapper.Map<ErrorV1>(response.Error);
                }

                return result;
            }
            catch (ApiExceptionV1 ex)
            {
                if (ex.StatusCode == 401)
                    throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid token"));

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        public override async Task<WatchlistResponse> AddWatchlist(AddWatchlistRequest request, ServerCallContext context)
        {
            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV1Client.WatchListsCreateAsync(
                new CustomWatchListCreateModel
                {
                    Name = request.Name,
                    Order = request.Order,
                    AssetIds = request.AssetIds.ToList()
                }, token);

                var result = new WatchlistResponse();

                if (response.Result != null)
                {
                    result.Result = _mapper.Map<Watchlist>(response.Result);
                    result.Result.AssetIds.AddRange(response.Result.AssetIds);
                }

                if (response.Error != null)
                {
                    result.Error = _mapper.Map<ErrorV1>(response.Error);
                }

                return result;
            }
            catch (ApiExceptionV1 ex)
            {
                if (ex.StatusCode == 401)
                    throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid token"));

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        public override async Task<WatchlistResponse> UpdateWatchlist(UpdateWatchlistRequest request, ServerCallContext context)
        {
            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV1Client.WatchListsUpdateAsync(
                    request.Id,
                    new CustomWatchListUpdateModel
                    {
                        Name = request.Name,
                        Order = request.Order,
                        AssetIds = request.AssetIds.ToList()
                    }, token);

                var result = new WatchlistResponse();

                if (response.Result != null)
                {
                    result.Result = _mapper.Map<Watchlist>(response.Result);
                    result.Result.AssetIds.AddRange(response.Result.AssetIds);
                }

                if (response.Error != null)
                {
                    result.Error = _mapper.Map<ErrorV1>(response.Error);
                }

                return result;
            }
            catch (ApiExceptionV1 ex)
            {
                if (ex.StatusCode == 401)
                    throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid token"));

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        public override async Task<DeleteWatchlistResponse> DeleteWatchlist(DeleteWatchlistRequest request, ServerCallContext context)
        {
            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV1Client.WatchListsDeleteAsync(request.Id, token);

                var result = new DeleteWatchlistResponse();

                if (response.Error != null)
                {
                    result.Error = _mapper.Map<ErrorV1>(response.Error);
                }

                return result;
            }
            catch (ApiExceptionV1 ex)
            {
                if (ex.StatusCode == 401)
                    throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid token"));

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        public override async Task<TierInfoRespone> GetTierInfo(Empty request, ServerCallContext context)
        {
            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV1Client.TiersGetInfoAsync(token);

                var result = new TierInfoRespone();

                if (response.Result != null)
                {
                    result.Result = new TierInfoPayload
                    {
                        CurrentTier = _mapper.Map<CurrentTier>(response.Result.CurrentTier),
                        NextTier = _mapper.Map<NextTier>(response.Result.NextTier),
                        UpgradeRequest = _mapper.Map<UpgradeRequest>(response.Result.UpgradeRequest),
                        QuestionnaireAnswered = response.Result.QuestionnaireAnswered
                    };
                }

                if (response.Error != null)
                {
                    result.Error = _mapper.Map<ErrorV1>(response.Error);
                }

                return result;
            }
            catch (ApiExceptionV1 ex)
            {
                if (ex.StatusCode == 401)
                    throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid token"));

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        public override async Task<WalletsResponse> GetWallets(Empty request, ServerCallContext context)
        {
            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV1Client.WalletsGetAsync(token);

                var result = new WalletsResponse();

                if (response.Result != null)
                {
                    result.Result = new WalletsResponse.Types.GetWalletsPayload
                    {
                        Lykke = new WalletsResponse.Types.LykkeWalletsPayload{Equity = response.Result.Lykke.Equity.ToString(CultureInfo.InvariantCulture)},
                        MultiSig = response.Result.MultiSig,
                        ColoredMultiSig = response.Result.ColoredMultiSig,
                        SolarCoinAddress = response.Result.SolarCoinAddress
                    };
                    result.Result.Lykke.Assets.AddRange(_mapper.Map<List<WalletsResponse.Types.WalletAsset>>(response.Result.Lykke.Assets));
                }

                if (response.Error != null)
                {
                    result.Error = _mapper.Map<ErrorV1>(response.Error);
                }

                return result;
            }
            catch (ApiExceptionV1 ex)
            {
                if (ex.StatusCode == 401)
                    throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid token"));

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        public override async Task<WalletResponse> GetWallet(WalletRequest request, ServerCallContext context)
        {
            var result = new WalletResponse();

            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV1Client.WalletsGetByIdAsync(request.AssetId, token);

                if (response.Result != null)
                {
                    result.Result = _mapper.Map<WalletResponse.Types.WalletPayload>(response.Result);
                }

                if (response.Error != null)
                {
                    result.Error = _mapper.Map<ErrorV1>(response.Error);
                }

                return result;
            }
            catch (ApiExceptionV1 ex)
            {
                if (ex.StatusCode == 401)
                    throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid token"));

                if (ex.StatusCode == 500)
                {
                    result = JsonConvert.DeserializeObject<WalletResponse>(ex.Response);
                    return result;
                }

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        public override async Task<GenerateWalletResponse> GenerateWallet(GenerateWalletRequest request, ServerCallContext context)
        {
            var result = new GenerateWalletResponse();

            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV1Client.WalletsGenerateWalletAsync(
                    new SubmitKeysModel
                    {
                        AssetId = request.AssetId,
                        BcnWallet = request.BcnWallet != null ? new BcnWallet
                        {
                            Address = request.BcnWallet.Address,
                            EncodedKey = request.BcnWallet.EncodedKey,
                            PublicKey = request.BcnWallet.PublicKey
                        } : null
                    }, token);

                if (response.Result != null)
                {
                    result.Result = _mapper.Map<GenerateWalletResponse.Types.WalletAddress>(response.Result);
                }

                if (response.Error != null)
                {
                    result.Error = _mapper.Map<ErrorV1>(response.Error);
                }

                return result;
            }
            catch (ApiExceptionV1 ex)
            {
                if (ex.StatusCode == 401)
                    throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid token"));

                if (ex.StatusCode == 500)
                {
                    result = JsonConvert.DeserializeObject<GenerateWalletResponse>(ex.Response);
                    return result;
                }

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        public override async Task<EmptyResponseV2> GenerateWalletV2(GenerateWalletV2Request request, ServerCallContext context)
        {
            var result = new EmptyResponseV2();

            try
            {
                var token = context.GetBearerToken();
                await _walletApiV2Client.PostCryptosDepositAddressesAsync(request.AssetId, token);
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

        public override async Task<SwiftCredentialsResponse> GetSwiftCredentials(SwiftCredentialsRequest request, ServerCallContext context)
        {
            var result = new SwiftCredentialsResponse();

            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV1Client.SwiftCredentialsGetByAssetIdAsync(request.AssetId, token);

                if (response.Result != null)
                {
                    result.Result = _mapper.Map<SwiftCredentialsResponse.Types.SwiftCredentials>(response.Result);
                }

                if (response.Error != null)
                {
                    result.Error = _mapper.Map<ErrorV1>(response.Error);
                }

                return result;
            }
            catch (ApiExceptionV1 ex)
            {
                if (ex.StatusCode == 401)
                    throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid token"));

                if (ex.StatusCode == 500)
                {
                    result = JsonConvert.DeserializeObject<SwiftCredentialsResponse>(ex.Response);
                    return result;
                }

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        public override async Task<EmptyResponse> SendBankTransferRequest(BankTransferRequest request, ServerCallContext context)
        {
            var result = new EmptyResponse();

            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV1Client.BankTransferRequestAsync(
                new TransferReqModel
                {
                    AssetId = request.AssetId,
                    BalanceChange = request.BalanceChange
                }, token);

                if (response.Error != null)
                {
                    result.Error = _mapper.Map<ErrorV1>(response.Error);
                }

                return result;
            }
            catch (ApiExceptionV1 ex)
            {
                if (ex.StatusCode == 401)
                    throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid token"));

                if (ex.StatusCode == 500)
                {
                    result = JsonConvert.DeserializeObject<EmptyResponse>(ex.Response);
                    return result;
                }

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        public override async Task<CountryPhoneCodesResponse> GetCountryPhoneCodes(Empty request, ServerCallContext context)
        {
            var result = new CountryPhoneCodesResponse();

            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV1Client.GetCountryPhoneCodesAsync();

                if (response.Result != null)
                {
                    result.Result = new CountryPhoneCodesResponse.Types.CountryPhoneCodes();
                    result.Result.Current = response.Result.Current;
                    result.Result.CountriesList.AddRange(_mapper.Map<List<Country>>(response.Result.CountriesList));
                }

                if (response.Error != null)
                {
                    result.Error = _mapper.Map<ErrorV1>(response.Error);
                }

                return result;
            }
            catch (ApiExceptionV1 ex)
            {
                if (ex.StatusCode == 401)
                    throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid token"));

                if (ex.StatusCode == 500)
                {
                    result = JsonConvert.DeserializeObject<CountryPhoneCodesResponse>(ex.Response);
                    return result;
                }

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        public override async Task<BankCardPaymentDetailsResponse> GetBankCardPaymentDetails(Empty request, ServerCallContext context)
        {
            var result = new BankCardPaymentDetailsResponse();

            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV1Client.GetBankCardPaymentUrlFormValuesAsync(token);

                if (response.Result != null)
                {
                    result.Result = _mapper.Map<BankCardPaymentDetailsResponse.Types.BankCardPaymentDetails>(response.Result);
                }

                if (response.Error != null)
                {
                    result.Error = _mapper.Map<ErrorV1>(response.Error);
                }

                return result;
            }
            catch (ApiExceptionV1 ex)
            {
                if (ex.StatusCode == 401)
                    throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid token"));

                if (ex.StatusCode == 500)
                {
                    result = JsonConvert.DeserializeObject<BankCardPaymentDetailsResponse>(ex.Response);
                    return result;
                }

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        public override async Task<BankCardPaymentUrlResponse> GetBankCardPaymentUrl(BankCardPaymentUrlRequest request, ServerCallContext context)
        {
            var result = new BankCardPaymentUrlResponse();

            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV1Client.BankCardPaymentUrlAsync(_mapper.Map<BankCardPaymentUrlInputModel>(request), string.Empty, token);

                if (response.Result != null)
                {
                    result.Result = _mapper.Map<BankCardPaymentUrlResponse.Types.BankCardPaymentUrl>(response.Result);
                }

                if (response.Error != null)
                {
                    result.Error = _mapper.Map<ErrorV1>(response.Error);
                }

                return result;
            }
            catch (ApiExceptionV1 ex)
            {
                if (ex.StatusCode == 401)
                    throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid token"));

                if (ex.StatusCode == 500)
                {
                    result = JsonConvert.DeserializeObject<BankCardPaymentUrlResponse>(ex.Response);
                    return result;
                }

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        public override async Task<EthereumSettingsResponse> GetEthereumSettings(Empty request, ServerCallContext context)
        {
            var result = new EthereumSettingsResponse();

            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV1Client.GetEthereumPrivateWalletSettingsAsync(token);

                if (response.Result != null)
                {
                    result.Result = _mapper.Map<EthereumSettingsResponse.Types.EthereumSettings>(response.Result);
                }

                if (response.Error != null)
                {
                    result.Error = _mapper.Map<ErrorV1>(response.Error);
                }

                return result;
            }
            catch (ApiExceptionV1 ex)
            {
                if (ex.StatusCode == 401)
                    throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid token"));

                if (ex.StatusCode == 500)
                {
                    result = JsonConvert.DeserializeObject<EthereumSettingsResponse>(ex.Response);
                    return result;
                }

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        public override async Task<CryptoDepositAddressResponse> GetCryptoDepositAddress(CryptoDepositAddressRequest request, ServerCallContext context)
        {
            var result = new CryptoDepositAddressResponse();

            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV2Client.GetCryptosDepositAddressesAsync(request.AssetId, token);

                if (response != null)
                {
                    result.Address = _mapper.Map<CryptoDepositAddressResponse.Types.CryptoDepositAddress>(response);
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

        public override async Task<WithdrawalCryptoInfoResponse> GetWithdrawalCryptoInfo(WithdrawalCryptoInfoRequest request, ServerCallContext context)
        {
            var result = new WithdrawalCryptoInfoResponse();

            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV2Client.GetAssetInfoAsync(request.AssetId, token);

                if (response != null)
                {
                    result.WithdrawalInfo = _mapper.Map<WithdrawalCryptoInfoResponse.Types.WithdrawalCryptoInfo>(response);
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

        public override async Task<SwiftCashoutInfoResponse> GetSwiftCashoutInfo(Empty request, ServerCallContext context)
        {
            var result = new SwiftCashoutInfoResponse();

            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV1Client.OffchainGetCashoutSwiftLastDataAsync(token);

                if (response.Result != null)
                {
                    result.Result = _mapper.Map<SwiftCashoutInfoResponse.Types.SwiftCashoutInfo>(response.Result);
                }

                if (response.Error != null)
                {
                    result.Error = _mapper.Map<ErrorV1>(response.Error);
                }

                return result;
            }
            catch (ApiExceptionV1 ex)
            {
                if (ex.StatusCode == 401)
                    throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid token"));

                if (ex.StatusCode == 500)
                {
                    result = JsonConvert.DeserializeObject<SwiftCashoutInfoResponse>(ex.Response);
                    return result;
                }

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        public override async Task<SwiftCashoutFeeResponse> GetSwiftCashoutFee(SwiftCashoutFeeRequest request, ServerCallContext context)
        {
            var result = new SwiftCashoutFeeResponse();

            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV1Client.OffchainCashoutSwiftFeeAsync(request.AssetId, request.CountryCode, token);

                if (response.Result != null)
                {
                    result.Result = _mapper.Map<SwiftCashoutFeeResponse.Types.SwiftCashoutFee>(response.Result);
                }

                if (response.Error != null)
                {
                    result.Error = _mapper.Map<ErrorV1>(response.Error);
                }

                return result;
            }
            catch (ApiExceptionV1 ex)
            {
                if (ex.StatusCode == 401)
                    throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid token"));

                if (ex.StatusCode == 500)
                {
                    result = JsonConvert.DeserializeObject<SwiftCashoutFeeResponse>(ex.Response);
                    return result;
                }

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        public override async Task<OffchainChannelKeyResponse> GetOffchainChannelKey(OffchainChannelKeyRequest request, ServerCallContext context)
        {
            var result = new OffchainChannelKeyResponse();

            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV1Client.GetOffchainChannelKeyAsync(request.AssetId, token);

                if (response.Result != null)
                {
                    result.Result = _mapper.Map<OffchainChannelKeyResponse.Types.OffchainChannel>(response.Result);
                }

                if (response.Error != null)
                {
                    result.Error = _mapper.Map<ErrorV1>(response.Error);
                }

                return result;
            }
            catch (ApiExceptionV1 ex)
            {
                if (ex.StatusCode == 401)
                    throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid token"));

                if (ex.StatusCode == 500)
                {
                    result = JsonConvert.DeserializeObject<OffchainChannelKeyResponse>(ex.Response);
                    return result;
                }

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        public override async Task<SwiftCashoutResponse> SwiftCashout(SwiftCashoutRequest request, ServerCallContext context)
        {
            var result = new SwiftCashoutResponse();

            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV1Client.OffchainCashoutSwiftAsync(_mapper.Map<OffchainCashoutSwiftModel>(request), token);

                if (response.Result != null)
                {
                    result.Result = _mapper.Map<SwiftCashoutResponse.Types.SwiftCashoutData>(response.Result);
                }

                if (response.Error != null)
                {
                    result.Error = _mapper.Map<ErrorV1>(response.Error);
                }

                return result;
            }
            catch (ApiExceptionV1 ex)
            {
                if (ex.StatusCode == 401)
                    throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid token"));

                if (ex.StatusCode == 500)
                {
                    result = JsonConvert.DeserializeObject<SwiftCashoutResponse>(ex.Response);
                    return result;
                }

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        public override async Task<SwiftCashoutFinalizeResponse> FinalizeSwiftCashout(SwiftCashoutFinalizeRequest request, ServerCallContext context)
        {
            var result = new SwiftCashoutFinalizeResponse();

            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV1Client.OffchainFinalizeAsync(_mapper.Map<OffchainFinalizeModel>(request), token);

                if (response.Result != null)
                {
                    result.Result = _mapper.Map<SwiftCashoutFinalizeResponse.Types.OffchainTradeRespone>(response.Result);
                }

                if (response.Error != null)
                {
                    result.Error = _mapper.Map<ErrorV1>(response.Error);
                }

                return result;
            }
            catch (ApiExceptionV1 ex)
            {
                if (ex.StatusCode == 401)
                    throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid token"));

                if (ex.StatusCode == 500)
                {
                    result = JsonConvert.DeserializeObject<SwiftCashoutFinalizeResponse>(ex.Response);
                    return result;
                }

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        public override async Task<EmptyResponse> CryptoCashout(CryptoCashoutRequest request, ServerCallContext context)
        {
            var result = new EmptyResponse();

            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV1Client.HotWalletCashoutAsync(_mapper.Map<HotWalletCashoutOperation>(request),
                    token, _walletApiConfig.Secret);

                if (response.Error != null)
                {
                    result.Error = _mapper.Map<ErrorV1>(response.Error);
                }

                return result;
            }
            catch (ApiExceptionV1 ex)
            {
                if (ex.StatusCode == 401)
                    throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid token"));

                if (ex.StatusCode == 500)
                {
                    result = JsonConvert.DeserializeObject<EmptyResponse>(ex.Response);
                    return result;
                }

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        public override async Task<AppSettingsResponse> GetAppSettings(Empty request, ServerCallContext context)
        {
            var result = new AppSettingsResponse();

            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV1Client.GetAppSettingsAsync(token);

                if (response.Result != null)
                {
                    result.Result = _mapper.Map<AppSettingsResponse.Types.AppSettingsData>(response.Result);

                    result.Result.FeeSettings.CashOut.AddRange(
                        _mapper.Map<CashOutFee[]>(response.Result.FeeSettings.CashOut?.ToArray() ??
                                                  Array.Empty<Lykke.ApiClients.V1.CashOutFee>()));
                }

                if (response.Error != null)
                {
                    result.Error = _mapper.Map<ErrorV1>(response.Error);
                }

                return result;
            }
            catch (ApiExceptionV1 ex)
            {
                if (ex.StatusCode == 401)
                    throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid token"));

                if (ex.StatusCode == 500)
                {
                    result = JsonConvert.DeserializeObject<AppSettingsResponse>(ex.Response);
                    return result;
                }

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        public override async Task<PrivateWalletsResponse> GetPrivateWallets(Empty request, ServerCallContext context)
        {
            var result = new PrivateWalletsResponse();

            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV1Client.PrivateWalletGetAsync(token);

                if (response.Result != null)
                {
                    result.Result = new PrivateWalletsResponse.Types.PrivateWalletsPayload();

                    foreach (var wallet in response.Result.Wallets)
                    {
                        var res = _mapper.Map<PrivateWallet>(wallet);
                        res.Balances.AddRange(_mapper.Map<List<BalanceRecord>>(wallet.Balances));
                        result.Result.Wallets.Add(res);
                    }
                }

                if (response.Error != null)
                {
                    result.Error = _mapper.Map<ErrorV1>(response.Error);
                }

                return result;
            }
            catch (ApiExceptionV1 ex)
            {
                if (ex.StatusCode == 401)
                    throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid token"));

                if (ex.StatusCode == 500)
                {
                    result = JsonConvert.DeserializeObject<PrivateWalletsResponse>(ex.Response);
                    return result;
                }

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        public override Task GetPriceUpdates(PriceUpdatesRequest request, IServerStreamWriter<PriceUpdate> responseStream, ServerCallContext context)
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

            return _priceStreamService.RegisterStream(streamInfo, prices);
        }

        public override Task GetCandleUpdates(CandleUpdatesRequest request, IServerStreamWriter<CandleUpdate> responseStream, ServerCallContext context)
        {
            Console.WriteLine($"New candles stream connect. peer:{context.Peer}");

            var streamInfo = new StreamInfo<CandleUpdate>
            {
                Stream = responseStream,
                Peer = context.Peer,
                Keys = new [] {$"{request.AssetPairId}_{request.Type}_{request.Interval}"},
                CancelationToken = context.CancellationToken
            };

            return _candlesStreamService.RegisterStream(streamInfo);
        }
    }
}

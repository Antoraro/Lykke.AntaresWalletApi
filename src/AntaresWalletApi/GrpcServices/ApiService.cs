using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using AntaresWalletApi.Common.Configuration;
using AntaresWalletApi.Common.Domain;
using AntaresWalletApi.Common.Domain.MyNoSqlEntities;
using AntaresWalletApi.Common.Domain.Services;
using AntaresWalletApi.Extensions;
using AntaresWalletApi.Services;
using AutoMapper;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Lykke.ApiClients.V1;
using Lykke.MatchingEngine.Connector.Abstractions.Services;
using Lykke.MatchingEngine.Connector.Models.Api;
using Lykke.Service.Assets.Client;
using Lykke.Service.Balances.Client;
using Lykke.Service.CandlesHistory.Client;
using Lykke.Service.CandlesHistory.Client.Models;
using MyNoSqlServer.Abstractions;
using Newtonsoft.Json;
using Swisschain.Lykke.AntaresWalletApi.ApiContract;
using Candle = Swisschain.Lykke.AntaresWalletApi.ApiContract.Candle;
using LimitOrderModel = Swisschain.Lykke.AntaresWalletApi.ApiContract.LimitOrderModel;
using Status = Grpc.Core.Status;
using UpgradeRequest = Swisschain.Lykke.AntaresWalletApi.ApiContract.UpgradeRequest;

namespace AntaresWalletApi.GrpcServices
{
    public class ApiService : Swisschain.Lykke.AntaresWalletApi.ApiContract.ApiService.ApiServiceBase
    {
        private readonly ILykkeWalletAPIv1Client _walletApiV1Client;
        private readonly IAssetsService _assetsService;
        private readonly AssetsHelper _assetsHelper;
        private readonly IMyNoSqlServerDataReader<PriceEntity> _pricesReader;
        private readonly IStreamService<PriceUpdate> _priceStreamService;
        private readonly IStreamService<CandleUpdate> _candlesStreamService;
        private readonly ICandleshistoryservice _candlesHistoryService;
        private readonly ValidationService _validationService;
        private readonly IMatchingEngineClient _matchingEngineClient;
        private readonly IBalancesClient _balancesClient;
        private readonly WalletApiConfig _walletApiConfig;
        private readonly IMapper _mapper;

        public ApiService(
            ILykkeWalletAPIv1Client walletApiV1Client,
            IAssetsService assetsService,
            AssetsHelper assetsHelper,
            IMyNoSqlServerDataReader<PriceEntity> pricesReader,
            IStreamService<PriceUpdate> priceStreamService,
            IStreamService<CandleUpdate> candlesStreamService,
            ICandleshistoryservice candlesHistoryService,
            ValidationService validationService,
            IMatchingEngineClient matchingEngineClient,
            IBalancesClient balancesClient,
            WalletApiConfig walletApiConfig,
            IMapper mapper
        )
        {
            _walletApiV1Client = walletApiV1Client;
            _assetsService = assetsService;
            _assetsHelper = assetsHelper;
            _pricesReader = pricesReader;
            _priceStreamService = priceStreamService;
            _candlesStreamService = candlesStreamService;
            _candlesHistoryService = candlesHistoryService;
            _validationService = validationService;
            _matchingEngineClient = matchingEngineClient;
            _balancesClient = balancesClient;
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

        public override async Task<PricesResponse> GetPrices(PricesRequest request, ServerCallContext context)
        {
            var entities = _pricesReader.Get(PriceEntity.GetPk());

            List<PriceUpdate> result = new List<PriceUpdate>();

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
            catch (ApiException ex)
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

                return _mapper.Map<PlaceOrderResponse>(response);
            }
            catch (ApiException ex)
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
            catch (ApiException ex)
            {
                if (ex.StatusCode == 401)
                    throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid token"));

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        public override async Task<CancelOrderResponse> CancelAllOrders(CancelOrdersRequest request, ServerCallContext context)
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

            var model = new LimitOrderMassCancelModel
            {
                Id = Guid.NewGuid().ToString(),
                AssetPairId = request.AssetPairId,
                ClientId = context.GetClientId(),
                IsBuy = isBuy
            };

            MeResponseModel response = await _matchingEngineClient.MassCancelLimitOrdersAsync(model);

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
            catch (ApiException ex)
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
            catch (ApiException ex)
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
            catch (ApiException ex)
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
            catch (ApiException ex)
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
            catch (ApiException ex)
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
            catch (ApiException ex)
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
            catch (ApiException ex)
            {
                if (ex.StatusCode == 401)
                    throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid token"));

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
            catch (ApiException ex)
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
            catch (ApiException ex)
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
            catch (ApiException ex)
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
            catch (ApiException ex)
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
            catch (ApiException ex)
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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AntaresWalletApi.Common.Configuration;
using AntaresWalletApi.Common.Domain;
using AntaresWalletApi.Common.Domain.MyNoSqlEntities;
using AntaresWalletApi.Extensions;
using AntaresWalletApi.Services;
using AutoMapper;
using Common;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Lykke.ApiClients.V1;
using Lykke.ApiClients.V2;
using Lykke.Common.Extensions;
using Lykke.MatchingEngine.Connector.Abstractions.Services;
using Lykke.MatchingEngine.Connector.Models.Api;
using Lykke.Service.Assets.Client;
using Lykke.Service.Balances.Client;
using Lykke.Service.CandlesHistory.Client;
using Lykke.Service.CandlesHistory.Client.Models;
using Lykke.Service.ClientAccount.Client;
using Lykke.Service.ClientAccount.Client.Models;
using Lykke.Service.RateCalculator.Client;
using Lykke.Service.Registration;
using Lykke.Service.Registration.Contract.Client.Enums;
using Lykke.Service.TradesAdapter.Client;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.StaticFiles;
using MyNoSqlServer.Abstractions;
using Newtonsoft.Json;
using Swisschain.Lykke.AntaresWalletApi.ApiContract;
using AnswersRequest = Swisschain.Lykke.AntaresWalletApi.ApiContract.AnswersRequest;
using ApiExceptionV1 = Lykke.ApiClients.V1.ApiException;
using ApiExceptionV2 = Lykke.ApiClients.V2.ApiException;
using Candle = Swisschain.Lykke.AntaresWalletApi.ApiContract.Candle;
using CashOutFee = Swisschain.Lykke.AntaresWalletApi.ApiContract.CashOutFee;
using Enum = System.Enum;
using LimitOrderModel = Swisschain.Lykke.AntaresWalletApi.ApiContract.LimitOrderModel;
using LimitOrderRequest = Swisschain.Lykke.AntaresWalletApi.ApiContract.LimitOrderRequest;
using MarketOrderRequest = Swisschain.Lykke.AntaresWalletApi.ApiContract.MarketOrderRequest;
using QuestionnaireResponse = Swisschain.Lykke.AntaresWalletApi.ApiContract.QuestionnaireResponse;
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
        private readonly OrderbookStreamService _orderbookStreamService;
        private readonly PublicTradesStreamService _publicTradesStreamService;
        private readonly ICandleshistoryservice _candlesHistoryService;
        private readonly ValidationService _validationService;
        private readonly OrderbooksService _orderbooksService;
        private readonly SessionService _sessionService;
        private readonly IMatchingEngineClient _matchingEngineClient;
        private readonly IBalancesClient _balancesClient;
        private readonly IClientAccountClient _clientAccountClient;
        private readonly IRateCalculatorClient _rateCalculatorClient;
        private readonly ITradesAdapterClient _tradesAdapterClient;
        private readonly IRegistrationServiceClient _registrationServiceClient;
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
            OrderbookStreamService orderbookStreamService,
            PublicTradesStreamService publicTradesStreamService,
            ICandleshistoryservice candlesHistoryService,
            ValidationService validationService,
            OrderbooksService orderbooksService,
            SessionService sessionService,
            IMatchingEngineClient matchingEngineClient,
            IBalancesClient balancesClient,
            IClientAccountClient clientAccountClient,
            IRateCalculatorClient rateCalculatorClient,
            ITradesAdapterClient tradesAdapterClient,
            IRegistrationServiceClient registrationServiceClient,
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
            _orderbookStreamService = orderbookStreamService;
            _publicTradesStreamService = publicTradesStreamService;
            _candlesHistoryService = candlesHistoryService;
            _validationService = validationService;
            _orderbooksService = orderbooksService;
            _sessionService = sessionService;
            _matchingEngineClient = matchingEngineClient;
            _balancesClient = balancesClient;
            _clientAccountClient = clientAccountClient;
            _rateCalculatorClient = rateCalculatorClient;
            _tradesAdapterClient = tradesAdapterClient;
            _registrationServiceClient = registrationServiceClient;
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

            var popularAssetPairs = await _assetsHelper.GetPopularPairsAsync(assets.Select(x => x.Id).ToList());

            foreach (var asset in result.Assets)
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

        public override async Task<PushSettingsResponse> GetPushSettings(Empty request, ServerCallContext context)
        {
            var result = new PushSettingsResponse();

            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV1Client.PushSettingsGetAsync(token);

                if (response.Result != null)
                {
                    result.Result = new PushSettingsResponse.Types.PushSettingsPayload
                    {
                        Enabled = response.Result.Enabled
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

                if (ex.StatusCode == 500)
                {
                    result = JsonConvert.DeserializeObject<PushSettingsResponse>(ex.Response);
                    return result;
                }

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        public override async Task<EmptyResponse> SetPushSettings(PushSettingsRequest request, ServerCallContext context)
        {
            var result = new EmptyResponse();

            try
            {
                var token = context.GetBearerToken();

                var response = await _walletApiV1Client.PushSettingsPostAsync(new PushNotificationsSettingsModel{Enabled = request.Enabled}, token);

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

        public override async Task<RegisterPushResponse> RegisterPushNotifications(RegisterPushRequest request, ServerCallContext context)
        {
            var result = new RegisterPushResponse();

            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV2Client.RegisterInstallationAsync(new PushRegistrationModel
                {
                    InstallationId = Guid.NewGuid().ToString(),
                    Platform = request.Platform == MobileOsPlatform.Ios ? PushRegistrationModelPlatform.Ios : PushRegistrationModelPlatform.Android,
                    PushChannel = request.PushChannel
                }, token);

                if (response != null)
                {
                    result.Result = new RegisterPushResponse.Types.InstallationPayload
                    {
                        InstallationId = response.InstallationId
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

        [AllowAnonymous]
        public override async Task<VerificationEmailResponse> SendVerificationEmail(VerificationEmailRequest request, ServerCallContext context)
        {
            var result = new VerificationEmailResponse();

            try
            {
                var response = await _walletApiV1Client.SendVerificationEmailAsync(new PostEmailModel{Email = request.Email});

                if (response.Result != null)
                {
                    result.Result = new VerificationEmailResponse.Types.VerificationEmailPayload { Token = response.Result.Token };
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
                    result = JsonConvert.DeserializeObject<VerificationEmailResponse>(ex.Response);
                    return result;
                }

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        [AllowAnonymous]
        public override async Task<EmptyResponse> SendVerificationSms(VerificationSmsRequest request, ServerCallContext context)
        {
            var result = new EmptyResponse();

            try
            {
                var response = await _walletApiV1Client.SendVerificationSmsAsync(new PostPhoneModel{PhoneNumber = request.Phone, Token = request.Token});

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

        [AllowAnonymous]
        public override async Task<VerifyResponse> VerifyEmail(VerifyEmailRequest request, ServerCallContext context)
        {
            var result = new VerifyResponse();

            try
            {
                var response = await _walletApiV1Client.VerifyEmailAsync(new VerifyEmailRequestModel
                {
                    Email = request.Email,
                    Code = request.Code,
                    Token = request.Token
                });

                if (response.Result != null)
                {
                    result.Result = new VerifyResponse.Types.VerifyPayload{ Passed = response.Result.Passed};
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
                    result = JsonConvert.DeserializeObject<VerifyResponse>(ex.Response);
                    return result;
                }

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        [AllowAnonymous]
        public override async Task<VerifyResponse> VerifyPhone(VerifyPhoneRequest request, ServerCallContext context)
        {
            var result = new VerifyResponse();

            try
            {
                var response = await _walletApiV1Client.VerifyPhoneAsync(new VerifyPhoneModel
                {
                    PhoneNumber = request.Phone,
                    Code = request.Code,
                    Token = request.Token
                });

                if (response.Result != null)
                {
                    result.Result = new VerifyResponse.Types.VerifyPayload{ Passed = response.Result.Passed};
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
                    result = JsonConvert.DeserializeObject<VerifyResponse>(ex.Response);
                    return result;
                }

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        [AllowAnonymous]
        public override async Task<RegisterResponse> Register(RegisterRequest request, ServerCallContext context)
        {
            var result = new RegisterResponse();

            try
            {
                var response = await _walletApiV1Client.RegisterAsync(new RegistrationModel
                {
                    FullName = request.FullName,
                    Email = request.Email,
                    Phone = request.Phone,
                    Password = request.Password,
                    Hint = request.Hint,
                    CountryIso3Poa = request.CountryIso3Code,
                    Pin = request.Pin,
                    Token = request.Token
                });

                if (response.Result != null)
                {
                    result.Result = _mapper.Map<RegisterResponse.Types.RegisterPayload>(response.Result);
                    string sessionId = await _sessionService.CreateVerifiedSessionAsync(response.Result.Token, request.PublicKey);
                    result.Result.SessionId = sessionId;
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
                    result = JsonConvert.DeserializeObject<RegisterResponse>(ex.Response);
                    return result;
                }

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        [AllowAnonymous]
        public override async Task<LoginResponse> Login(LoginRequest request, ServerCallContext context)
        {
            var validateResult = ValidateLoginRequest(request);

            if (validateResult != null)
                return validateResult;

            var result = new LoginResponse();

            try
            {
                var response = await _registrationServiceClient.LoginApi.AuthenticateAsync(new Lykke.Service.Registration.Contract.Client.Models.AuthenticateModel
                {
                    Email = request.Email,
                    Password = request.Password,
                    Ip = context.GetHttpContext().GetIp(),
                    UserAgent = context.GetHttpContext().GetUserAgent()
                });

                if (response.Status == AuthenticationStatus.Error)
                {
                    result.Error = new ErrorV1
                    {
                        Code = "2",
                        Message = response.ErrorMessage
                    };

                    return result;
                }

                string sessionId = await _sessionService.CreateSessionAsync(response.Token, request.PublicKey);

                result.Result = new LoginResponse.Types.LoginPayload
                {
                    SessionId = sessionId,
                    NotificationId = response.NotificationsId
                };

                return result;
            }
            catch (Exception ex)
            {
                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        [AllowAnonymous]
        public override async Task<EmptyResponse> SendLoginSms(LoginSmsRequest request, ServerCallContext context)
        {
            var result = new EmptyResponse();

            var session = _sessionService.GetSession(request.SessionId);

            if (session == null)
            {
                result.Error = new ErrorV1
                {
                    Code = ErrorModelCode.InvalidInputField.ToString(),
                    Message = ErrorMessages.InvalidFieldValue(nameof(request.SessionId)),
                    Field = nameof(request.SessionId)
                };

                return result;
            }

            try
            {
                var response = await _walletApiV1Client.RequestCodesAsync($"Bearer {session.Token}");

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

        [AllowAnonymous]
        public override async Task<VerifyLoginSmsResponse> VerifyLoginSms(VerifyLoginSmsRequest request, ServerCallContext context)
        {
            var result = new VerifyLoginSmsResponse();

            var session = _sessionService.GetSession(request.SessionId);

            if (session == null)
            {
                result.Error = new ErrorV1
                {
                    Code = ErrorModelCode.InvalidInputField.ToString(),
                    Message = ErrorMessages.InvalidFieldValue(nameof(request.SessionId)),
                    Field = nameof(request.SessionId)
                };

                return result;
            }

            try
            {
                var response = await _walletApiV1Client.SubmitCodeAsync(new SubmitCodeModel
                {
                    Code = request.Code
                }, $"Bearer {session.Token}");

                if (response.Result != null)
                {
                    result.Result = new VerifyLoginSmsResponse.Types.VerifyLoginSmsPayload{Passed = true};
                    session.Sms = true;

                    if (session.Pin)
                        session.Verified = true;

                    await _sessionService.SaveSessionAsync(session);
                }

                if (response.Error != null)
                {
                    var error = _mapper.Map<ErrorV1>(response.Error);

                    if (error.Code == "WrongConfirmationCode")
                    {
                        result.Result = new VerifyLoginSmsResponse.Types.VerifyLoginSmsPayload{Passed = false};
                        return result;
                    }

                    result.Error = error;
                }

                return result;
            }
            catch (ApiExceptionV1 ex)
            {
                if (ex.StatusCode == 401)
                    throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid token"));

                if (ex.StatusCode == 500)
                {
                    result = JsonConvert.DeserializeObject<VerifyLoginSmsResponse>(ex.Response);
                    return result;
                }

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        [AllowAnonymous]
        public override async Task<CheckPinResponse> CheckPin(CheckPinRequest request, ServerCallContext context)
        {
            var result = new CheckPinResponse();

            var session = _sessionService.GetSession(request.SessionId);

            if (session == null)
            {
                result.Error = new ErrorV1
                {
                    Code = ErrorModelCode.InvalidInputField.ToString(),
                    Message = ErrorMessages.InvalidFieldValue(nameof(request.SessionId)),
                    Field = nameof(request.SessionId)
                };

                return result;
            }

            try
            {
                var response = await _walletApiV1Client.PinSecurityCheckPinCodePostAsync(new PinSecurityCheckRequestModel
                {
                    Pin = request.Pin
                }, $"Bearer {session.Token}");

                if (response.Result != null)
                {
                    result.Result = new CheckPinResponse.Types.CheckPinPayload{Passed = response.Result.Passed};

                    if (result.Result.Passed)
                    {
                        if (session.Verified)
                        {
                            await _sessionService.ProlongateSessionAsync(session);
                        }
                        else
                        {
                            session.Pin = true;

                            if (session.Sms)
                                session.Verified = true;

                            await _sessionService.SaveSessionAsync(session);
                        }
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
                    result = JsonConvert.DeserializeObject<CheckPinResponse>(ex.Response);
                    return result;
                }

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        [AllowAnonymous]
        public override Task<CheckSessionResponse> IsSessionExpired(CheckSessionRequest request, ServerCallContext context)
        {
            var result = new CheckSessionResponse();

            var session = _sessionService.GetSession(request.SessionId);

            result.Expired = session == null || session.ExpirationDate < DateTime.UtcNow;

            return Task.FromResult(result);
        }

        public override async Task<EmptyResponse> ProlongateSession(Empty request, ServerCallContext context)
        {
            var result = new EmptyResponse();

            string sessionId = context.GetToken();

            var session = _sessionService.GetSession(sessionId);

            if (session == null)
            {
                result.Error = new ErrorV1
                {
                    Code = ErrorModelCode.InvalidInputField.ToString(),
                    Message = ErrorMessages.InvalidFieldValue(nameof(sessionId)),
                    Field = nameof(sessionId)
                };

                return result;
            }

            await _sessionService.ProlongateSessionAsync(session);

            return result;
        }

        public override async Task<EmptyResponse> Logout(Empty request, ServerCallContext context)
        {
            var result = new EmptyResponse();

            string sessionId = context.GetToken();

            var session = _sessionService.GetSession(sessionId);

            if (session == null)
            {
                result.Error = new ErrorV1
                {
                    Code = ErrorModelCode.InvalidInputField.ToString(),
                    Message = ErrorMessages.InvalidFieldValue(nameof(sessionId)),
                    Field = nameof(sessionId)
                };

                return result;
            }

            await _sessionService.LogoutAsync(session);

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

        public override async Task<AssetTradesResponse> GetAssetTrades(AssetTradesRequest request, ServerCallContext context)
        {
            var result = new AssetTradesResponse();

            try
            {
                var token = context.GetBearerToken();
                var wallets = await _clientAccountClient.Wallets.GetClientWalletsFilteredAsync(context.GetClientId(), WalletType.Trading);

                var walletId = wallets.FirstOrDefault()?.Id;

                var response = await _walletApiV2Client.GetByWalletIdAsync(
                    walletId, new List<string>{}, request.AssetId, null, request.Take, request.Skip,
                    token);

                if (response != null)
                {
                    result.Trades.AddRange(_mapper.Map<List<AssetTradesResponse.Types.AssetTradeModel>>(response));
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

        public override async Task<FundsResponse> GetFunds(FundsRequest request, ServerCallContext context)
        {
            var result = new FundsResponse();

            try
            {
                var token = context.GetBearerToken();
                var wallets = await _clientAccountClient.Wallets.GetClientWalletsFilteredAsync(context.GetClientId(), WalletType.Trading);

                var walletId = wallets.FirstOrDefault()?.Id;

                var response = await _walletApiV2Client.GetFundsByWalletIdAsync(
                    walletId, null, request.AssetId, request.Take, request.Skip,
                    request.OptionalFromDateCase == FundsRequest.OptionalFromDateOneofCase.None ? (DateTimeOffset?) null : request.From.ToDateTimeOffset(),
                    request.OptionalToDateCase == FundsRequest.OptionalToDateOneofCase.None ? (DateTimeOffset?) null : request.To.ToDateTimeOffset(),
                    token);

                if (response != null)
                {
                    result.Funds.AddRange(_mapper.Map<List<FundsResponse.Types.FundsModel>>(response));
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

        public override async Task<ExplorerLinksResponse> GetExplorerLinks(ExplorerLinksRequest request, ServerCallContext context)
        {
            var result = new ExplorerLinksResponse();

            try
            {
                var token = context.GetBearerToken();
                var wallets = await _clientAccountClient.Wallets.GetClientWalletsFilteredAsync(context.GetClientId(), WalletType.Trading);

                var walletId = wallets.FirstOrDefault()?.Id;

                var response = await _walletApiV2Client.GetExplorerLinksAsync(request.AssetId, request.TransactionHash,
                    token);

                if (response != null)
                {
                    result.Links.AddRange(_mapper.Map<List<ExplorerLinksResponse.Types.ExplorerLinkModel>>(response.Links));
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

        public override async Task<PublicTradesResponse> GetPublicTrades(PublicTradesRequest request, ServerCallContext context)
        {
            var result = new PublicTradesResponse();

            if (string.IsNullOrEmpty(request.AssetPairId))
            {
                result.Error = new ErrorV1
                {
                    Code = ErrorModelCode.InvalidInputField.ToString(),
                    Field = nameof(request.AssetPairId),
                    Message = $"{nameof(request.AssetPairId)} can't be empty"
                };
                return result;
            }

            try
            {
                var response = await _tradesAdapterClient.GetTradesByAssetPairIdAsync(request.AssetPairId, request.Skip, request.Take);

                if (response.Records != null)
                {
                    result.Result.AddRange(_mapper.Map<List<PublicTrade>>(response.Records));
                }

                if (response.Error != null)
                {
                    result.Error = new ErrorV1{Message = response.Error.Message};
                }

                return result;
            }
            catch (Exception ex)
            {
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

                    result.Result.NextTier?.Documents.AddRange(response.Result.NextTier?.Documents);
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

        public override async Task<PersonalDataResponse> GetPersonalData(Empty request, ServerCallContext context)
        {
            var result = new PersonalDataResponse();

            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV1Client.GetPersonalDataAsync(token);

                if (response.Result != null)
                {
                    result.Result = _mapper.Map<PersonalData>(response.Result);
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
                    result = JsonConvert.DeserializeObject<PersonalDataResponse>(ex.Response);
                    return result;
                }

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        public override async Task<KycDocumentsResponse> GetKycDocuments(Empty request, ServerCallContext context)
        {
            var result = new KycDocumentsResponse();

            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV1Client.GetKycProfilesDocumentsByProfileTypeAsync("LykkeEurope", token);

                if (response.Result != null)
                {
                    foreach (var item in response.Result)
                    {
                        var document = _mapper.Map<KycDocumentsResponse.Types.KycDocument>(item.Value);
                        if (item.Value.Files.Any())
                        {
                            document.Files.AddRange(_mapper.Map<List<KycDocumentsResponse.Types.KycFile>>(item.Value.Files));
                        }

                        result.Result.Add(item.Key, document);
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
                    result = JsonConvert.DeserializeObject<KycDocumentsResponse>(ex.Response);
                    return result;
                }

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        public override async Task<EmptyResponseV2> SetAddress(SetAddressRequest request, ServerCallContext context)
        {
            var result = new EmptyResponseV2();

            try
            {
                var token = context.GetBearerToken();
                await _walletApiV2Client.UpdateAddressAsync(new AddressModel{Address = request.Address}, token);
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

        public override async Task<EmptyResponseV2> SetZip(SetZipRequest request, ServerCallContext context)
        {
            var result = new EmptyResponseV2();

            try
            {
                var token = context.GetBearerToken();
                await _walletApiV2Client.UpdateZipCodeAsync(new ZipCodeModel{Zip = request.Zip}, token);
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

        public override async Task<EmptyResponse> UploadKycFile(KycFileRequest request, ServerCallContext context)
        {
            var result = new EmptyResponse();

            try
            {
                var token = context.GetBearerToken();

                if (request.File.IsEmpty)
                    return result;

                var provider = new FileExtensionContentTypeProvider();
                if(!provider.TryGetContentType(request.Filename, out var contentType))
                {
                    contentType = "image/jpeg";
                }

                using (var ms = new MemoryStream(request.File.ToByteArray()))
                {
                    await _walletApiV1Client.KycFilesUploadFileAsync(request.DocumentType, string.Empty,
                        new FileParameter(ms, request.Filename, contentType), token);

                    return result;
                }
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

        public override async Task<QuestionnaireResponse> GetQuestionnaire(Empty request, ServerCallContext context)
        {
            var result = new QuestionnaireResponse();

            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV1Client.TiersGetQuestionnaireAsync(token);

                if (response.Result != null)
                {
                    result.Result = new QuestionnaireResponse.Types.QuestionnairePayload();

                    foreach (var question in response.Result.Questionnaire)
                    {
                        var q = _mapper.Map<QuestionnaireResponse.Types.Question>(question);
                        q.Answers.AddRange(_mapper.Map<List<QuestionnaireResponse.Types.Answer>>(question.Answers));
                        result.Result.Questionnaire.Add(q);
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
                    result = JsonConvert.DeserializeObject<QuestionnaireResponse>(ex.Response);
                    return result;
                }

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        public override async Task<EmptyResponse> SaveQuestionnaire(AnswersRequest request, ServerCallContext context)
        {
            var result = new EmptyResponse();

            try
            {
                var token = context.GetBearerToken();

                var req = new Lykke.ApiClients.V1.AnswersRequest
                {
                    Answers = _mapper.Map<List<ChoiceModel>>(request.Answers.ToList())
                };

                var response = await _walletApiV1Client.TiersSaveQuestionnaireAsync(req, token);

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

        public override async Task<EmptyResponse> SubmitProfile(SubmitProfileRequest request, ServerCallContext context)
        {
            var result = new EmptyResponse();

            try
            {
                var token = context.GetBearerToken();
                Tier? tier = request.OptionalTierCase == SubmitProfileRequest.OptionalTierOneofCase.None
                    ? (Tier?)null
                    : request.Tier == TierUpgrade.Advanced ? Tier.Advanced : Tier.ProIndividual;

                var response = await _walletApiV1Client.KycProfilesSubmitAsync("LykkeEurope", tier, token);

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

        [AllowAnonymous]
        public override async Task<CountryPhoneCodesResponse> GetCountryPhoneCodes(Empty request, ServerCallContext context)
        {
            var result = new CountryPhoneCodesResponse();

            try
            {
                var response = await _walletApiV1Client.GetCountryPhoneCodesAsync();

                if (response.Result != null)
                {
                    result.Result = new CountryPhoneCodesResponse.Types.CountryPhoneCodes
                    {
                        Current = response.Result.Current
                    };
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

        public override async Task<CheckCryptoAddressResponse> IsCryptoAddressValid(CheckCryptoAddressRequest request, ServerCallContext context)
        {
            var result = new CheckCryptoAddressResponse();

            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV1Client.HotWalletAddressesValidityAsync(request.AddressExtension,
                    request.Address, request.AssetId, token);

                if (response.Result != null)
                {
                    result.Result = new CheckCryptoAddressResponse.Types.CheckCryptoAddressPayload
                    {
                        IsValid = response.Result.IsValid
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

                if (ex.StatusCode == 500)
                {
                    result = JsonConvert.DeserializeObject<CheckCryptoAddressResponse>(ex.Response);
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

        public override async Task<SwiftCashoutResponse> SwiftCashout(SwiftCashoutRequest request, ServerCallContext context)
        {
            var result = new SwiftCashoutResponse();

            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV1Client.OffchainCashoutSwiftAsync(_mapper.Map<OffchainCashoutSwiftModel>(request), token);

                if (response.Result != null)
                {
                    var finalizeResponse =
                        await _walletApiV1Client.OffchainFinalizeAsync(new OffchainFinalizeModel
                        {
                            TransferId = response.Result.TransferId,
                            ClientRevokePubKey = response.Result.TransferId,
                            ClientRevokeEncryptedPrivateKey = response.Result.TransferId,
                            SignedTransferTransaction = response.Result.TransferId
                        }, token);

                    if (finalizeResponse.Result != null)
                    {
                        result.Result = new SwiftCashoutResponse.Types.SwiftCashoutData
                        {
                            TransferId = finalizeResponse.Result.TransferId
                        };
                    }

                    if (response.Error != null)
                    {
                        result.Error = _mapper.Map<ErrorV1>(response.Error);
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
                    result = JsonConvert.DeserializeObject<SwiftCashoutResponse>(ex.Response);
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

        public override async Task<AssetDisclaimersResponse> GetAssetDisclaimers(Empty request, ServerCallContext context)
        {
            var result = new AssetDisclaimersResponse();

            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV1Client.GetAssetDisclaimersAsync(token);

                if (response.Result != null)
                {
                    result.Result = new AssetDisclaimersResponse.Types.AssetDisclaimersPayload();

                    foreach (var disclaimer in response.Result.Disclaimers)
                    {
                        var res = new AssetDisclaimer{Id = disclaimer.Id, Text = disclaimer.Text};
                        result.Result.Disclaimers.Add(res);
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
                    result = JsonConvert.DeserializeObject<AssetDisclaimersResponse>(ex.Response);
                    return result;
                }

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        public override async Task<EmptyResponse> ApproveAssetDisclaimer(AssetDisclaimerRequest request, ServerCallContext context)
        {
            var result = new EmptyResponse();

            try
            {
                var token = context.GetBearerToken();

                var response = await _walletApiV1Client.ApproveAssetDisclaimerAsync(request.DisclaimerId, token);

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

        public override async Task<EmptyResponse> DeclineAssetDisclaimer(AssetDisclaimerRequest request, ServerCallContext context)
        {
            var result = new EmptyResponse();

            try
            {
                var token = context.GetBearerToken();

                var response = await _walletApiV1Client.DeclineAssetDisclaimerAsync(request.DisclaimerId, token);

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

        private LoginResponse ValidateLoginRequest(LoginRequest request)
        {
            if (string.IsNullOrEmpty(request.Email))
                return new LoginResponse
                {
                    Error = new ErrorV1
                    {
                        Code = ErrorModelCode.InvalidInputField.ToString(),
                        Message = ErrorMessages.CantBeEmpty(nameof(request.Email)),
                        Field = nameof(request.Email)
                    }
                };

            if (!request.Email.IsValidEmailAndRowKey())
                return new LoginResponse
                {
                    Error = new ErrorV1
                    {
                        Code = ErrorModelCode.InvalidInputField.ToString(),
                        Message = ErrorMessages.InvalidFieldValue(nameof(request.Email)),
                        Field = nameof(request.Email)
                    }
                };

            if (string.IsNullOrEmpty(request.Password))
                return new LoginResponse
                {
                    Error = new ErrorV1
                    {
                        Code = ErrorModelCode.InvalidInputField.ToString(),
                        Message = ErrorMessages.CantBeEmpty(nameof(request.Password)),
                        Field = nameof(request.Password)
                    }
                };

            return null;
        }
    }
}

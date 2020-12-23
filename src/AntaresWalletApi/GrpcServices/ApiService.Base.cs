using AntaresWalletApi.Common.Configuration;
using AntaresWalletApi.Common.Domain.MyNoSqlEntities;
using AntaresWalletApi.Services;
using AutoMapper;
using Lykke.ApiClients.V1;
using Lykke.ApiClients.V2;
using Lykke.MatchingEngine.Connector.Abstractions.Services;
using Lykke.Service.Assets.Client;
using Lykke.Service.Balances.Client;
using Lykke.Service.CandlesHistory.Client;
using Lykke.Service.ClientAccount.Client;
using Lykke.Service.RateCalculator.Client;
using Lykke.Service.Registration;
using Lykke.Service.TradesAdapter.Client;
using MyNoSqlServer.Abstractions;

namespace AntaresWalletApi.GrpcServices
{
    public partial class ApiService : Swisschain.Lykke.AntaresWalletApi.ApiContract.ApiService.ApiServiceBase
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
        private readonly AppConfig _config;
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
            AppConfig config,
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
            _config = config;
            _mapper = mapper;
        }
    }
}

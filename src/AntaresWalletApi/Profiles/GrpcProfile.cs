using System;
using System.Globalization;
using AntaresWalletApi.Common.Domain.MyNoSqlEntities;
using AutoMapper;
using Google.Protobuf.WellKnownTypes;
using Lykke.ApiClients.V1;
using Lykke.ApiClients.V2;
using Lykke.Service.CandlesHistory.Client.Models;
using Swisschain.Lykke.AntaresWalletApi.ApiContract;
using AnswersRequest = Swisschain.Lykke.AntaresWalletApi.ApiContract.AnswersRequest;
using Candle = Swisschain.Lykke.AntaresWalletApi.ApiContract.Candle;
using ClientBalanceResponseModel = Lykke.Service.Balances.AutorestClient.Models.ClientBalanceResponseModel;
using CountryItem = Lykke.ApiClients.V1.CountryItem;
using QuestionnaireResponse = Swisschain.Lykke.AntaresWalletApi.ApiContract.QuestionnaireResponse;
using UpgradeRequest = Swisschain.Lykke.AntaresWalletApi.ApiContract.UpgradeRequest;

namespace AntaresWalletApi.Profiles
{
    public class GrpcProfile : Profile
    {
        public GrpcProfile()
        {
            CreateMap<DateTime, string>().ConvertUsing(dt => dt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
            CreateMap<DateTime?, string>().ConvertUsing(dt => dt.HasValue ? dt.Value.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") : string.Empty);
            CreateMap<decimal, string>().ConvertUsing(d => d.ToString("0." + new string('#', 339), CultureInfo.InvariantCulture));
            CreateMap<decimal?, string>().ConvertUsing(d => d.HasValue ? d.Value.ToString("0." + new string('#', 339), CultureInfo.InvariantCulture) : string.Empty);
            CreateMap<double, string>().ConvertUsing(d => d.ToString("0." + new string('#', 339), CultureInfo.InvariantCulture));
            CreateMap<double?, string>().ConvertUsing(d => d.HasValue ? d.Value.ToString("0." + new string('#', 339), CultureInfo.InvariantCulture) : string.Empty);
            CreateMap<string, string>().ConvertUsing(d => d ?? string.Empty);
            CreateMap<string, double>().ConvertUsing(d => double.Parse(d, NumberStyles.Any, CultureInfo.InvariantCulture));
            CreateMap<DateTime, Timestamp>().ConvertUsing((dt, timestamp) =>
            {
                var date = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                return Timestamp.FromDateTime(date);
            });
            CreateMap<DateTimeOffset, Timestamp>().ConvertUsing((dt, timestamp) => Timestamp.FromDateTime(dt.UtcDateTime));
            CreateMap<string, Timestamp>().ConvertUsing((str, timestamp) =>
            {
                var date = DateTime.SpecifyKind(DateTime.Parse(str, CultureInfo.InvariantCulture), DateTimeKind.Utc);
                return Timestamp.FromDateTime(date);
            });

            CreateMap<Lykke.Service.Assets.Client.Models.Asset, Asset>(MemberList.Destination)
                .ForMember(d => d.CardDeposit, o => o.MapFrom(x => x.BankCardsDepositEnabled))
                .ForMember(d => d.SwiftDeposit, o => o.MapFrom(x => x.SwiftDepositEnabled))
                .ForMember(d => d.BlockchainDeposit, o => o.MapFrom(x => x.BlockchainDepositEnabled))
                .ForMember(d => d.CanBeBase, o => o.MapFrom(x => x.IsBase))
                .ForMember(d => d.PopularPairs, o => o.Ignore());

            CreateMap<Lykke.Service.Assets.Client.Models.AssetCategory, AssetCategory>(MemberList.Destination)
                .ForMember(d => d.IconUrl, o => o.MapFrom(x => x.AndroidIconUrl));

            CreateMap<PriceEntity, PriceUpdate>(MemberList.Destination)
                .ForMember(d => d.Timestamp, o => o.MapFrom(x => x.UpdatedDt))
                .ForMember(d => d.VolumeBase24H, o => o.Ignore())
                .ForMember(d => d.VolumeQuote24H, o => o.Ignore())
                .ForMember(d => d.PriceChange24H, o => o.Ignore());

            CreateMap<CandlePriceType, CandleType>();
            CreateMap<CandleTimeInterval, CandleInterval>();

            CreateMap<Lykke.Job.CandlesProducer.Contract.CandlePriceType, CandleType>();
            CreateMap<Lykke.Job.CandlesProducer.Contract.CandleTimeInterval, CandleInterval>();

            CreateMap<Lykke.Service.CandlesHistory.Client.Models.Candle, Candle>()
                .ForMember(d => d.Timestamp, o => o.MapFrom(x => x.DateTime))
                .ForMember(d => d.Volume, o => o.MapFrom(x => x.TradingVolume))
                .ForMember(d => d.OppositeVolume, o => o.MapFrom(x => x.TradingOppositeVolume))
                .ForMember(d => d.LastPrice, o => o.MapFrom(x => x.LastTradePrice));

            CreateMap<CandleEntity, CandleUpdate>()
                .ForMember(d => d.Timestamp, o => o.MapFrom(x => x.CandleTimestamp))
                .ForMember(d => d.UpdateTimestamp, o => o.MapFrom(x => x.UpdatedAt))
                .ForMember(d => d.Volume, o => o.MapFrom(x => x.TradingVolume))
                .ForMember(d => d.OppositeVolume, o => o.MapFrom(x => x.TradingOppositeVolume))
                .ForMember(d => d.LastPrice, o => o.MapFrom(x => x.LastTradePrice));

            CreateMap<OrderbookEntity, Orderbook>(MemberList.Destination)
                .ForMember(d => d.Timestamp, o => o.MapFrom(x => x.CreatedAt));

            CreateMap<Lykke.Service.TradesAdapter.Contract.Trade, PublicTrade>(MemberList.Destination)
                .ForMember(d => d.Side, o => o.MapFrom(x => x.Action));

            CreateMap<VolumePriceEntity, Orderbook.Types.PriceVolume>(MemberList.Destination)
                .ForMember(d => d.V, o => o.MapFrom(x => x.Volume))
                .ForMember(d => d.P, o => o.MapFrom(x => x.Price));

            CreateMap<ClientBalanceResponseModel, Balance>()
                .ForMember(d => d.Timestamp, o => o.MapFrom(x => x.UpdatedAt))
                .ForMember(d => d.Available, o => o.MapFrom(x => x.Balance))
                .ForMember(d => d.Reserved, o => o.MapFrom(x => x.Reserved))
                .ForMember(d => d.AssetId, o => o.MapFrom(x => x.AssetId));

            CreateMap<ApiHotWalletOrder, OrderModel>();

            CreateMap<ApiOffchainOrder, LimitOrderModel>();
            CreateMap<WatchList, Watchlist>();

            CreateMap<CurrentTierInfo, CurrentTier>();
            CreateMap<TierInfo, NextTier>();
            CreateMap<Lykke.ApiClients.V1.UpgradeRequest, UpgradeRequest>();

            CreateMap<GetSwiftCredentialsModel, SwiftCredentialsResponse.Types.Body>();
            CreateMap<BankCardPaymentUrlInputModel, BankCardPaymentDetailsResponse.Types.Body>();
            CreateMap<BankCardPaymentUrlResponceModel, BankCardPaymentUrlResponse.Types.Body>();
            CreateMap<BankCardPaymentUrlRequest, BankCardPaymentUrlInputModel>()
                .ForMember(d => d.WalletId, o => o.Ignore())
                .ForMember(d => d.OkUrl, o => o.Ignore())
                .ForMember(d => d.FailUrl, o => o.Ignore())
                .ForMember(d => d.FirstName, o => o.Ignore())
                .ForMember(d => d.LastName, o => o.Ignore())
                .ForMember(d => d.City, o => o.Ignore())
                .ForMember(d => d.Zip, o => o.Ignore())
                .ForMember(d => d.Address, o => o.Ignore())
                .ForMember(d => d.Country, o => o.Ignore())
                .ForMember(d => d.Email, o => o.Ignore())
                .ForMember(d => d.Phone, o => o.Ignore())
                .ForMember(d => d.DepositOption, o => o.MapFrom(x => "BankCard"));

            CreateMap<CountryItem, Country>();
            CreateMap<EthereumAssetResponse, EthereumSettingsResponse.Types.Body>();
            CreateMap<BitcoinFeeSettings, EthereumSettingsResponse.Types.BitcoinFee>();

            CreateMap<WithdrawalCryptoInfoModel, WithdrawalCryptoInfoResponse.Types.Body>();
            CreateMap<CashoutSwiftLastDataResponse, SwiftCashoutInfoResponse.Types.Body>();
            CreateMap<CashoutSwiftFeeResponse, SwiftCashoutFeeResponse.Types.Body>();
            CreateMap<SwiftCashoutRequest, OffchainCashoutSwiftModel>()
                .ForMember(d => d.PrevTempPrivateKey, o => o.Ignore());
            CreateMap<OffchainTradeRespModel, SwiftCashoutResponse.Types.Body>();

            CreateMap<ApiAppSettingsModel, AppSettingsResponse.Types.Body>();
            CreateMap<ApiAssetModel, AppSettingsResponse.Types.ApiAsset>();
            CreateMap<ApiRefundSettings, AppSettingsResponse.Types.ApiRefundSettings>();
            CreateMap<ApiFeeSettings, AppSettingsResponse.Types.ApiFeeSettings>();
            CreateMap<Lykke.ApiClients.V1.CashOutFee, Swisschain.Lykke.AntaresWalletApi.ApiContract.CashOutFee>();
            CreateMap<ApiWalletAssetModel, WalletResponse.Types.Body>();
            CreateMap<ApiPrivateWallet, PrivateWallet>();
            CreateMap<ApiBalanceRecord, BalanceRecord>();
            CreateMap<CryptoCashoutRequest, HotWalletCashoutOperation>();
            CreateMap<AssetPairModel, AssetPair>();

            CreateMap<TradeResponseModel, TradesResponse.Types.TradeModel>();
            CreateMap<MarketSlice, MarketsResponse.Types.MarketModel>();
            CreateMap<PendingActionsModel, PendingActionsResponse.Types.Body>();
            CreateMap<Lykke.ApiClients.V1.ApiPersonalDataModel, PersonalData>();
            CreateMap<DocumentModel, KycDocument>();
            CreateMap<FileModel, KycFile>();
            CreateMap<QuestionModel, QuestionnaireResponse.Types.Question>();
            CreateMap<AnswerModel, QuestionnaireResponse.Types.Answer>();
            CreateMap<AnswersRequest.Types.Choice, ChoiceModel>();
            CreateMap<FundsResponseModel, FundsResponse.Types.FundsModel>();
            CreateMap<Lykke.ApiClients.V1.AccountsRegistrationResponseModel, RegisterResponse.Types.Body>()
                .ForMember(d => d.SessionId, o => o.Ignore());
            CreateMap<Lykke.Service.TradesAdapter.AutorestClient.Models.Trade, PublicTrade>()
                .ForMember(d => d.Side, o => o.MapFrom(x => x.Action));
            CreateMap<BlockchainExplorerLinkResponse, ExplorerLinksResponse.Types.ExplorerLinkModel>();
        }
    }
}

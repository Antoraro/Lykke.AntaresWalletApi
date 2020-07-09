using System;
using System.Globalization;
using AntaresWalletApi.Common.Domain.MyNoSqlEntities;
using AutoMapper;
using Google.Protobuf.WellKnownTypes;
using Lykke.ApiClients.V1;
using Lykke.Service.Balances.AutorestClient.Models;
using Lykke.Service.CandlesHistory.Client.Models;
using Swisschain.Lykke.AntaresWalletApi.ApiContract;
using Candle = Swisschain.Lykke.AntaresWalletApi.ApiContract.Candle;
using UpgradeRequest = Swisschain.Lykke.AntaresWalletApi.ApiContract.UpgradeRequest;

namespace AntaresWalletApi.Profiles
{
    public class GrpcProfile : Profile
    {
        public GrpcProfile()
        {
            CreateMap<DateTime, string>().ConvertUsing(dt => dt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
            CreateMap<DateTime?, string>().ConvertUsing(dt => dt.HasValue ? dt.Value.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") : string.Empty);
            CreateMap<decimal, string>().ConvertUsing(d => d.ToString(CultureInfo.InvariantCulture));
            CreateMap<decimal?, string>().ConvertUsing(d => d.HasValue ? d.Value.ToString(CultureInfo.InvariantCulture) : string.Empty);
            CreateMap<double, string>().ConvertUsing(d => d.ToString(CultureInfo.InvariantCulture));
            CreateMap<double?, string>().ConvertUsing(d => d.HasValue ? d.Value.ToString(CultureInfo.InvariantCulture) : string.Empty);
            CreateMap<string, string>().ConvertUsing(d => d ?? string.Empty);
            CreateMap<string, double>().ConvertUsing(d => double.Parse(d, NumberStyles.Any, CultureInfo.InvariantCulture));
            CreateMap<DateTime, Timestamp>().ConvertUsing((dt, timestamp) =>
            {
                var date = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                return Timestamp.FromDateTime(date);
            });

            CreateMap<Lykke.Service.Assets.Client.Models.Asset, Asset>(MemberList.Destination)
                .ForMember(d => d.CardDeposit, o => o.MapFrom(x => x.BankCardsDepositEnabled))
                .ForMember(d => d.SwiftDeposit, o => o.MapFrom(x => x.SwiftDepositEnabled))
                .ForMember(d => d.BlockchainDeposit, o => o.MapFrom(x => x.BlockchainDepositEnabled))
                .ForMember(d => d.Symbol, o => o.MapFrom(x => x.Symbol ?? x.DisplayId ?? x.Id))
                .ForMember(d => d.CanBeBase, o => o.MapFrom(x => x.IsBase));

            CreateMap<Lykke.Service.Assets.Client.Models.AssetCategory, AssetCategory>(MemberList.Destination)
                .ForMember(d => d.IconUrl, o => o.MapFrom(x => x.AndroidIconUrl));

            CreateMap<PriceEntity, PriceUpdate>(MemberList.Destination)
                .ForMember(d => d.Timestamp, o => o.MapFrom(x => x.UpdatedDt));

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

            CreateMap<ClientBalanceResponseModel, Balance>()
                .ForMember(d => d.Timestamp, o => o.MapFrom(x => x.UpdatedAt))
                .ForMember(d => d.Available, o => o.MapFrom(x => x.Balance))
                .ForMember(d => d.Reserved, o => o.MapFrom(x => x.Reserved))
                .ForMember(d => d.AssetId, o => o.MapFrom(x => x.AssetId));

            CreateMap<ErrorModel, ErrorV1>();

            CreateMap<ResponseModelOfHotWalletSuccessTradeRespModel, PlaceOrderResponse>();
            CreateMap<HotWalletSuccessTradeRespModel, PlaceOrderResponse.Types.OrderPayload>();
            CreateMap<ApiHotWalletOrder, OrderModel>();

            CreateMap<ApiOffchainOrder, LimitOrderModel>();
            CreateMap<WatchList, Watchlist>();

            CreateMap<CurrentTierInfo, CurrentTier>();
            CreateMap<TierInfo, NextTier>();
            CreateMap<Lykke.ApiClients.V1.UpgradeRequest, UpgradeRequest>();

            CreateMap<ApiWalletAssetModel, WalletsResponse.Types.WalletAsset>();

            CreateMap<GetSwiftCredentialsModel, SwiftCredentialsResponse.Types.SwiftCredentials>();
            CreateMap<BankCardPaymentUrlInputModel, BankCardPaymentDetailsResponse.Types.BankCardPaymentDetails>();
            CreateMap<BankCardPaymentUrlResponceModel, BankCardPaymentUrlResponse.Types.BankCardPaymentUrl>();
            CreateMap<BankCardPaymentUrlRequest, BankCardPaymentUrlInputModel>()
                .ForMember(d => d.WalletId, o => o.Ignore())
                .ForMember(d => d.OkUrl, o => o.Ignore())
                .ForMember(d => d.FailUrl, o => o.Ignore());

            CreateMap<CountryItem, Country>();
        }
    }
}
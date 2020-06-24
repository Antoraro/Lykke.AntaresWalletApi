using System;
using System.Globalization;
using AutoMapper;
using Lykke.ApiClients.V1;
using Swisschain.Lykke.AntaresWalletApi.ApiContract;

namespace AntaresWalletApi.Profiles
{
    public class GrpcProfile : Profile
    {
        public GrpcProfile()
        {
            CreateMap<DateTime, string>().ConvertUsing(dt => dt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
            CreateMap<DateTime?, string>().ConvertUsing(dt => dt.HasValue ? dt.Value.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") : string.Empty);
            CreateMap<decimal, string>().ConvertUsing(d => d.ToString(CultureInfo.InvariantCulture));
            CreateMap<double, string>().ConvertUsing(d => d.ToString(CultureInfo.InvariantCulture));
            CreateMap<string, string>().ConvertUsing(d => d ?? string.Empty);

            CreateMap<ApiAssetCategoryModel, AssetCategory>(MemberList.Destination)
                .ForMember(d => d.IconUrl, o => o.MapFrom(x => x.AndroidIconUrl));

            CreateMap<ApiDictAsset, Asset>(MemberList.Destination)
                .ForMember(d => d.CardDeposit, o => o.MapFrom(x => x.VisaDeposit))
                .ForMember(d => d.Symbol, o => o.MapFrom(x => x.Symbol ?? x.DisplayId ?? x.Id))
                .ForMember(d => d.CanBeBase, o => o.MapFrom(x => false));
        }
    }
}

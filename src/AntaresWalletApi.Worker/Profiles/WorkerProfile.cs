using System;
using AntaresWalletApi.Common.Domain.MyNoSqlEntities;
using AutoMapper;
using Lykke.Job.CandlesProducer.Contract;
using Lykke.Service.TradesAdapter.Contract;

namespace AntaresWalletApi.Worker.Profiles
{
    public class WorkerProfile : Profile
    {
        public WorkerProfile()
        {
            CreateMap<DateTime, string>().ConvertUsing(dt => dt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));

            CreateMap<CandleUpdate, CandleEntity>(MemberList.Destination)
                .ForMember(d => d.UpdatedAt, o => o.MapFrom(x => x.ChangeTimestamp))
                .ForMember(d => d.PartitionKey, o => o.MapFrom(x => $"{x.AssetPairId}_{x.PriceType}_{x.TimeInterval}"))
                .ForMember(d => d.RowKey, o => o.MapFrom(x => x.CandleTimestamp))
                .ForMember(d => d.TimeStamp, o => o.Ignore())
                .ForMember(d => d.Expires, o => o.Ignore());
        }
    }
}

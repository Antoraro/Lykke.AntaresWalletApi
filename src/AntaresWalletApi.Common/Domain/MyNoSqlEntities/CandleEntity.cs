using System;
using Lykke.Job.CandlesProducer.Contract;
using MyNoSqlServer.Abstractions;

namespace AntaresWalletApi.Common.Domain.MyNoSqlEntities
{
    public class CandleEntity : IMyNoSqlEntity
    {
        public string AssetPairId { get; set; }
        public CandlePriceType PriceType { get; set; }
        public CandleTimeInterval TimeInterval { get; set; }
        public DateTime CandleTimestamp { get; set; }
        public DateTime UpdatedAt { get; set; }
        public double Open { get; set; }
        public double Close { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double LastTradePrice { get; set; }
        public double TradingVolume { get; set; }
        public double TradingOppositeVolume { get; set; }

        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTime TimeStamp { get; set; }
        public DateTime? Expires { get; set; }
    }
}

using System;
using MyNoSqlServer.Abstractions;

namespace AntaresWalletApi.Common.Domain.MyNoSqlEntities
{
    public class PriceEntity : IMyNoSqlEntity
    {
        public string AssetPairId { get; set; }
        public decimal Bid { get; set; }
        public decimal Ask { get; set; }
        public DateTime UpdatedDt { get; set; }
        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTime TimeStamp { get; set; }
        public DateTime? Expires { get; set; }

        public static string GetPk() => "Price";
    }
}

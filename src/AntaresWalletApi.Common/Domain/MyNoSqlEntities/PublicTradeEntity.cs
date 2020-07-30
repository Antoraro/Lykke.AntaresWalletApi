using System;
using Lykke.Service.TradesAdapter.AutorestClient.Models;
using MyNoSqlServer.Abstractions;

namespace AntaresWalletApi.Common.Domain.MyNoSqlEntities
{
    public class PublicTradeEntity : IMyNoSqlEntity
    {
        public string Id { get; set; }
        public string AssetPairId { get; set; }
        public DateTime DateTime { get; set; }
        public int? Index { get; set; }
        public double Volume { get; set; }
        public double Price { get; set; }
        public TradeAction Action { get; set; }

        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTime TimeStamp { get; set; }
        public DateTime? Expires { get; set; }
    }
}

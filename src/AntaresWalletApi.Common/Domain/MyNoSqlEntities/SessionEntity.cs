using System;
using AntaresWalletApi.Common.Extensions;
using Common;
using MyNoSqlServer.Abstractions;

namespace AntaresWalletApi.Common.Domain.MyNoSqlEntities
{
    public class SessionEntity : IMyNoSqlEntity
    {
        public string Id { get; set; }
        public string Token { get; set; }
        public string ClientId { get; set; }
        public string PartnerId { get; set; }
        public bool Verified { get; set; }
        public bool Sms { get; set; }
        public bool Pin { get; set; }
        public string PublicKey { get; set; }
        public DateTime ExpirationDate { get; set; }

        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTime TimeStamp { get; set; }
        public DateTime? Expires { get; set; }

        public static string GetPk() => "Session";
        public static string GenerateSessionId() => $"{Guid.NewGuid():N}{Guid.NewGuid():N}{Guid.NewGuid():N}";

        public static SessionEntity Generate(int expirationInMins, string id)
        {
            var sessionId = id.ToSha256();
            return new SessionEntity
            {
                PartitionKey = GetPk(),
                RowKey = sessionId,
                Id = sessionId,
                ExpirationDate = DateTime.UtcNow.AddMinutes(expirationInMins)
            };
        }
    }
}

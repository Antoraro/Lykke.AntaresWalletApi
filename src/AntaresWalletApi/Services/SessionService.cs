using System;
using System.Threading.Tasks;
using AntaresWalletApi.Common.Domain.MyNoSqlEntities;
using AntaresWalletApi.Common.Extensions;
using Lykke.Service.Session.Client;
using MyNoSqlServer.Abstractions;

namespace AntaresWalletApi.Services
{
    public class SessionService
    {
        private readonly IMyNoSqlServerDataReader<SessionEntity> _sessionsReader;
        private readonly IMyNoSqlServerDataWriter<SessionEntity> _sessionsWriter;
        private readonly IClientSessionsClient _clientSessionsClient;
        private readonly int _expirationTimeInMins;

        public SessionService(
            IMyNoSqlServerDataReader<SessionEntity> sessionsReader,
            IMyNoSqlServerDataWriter<SessionEntity> sessionsWriter,
            IClientSessionsClient clientSessionsClient,
            int expirationTimeInMins
        )
        {
            _sessionsReader = sessionsReader;
            _sessionsWriter = sessionsWriter;
            _clientSessionsClient = clientSessionsClient;
            _expirationTimeInMins = expirationTimeInMins;
        }

        public SessionEntity GetSession(string sessionId)
        {
            return _sessionsReader.Get(SessionEntity.GetPk(), sessionId.ToSha256());
        }

        public async Task<string> CreateVerifiedSessionAsync(string token, string publicKey = null)
        {
            string sessionId = SessionEntity.GenerateSessionId();
            var session = SessionEntity.Generate(_expirationTimeInMins, sessionId);
            session.Token = token;
            session.Verified = true;
            session.Sms = true;
            session.Pin = true;
            session.PublicKey = publicKey;

            var lykkeSession = await _clientSessionsClient.GetAsync(token);
            session.ClientId = lykkeSession.ClientId;
            session.PartnerId = lykkeSession.PartnerId;
            session.LykkeSessionId = lykkeSession.AuthId.ToString();

            await _sessionsWriter.InsertOrReplaceAsync(session);
            return sessionId;
        }

        public async Task<string> CreateSessionAsync(string token, string publicKey = null)
        {
            string sessionId = SessionEntity.GenerateSessionId();
            var session = SessionEntity.Generate(_expirationTimeInMins, sessionId);
            session.Token = token;
            session.PublicKey = publicKey;

            var lykkeSession = await _clientSessionsClient.GetAsync(token);
            session.ClientId = lykkeSession.ClientId;
            session.PartnerId = lykkeSession.PartnerId;
            session.LykkeSessionId = lykkeSession.AuthId.ToString();

            await _sessionsWriter.InsertOrReplaceAsync(session);
            return sessionId;
        }

        public ValueTask SaveSessionAsync(SessionEntity session)
        {
            return _sessionsWriter.InsertOrReplaceAsync(session);
        }

        public ValueTask ProlongateSessionAsync(SessionEntity session)
        {
            session.ExpirationDate = DateTime.UtcNow.AddMinutes(_expirationTimeInMins);
            return _sessionsWriter.InsertOrReplaceAsync(session);
        }

        public async Task LogoutAsync(SessionEntity session)
        {
            await _sessionsWriter.DeleteAsync(session.PartitionKey, session.RowKey);
            await _clientSessionsClient.DeleteSessionIfExistsAsync(session.Token);
        }
    }
}

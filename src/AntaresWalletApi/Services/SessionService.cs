using System;
using System.Threading.Tasks;
using AntaresWalletApi.Common.Domain.MyNoSqlEntities;
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
            return _sessionsReader.Get(SessionEntity.GetPk(), sessionId);
        }

        public SessionEntity GenerateSession()
        {
            return SessionEntity.Generate(_expirationTimeInMins);
        }

        public async Task<SessionEntity> CreateVerifiedSessionAsync(string token, string publicKey = null)
        {
            var session = SessionEntity.Generate(_expirationTimeInMins);
            session.Token = token;
            session.Verified = true;
            session.Sms = true;
            session.Pin = true;
            session.PublicKey = publicKey;

            var lykkeSession = await _clientSessionsClient.GetAsync(token);
            session.ClientId = lykkeSession.ClientId;
            session.PartnerId = lykkeSession.PartnerId;

            await _sessionsWriter.InsertOrReplaceAsync(session);
            return session;
        }

        public async Task<SessionEntity> CreateSessionAsync(string token, string publicKey = null)
        {
            var session = SessionEntity.Generate(_expirationTimeInMins);
            session.Token = token;
            session.PublicKey = publicKey;

            var lykkeSession = await _clientSessionsClient.GetAsync(token);
            session.ClientId = lykkeSession.ClientId;
            session.PartnerId = lykkeSession.PartnerId;

            await _sessionsWriter.InsertOrReplaceAsync(session);
            return session;
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
    }
}

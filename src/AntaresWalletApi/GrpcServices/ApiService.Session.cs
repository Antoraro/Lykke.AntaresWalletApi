using System;
using System.Threading.Tasks;
using AntaresWalletApi.Common.Domain;
using AntaresWalletApi.Extensions;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Lykke.ApiClients.V1;
using Microsoft.AspNetCore.Authorization;
using Swisschain.Lykke.AntaresWalletApi.ApiContract;

namespace AntaresWalletApi.GrpcServices
{
    public partial class ApiService
    {
        [AllowAnonymous]
        public override Task<CheckSessionResponse> IsSessionExpired(CheckSessionRequest request, ServerCallContext context)
        {
            var result = new CheckSessionResponse();

            var session = _sessionService.GetSession(request.SessionId);

            result.Expired = session == null || session.ExpirationDate < DateTime.UtcNow;

            return Task.FromResult(result);
        }

        public override async Task<EmptyResponse> ProlongateSession(Empty request, ServerCallContext context)
        {
            var result = new EmptyResponse();

            string sessionId = context.GetToken();

            var session = _sessionService.GetSession(sessionId);

            if (session == null)
            {
                result.Error = new ErrorV1
                {
                    Code = ErrorModelCode.InvalidInputField.ToString(),
                    Message = ErrorMessages.InvalidFieldValue(nameof(sessionId)),
                    Field = nameof(sessionId)
                };

                return result;
            }

            await _sessionService.ProlongateSessionAsync(session);

            return result;
        }

        public override async Task<EmptyResponse> Logout(Empty request, ServerCallContext context)
        {
            var result = new EmptyResponse();

            string sessionId = context.GetToken();

            var session = _sessionService.GetSession(sessionId);

            if (session == null)
            {
                result.Error = new ErrorV1
                {
                    Code = ErrorModelCode.InvalidInputField.ToString(),
                    Message = ErrorMessages.InvalidFieldValue(nameof(sessionId)),
                    Field = nameof(sessionId)
                };

                return result;
            }

            await _sessionService.LogoutAsync(session);

            return result;
        }
    }
}

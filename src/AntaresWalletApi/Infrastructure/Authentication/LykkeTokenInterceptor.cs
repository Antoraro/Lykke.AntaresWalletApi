using System;
using System.Threading.Tasks;
using AntaresWalletApi.Extensions;
using AntaresWalletApi.Services;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace AntaresWalletApi.Infrastructure.Authentication
{
    public class LykkeTokenInterceptor : Interceptor
    {
        private readonly IGrpcPrincipal _grpcPrincipal;
        private readonly SessionService _sessionService;

        public LykkeTokenInterceptor(
            IGrpcPrincipal grpcPrincipal,
            SessionService sessionService
            )
        {
            _grpcPrincipal = grpcPrincipal;
            _sessionService = sessionService;
        }

        public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(TRequest request,
            ServerCallContext context,
            UnaryServerMethod<TRequest, TResponse> continuation)
        {
            if (!IsAuthRequired(context))
                return await base.UnaryServerHandler(request, context, continuation);

            var sessionId = context.GetToken();

            if (sessionId == null)
            {
                context.Status = new Status(StatusCode.Unauthenticated, "InvalidToken");
                return Activator.CreateInstance<TResponse>();
            }

            var session = _sessionService.GetSession(sessionId);

            if (session == null)
            {
                context.Status = new Status(StatusCode.Unauthenticated, "SessionNotFound");
                return Activator.CreateInstance<TResponse>();
            }

            if (!session.Verified)
            {
                context.Status = new Status(StatusCode.Unauthenticated, "SessionNotVerified");
                return Activator.CreateInstance<TResponse>();
            }

            if (DateTime.UtcNow > session.ExpirationDate)
            {
                context.Status = new Status(StatusCode.Unauthenticated, "SessionExpired");
                return Activator.CreateInstance<TResponse>();
            }

            context.UserState.Add(UserStateProperties.ClientId, session.ClientId);
            context.UserState.Add(UserStateProperties.PartnerId, session.PartnerId);
            context.UserState.Add(UserStateProperties.Token, session.Token);

            return await base.UnaryServerHandler(request, context, continuation);
        }

        private bool IsAuthRequired(ServerCallContext context)
        {
            var endpoint = context.GetHttpContext().GetEndpoint();
            var anonymousAttribute = endpoint.Metadata.GetMetadata<AllowAnonymousAttribute>();

            return anonymousAttribute == null;
        }
    }

    public static class UserStateProperties
    {
        public const string ClientId = "clientId";
        public const string PartnerId = "partnerId";
        public const string Token = "token";
    }
}

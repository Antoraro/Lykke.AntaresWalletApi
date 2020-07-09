using System.Threading.Tasks;
using AntaresWalletApi.Extensions;
using Grpc.Core;
using Grpc.Core.Interceptors;

namespace AntaresWalletApi.Infrastructure.Authentication
{
    public class LykkeTokenInterceptor : Interceptor
    {
        private readonly IGrpcPrincipal _grpcPrincipal;

        public LykkeTokenInterceptor(
            IGrpcPrincipal grpcPrincipal
        )
        {
            _grpcPrincipal = grpcPrincipal;
        }

        public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
        {
            var token = context.GetToken();
            if (token == null)
            {
                context.Status = new Status(StatusCode.Unauthenticated, "Invalid token");
                return default(TResponse);
            }

            var principal = await _grpcPrincipal.GetPrincipalAsync(token);

            if (principal == null)
            {
                context.Status = new Status(StatusCode.Unauthenticated, "Invalid token");
                return default(TResponse);
            }

            context.UserState.Add(UserStateProperties.ClientId, principal.GetClientId());
            context.UserState.Add(UserStateProperties.PartnerId, principal.GetPartnerId());

            return await base.UnaryServerHandler(request, context, continuation);
        }
    }

    public static class UserStateProperties
    {
        public const string ClientId = "clientId";
        public const string PartnerId = "partnerId";
    }
}

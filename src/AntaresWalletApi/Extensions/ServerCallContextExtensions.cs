using System.Linq;
using AntaresWalletApi.Infrastructure.Authentication;
using Grpc.Core;

namespace AntaresWalletApi.Extensions
{
    public static class ServerCallContextExtensions
    {
        public static string GetToken(this ServerCallContext context)
        {
            var header = GetAuthorizationHeader(context);

            if (string.IsNullOrEmpty(header))
                return null;

            var values = header.Split(' ');

            if (values.Length != 2)
                return null;

            if (values[0] != "Bearer")
                return null;

            return values[1];
        }

        public static string GetAuthorizationHeader(this ServerCallContext context)
        {
            var header = context.RequestHeaders.FirstOrDefault(x => x.Key.ToLowerInvariant() == "authorization")?.Value;
            return header;
        }

        public static string GetBearerToken(this ServerCallContext context)
        {
            return $"Bearer {context.UserState[UserStateProperties.Token]}";
        }

        public static string GetClientId(this ServerCallContext context)
        {
            return context.UserState[UserStateProperties.ClientId]?.ToString();
        }

        public static string GetParnerId(this ServerCallContext context)
        {
            return context.UserState[UserStateProperties.PartnerId]?.ToString();
        }

        public static string GetSessionId(this ServerCallContext context)
        {
            return context.UserState[UserStateProperties.SessionId]?.ToString();
        }

        public static string GetLykkeSessionId(this ServerCallContext context)
        {
            return context.UserState[UserStateProperties.LykkeSessionId]?.ToString();
        }
    }
}

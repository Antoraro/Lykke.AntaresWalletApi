using System.Linq;
using System.Security.Claims;
using AntaresWalletApi.Infrastructure.Authentication;
using Microsoft.AspNetCore.Http;

namespace AntaresWalletApi.Extensions
{
    public static class HttpContextExtensions
    {
        public static string GetToken(this HttpContext context)
        {
            return GetToken(context.Request);
        }

        public static string GetToken(this HttpRequest request)
        {
            if (!request.Headers.ContainsKey("Authorization"))
            {
                return null;
            }

            var header = request.Headers["Authorization"].ToString();
            var values = header.Split(' ');

            if (values.Length != 2)
                return null;

            return values[0] != "Bearer" ? null : values[1];
        }

        public static string GetPartnerId(this HttpContext context)
        {
            return context.User.Claims.FirstOrDefault(x => x.Type == UserStateProperties.PartnerId)?.Value;
        }

        public static string GetClientId(this HttpContext context)
        {
            return context.User.Claims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier)?.Value;
        }
    }
}

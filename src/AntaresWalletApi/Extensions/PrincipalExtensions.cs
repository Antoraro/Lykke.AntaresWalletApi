using System.Linq;
using System.Security.Claims;
using AntaresWalletApi.Infrastructure.Authentication;

namespace AntaresWalletApi.Extensions
{
    public static class PrincipalExtensions
    {
        public static string GetPartnerId(this ClaimsPrincipal principal)
        {
            return principal.Claims.FirstOrDefault(x => x.Type == UserStateProperties.PartnerId)?.Value;
        }

        public static string GetClientId(this ClaimsPrincipal principal)
        {
            return principal.Identity?.Name;
        }
    }
}

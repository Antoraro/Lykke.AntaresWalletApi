using System.Security.Claims;
using System.Threading.Tasks;
using Lykke.Service.Session.Client;

namespace AntaresWalletApi.Infrastructure.Authentication
{
    public class GrpcPrincipal : IGrpcPrincipal
    {
        private readonly IClientSessionsClient _clientSessionsClient;
        private readonly ClaimsCache _claimsCache = new ClaimsCache();

        public GrpcPrincipal(
            IClientSessionsClient clientSessionsClient
        )
        {
            _clientSessionsClient = clientSessionsClient;
        }

        public async Task<ClaimsPrincipal> GetPrincipalAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return null;

            var result = _claimsCache.Get(token);
            if (result != null)
                return  result;

            var session = await _clientSessionsClient.GetAsync(token);
            if (session == null)
                return null;

            result = new ClaimsPrincipal(LykkeIdentity.Create(session.ClientId));
            if (session.PartnerId != null)
            {
                (result.Identity as ClaimsIdentity)?.AddClaim(new Claim(UserStateProperties.PartnerId, session.PartnerId));
            }

            _claimsCache.Set(token, result);
            return result;
        }
    }
}

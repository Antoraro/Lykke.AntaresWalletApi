using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using AntaresWalletApi.Extensions;
using JetBrains.Annotations;
using Lykke.Service.Session.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AntaresWalletApi.Infrastructure.Authentication
{
    [UsedImplicitly]
    public class LykkeTokenAuthenticationHandler : AuthenticationHandler<LykkeTokenAuthenticationSchemeOptions>
    {
        private readonly IClientSessionsClient _clientSessionsClient;
        private readonly IMemoryCache _memoryCache;

        public LykkeTokenAuthenticationHandler(
            IClientSessionsClient clientSessionsClient,
            IMemoryCache memoryCache,
            IOptionsMonitor<LykkeTokenAuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ISystemClock clock) : base(options, logger, encoder,
            clock)
        {
            _clientSessionsClient = clientSessionsClient;
            _memoryCache = memoryCache;
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var token = Request.GetToken();

            if (string.IsNullOrEmpty(token))
            {
                return AuthenticateResult.Fail("Invalid token.");
            }

            if (!_memoryCache.TryGetValue(token, out IClientSession session))
            {
                session = await _clientSessionsClient.GetAsync(token);

                if (session == null)
                {
                    return AuthenticateResult.Fail("Invalid token.");
                }

                _memoryCache.Set(token, session, TimeSpan.FromMinutes(5));
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, session.ClientId)
            };

            if (!string.IsNullOrEmpty(session.PartnerId))
            {
                claims.Add(new Claim(UserStateProperties.PartnerId, session.PartnerId));
            }

            var claimsIdentity = new ClaimsIdentity(claims, nameof(LykkeTokenAuthenticationHandler));
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(claimsIdentity), Scheme.Name);

            return AuthenticateResult.Success(ticket);
        }
    }
}

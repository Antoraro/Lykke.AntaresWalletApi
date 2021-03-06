using System;
using System.Security.Principal;

namespace AntaresWalletApi.Infrastructure.Authentication
{
    public class LykkeIdentity : IIdentity
    {
        public string Name { get; private set; }
        public string AuthenticationType { get; private set; }
        public bool IsAuthenticated { get; private set; }
        public DateTime Created { get; private set; }

        public static LykkeIdentity Create(string clientId)
        {
            return new LykkeIdentity
            {
                Name = clientId,
                AuthenticationType = "Token",
                Created = DateTime.UtcNow,
                IsAuthenticated = true
            };
        }
    }
}

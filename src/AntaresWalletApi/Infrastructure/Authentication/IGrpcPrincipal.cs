using System.Security.Claims;
using System.Threading.Tasks;

namespace AntaresWalletApi.Infrastructure.Authentication
{
    public interface IGrpcPrincipal
    {
        Task<ClaimsPrincipal> GetPrincipalAsync(string token);
    }
}

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lykke.Service.Assets.Client;
using Lykke.Service.Assets.Client.Models;

namespace AntaresWalletApi.Services
{
    public class AssetsHelper
    {
        private readonly IAssetsService _assetsService;
        private readonly IAssetsServiceWithCache _assetsServiceWithCache;

        public AssetsHelper(
            IAssetsService assetsService,
            IAssetsServiceWithCache assetsServiceWithCache
            )
        {
            _assetsService = assetsService;
            _assetsServiceWithCache = assetsServiceWithCache;
        }

        public async Task<IEnumerable<Asset>> GetAssetsAvailableToClientAsync(
            string clientId,
            string partnerId,
            bool? tradable = default(bool?))
        {
            var allAssets = await _assetsServiceWithCache.GetAllAssetsAsync(true);
            var relevantAssets = allAssets.Where(x => !x.IsDisabled && (!tradable.HasValue || x.IsTradable == tradable));

            var assetsAvailableToUser = new HashSet<string>(await _assetsService.ClientGetAssetIdsAsync(clientId, true));

            return relevantAssets.Where(x =>
                assetsAvailableToUser.Contains(x.Id) &&
                (x.NotLykkeAsset
                    ? partnerId != null && x.PartnerIds.Contains(partnerId)
                    : partnerId == null || x.PartnerIds.Contains(partnerId)));
        }
    }
}

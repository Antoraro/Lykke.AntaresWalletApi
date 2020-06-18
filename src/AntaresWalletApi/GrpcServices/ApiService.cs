using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AntaresWalletApi.Common.Configuration;
using AutoMapper;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Lykke.ApiClients.V1;
using Swisschain.Lykke.AntaresWalletApi.ApiContract;

namespace AntaresWalletApi.GrpcServices
{
    public class ApiService : Swisschain.Lykke.AntaresWalletApi.ApiContract.ApiService.ApiServiceBase
    {
        private readonly ILykkeWalletAPIv1Client _walletApiV1Client;
        private readonly TokenConfig _tokenConfig;
        private readonly IMapper _mapper;

        public ApiService(
            ILykkeWalletAPIv1Client walletApiV1Client,
            TokenConfig tokenConfig,
            IMapper mapper
        )
        {
            _walletApiV1Client = walletApiV1Client;
            _tokenConfig = tokenConfig;
            _mapper = mapper;
        }
        public override async Task<AssetsDictionaryResponse> AssetsDictionary(Empty request, ServerCallContext context)
        {
            var categoriesResponse = _walletApiV1Client.GetAssetCategoryAsync(_tokenConfig.Auth);
            var assetsResponse = _walletApiV1Client.GetDictsAssetsAsync(_tokenConfig.Auth);
            var baseAssets = _walletApiV1Client.GetBaseAssetListAsync(_tokenConfig.Auth);

            await Task.WhenAll(categoriesResponse, assetsResponse, baseAssets);

            var result = new AssetsDictionaryResponse();

            if (categoriesResponse.Result.Result != null)
            {
                result.Categories.AddRange(_mapper.Map<List<AssetCategory>>(categoriesResponse.Result.Result.AssetCategories));
            }

            if (assetsResponse.Result.Result != null)
            {
                var assets = _mapper.Map<List<Asset>>(assetsResponse.Result.Result.Assets);

                if (baseAssets.Result.Result != null)
                {
                    var baseAssetIds = baseAssets.Result.Result.Assets.Select(x => x.Id).ToList();
                    foreach (var asset in assets)
                    {
                        asset.CanBeBase = baseAssetIds.Contains(asset.Id);
                    }
                }

                result.Assets.AddRange(assets);
            }

            return result;
        }
    }
}

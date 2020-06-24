using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AntaresWalletApi.Common.Domain.MyNoSqlEntities;
using AntaresWalletApi.Extensions;
using AutoMapper;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Lykke.ApiClients.V1;
using MyNoSqlServer.Abstractions;
using Swisschain.Lykke.AntaresWalletApi.ApiContract;
using Status = Grpc.Core.Status;

namespace AntaresWalletApi.GrpcServices
{
    public class ApiService : Swisschain.Lykke.AntaresWalletApi.ApiContract.ApiService.ApiServiceBase
    {
        private readonly ILykkeWalletAPIv1Client _walletApiV1Client;
        private readonly IMyNoSqlServerDataReader<PriceEntity> _pricesReader;
        private readonly IMapper _mapper;

        public ApiService(
            ILykkeWalletAPIv1Client walletApiV1Client,
            IMyNoSqlServerDataReader<PriceEntity> pricesReader,
            IMapper mapper
        )
        {
            _walletApiV1Client = walletApiV1Client;
            _pricesReader = pricesReader;
            _mapper = mapper;
        }
        public override async Task<AssetsDictionaryResponse> AssetsDictionary(Empty request, ServerCallContext context)
        {
            var token = context.GetToken();

            if (token == null)
                throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid token"));

            var result = new AssetsDictionaryResponse();

            try
            {
                var categoriesResponse = _walletApiV1Client.GetAssetCategoryAsync(token);
                var assetsResponse = _walletApiV1Client.GetDictsAssetsAsync(token);
                var baseAssets = _walletApiV1Client.GetBaseAssetListAsync(token);

                await Task.WhenAll(categoriesResponse, assetsResponse, baseAssets);


                if (categoriesResponse.Result.Result != null)
                {
                    result.Categories.AddRange(
                        _mapper.Map<List<AssetCategory>>(categoriesResponse.Result.Result.AssetCategories));
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
            }
            catch (ApiException ex)
            {
                if (ex.StatusCode == 401)
                    throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid token"));
            }

            return result;
        }

        public override async Task<PricesResponse> GetPrices(PricesRequest request, ServerCallContext context)
        {
            var entities = _pricesReader.Get(PriceEntity.GetPk());

            List<PriceUpdate> result = new List<PriceUpdate>();

            if (entities.Any())
            {
                result = _mapper.Map<List<PriceUpdate>>(entities);
            }
            // else
            // {
            //     var marketData = await _marketDataClient.GetMarketDataAsync(new Empty());
            //     result = _mapper.Map<List<PriceUpdate>>(marketData.Items.ToList());
            // }

            if (request.AssetPairIds.Any())
            {
                result = result.Where(x =>
                        request.AssetPairIds.Contains(x.AssetPairId, StringComparer.InvariantCultureIgnoreCase))
                    .ToList();
            }

            var response = new PricesResponse();

            response.Prices.AddRange(result);

            return response;
        }
    }
}

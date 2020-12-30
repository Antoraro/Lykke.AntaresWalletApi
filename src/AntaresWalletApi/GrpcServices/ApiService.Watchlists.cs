using System.Linq;
using System.Threading.Tasks;
using AntaresWalletApi.Extensions;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Lykke.ApiClients.V1;
using Swisschain.Lykke.AntaresWalletApi.ApiContract;
using Status = Grpc.Core.Status;
using ApiExceptionV1 = Lykke.ApiClients.V1.ApiException;
using ApiExceptionV2 = Lykke.ApiClients.V2.ApiException;

namespace AntaresWalletApi.GrpcServices
{
    public partial class ApiService
    {
        public override async Task<WatchlistsResponse> GetWatchlists(Empty request, ServerCallContext context)
        {
            var token = context.GetBearerToken();
            var response = await _walletApiV1Client.WatchListsGetListAsync(token);

            var result = new WatchlistsResponse();

            if (response.Result != null)
            {
                result.Body = new WatchlistsResponse.Types.Body();

                foreach (var watchlist in response.Result)
                {
                    var list = _mapper.Map<Watchlist>(watchlist);
                    list.AssetIds.AddRange(watchlist.AssetIds);

                    result.Body.Result.Add(list);
                }
            }

            if (response.Error != null)
            {
                result.Error = response.Error.ToApiError();
            }

            return result;
        }

        public override async Task<WatchlistResponse> GetWatchlist(WatchlistRequest request, ServerCallContext context)
        {
            var token = context.GetBearerToken();
            var response = await _walletApiV1Client.WatchListsGetAsync(request.Id, token);

            var result = new WatchlistResponse();

            if (response.Result != null)
            {
                result.Body = _mapper.Map<Watchlist>(response.Result);
                result.Body.AssetIds.AddRange(response.Result.AssetIds);
            }

            if (response.Error != null)
            {
                result.Error = response.Error.ToApiError();
            }

            return result;
        }

        public override async Task<WatchlistResponse> AddWatchlist(AddWatchlistRequest request, ServerCallContext context)
        {
            var token = context.GetBearerToken();
            var response = await _walletApiV1Client.WatchListsCreateAsync(
            new CustomWatchListCreateModel
            {
                Name = request.Name,
                Order = request.Order,
                AssetIds = request.AssetIds.ToList()
            }, token);

            var result = new WatchlistResponse();

            if (response.Result != null)
            {
                result.Body = _mapper.Map<Watchlist>(response.Result);
                result.Body.AssetIds.AddRange(response.Result.AssetIds);
            }

            if (response.Error != null)
            {
                result.Error = response.Error.ToApiError();
            }

            return result;
        }

        public override async Task<WatchlistResponse> UpdateWatchlist(UpdateWatchlistRequest request, ServerCallContext context)
        {
            var token = context.GetBearerToken();
            var response = await _walletApiV1Client.WatchListsUpdateAsync(
                request.Id,
                new CustomWatchListUpdateModel
                {
                    Name = request.Name,
                    Order = request.Order,
                    AssetIds = request.AssetIds.ToList()
                }, token);

            var result = new WatchlistResponse();

            if (response.Result != null)
            {
                result.Body = _mapper.Map<Watchlist>(response.Result);
                result.Body.AssetIds.AddRange(response.Result.AssetIds);
            }

            if (response.Error != null)
            {
                result.Error = response.Error.ToApiError();
            }

            return result;
        }

        public override async Task<EmptyResponse> DeleteWatchlist(DeleteWatchlistRequest request, ServerCallContext context)
        {
            var token = context.GetBearerToken();
            var response = await _walletApiV1Client.WatchListsDeleteAsync(request.Id, token);

            var result = new EmptyResponse();

            if (response.Error != null)
            {
                result.Error = response.Error.ToApiError();
            }

            return result;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using AntaresWalletApi.Common.Domain;
using AntaresWalletApi.Extensions;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Lykke.ApiClients.V1;
using Lykke.ApiClients.V2;
using Lykke.MatchingEngine.Connector.Models.Api;
using Lykke.Service.Assets.Client;
using Lykke.Service.ClientAccount.Client.Models;
using Swisschain.Lykke.AntaresWalletApi.ApiContract;
using ApiExceptionV1 = Lykke.ApiClients.V1.ApiException;
using ApiExceptionV2 = Lykke.ApiClients.V2.ApiException;
using Enum = System.Enum;
using LimitOrderModel = Swisschain.Lykke.AntaresWalletApi.ApiContract.LimitOrderModel;
using LimitOrderRequest = Swisschain.Lykke.AntaresWalletApi.ApiContract.LimitOrderRequest;
using MarketOrderRequest = Swisschain.Lykke.AntaresWalletApi.ApiContract.MarketOrderRequest;
using Status = Grpc.Core.Status;

namespace AntaresWalletApi.GrpcServices
{
    public partial class ApiService
    {
        public override async Task<LimitOrdersResponse> GetOrders(LimitOrdersRequest request, ServerCallContext context)
        {
            var token = context.GetBearerToken();
            var response = await _walletApiV1Client.OffchainLimitListAsync(request.AssetPairId, token);

            var result = new LimitOrdersResponse();

            if (response.Result != null)
            {
                result.Body = new LimitOrdersResponse.Types.Body();
                result.Body.Orders.AddRange(_mapper.Map<List<LimitOrderModel>>(response.Result.Orders));
            }

            if (response.Error != null)
            {
                result.Error = response.Error.ToApiError();
            }

            return result;
        }

        public override async Task<PlaceOrderResponse> PlaceLimitOrder(LimitOrderRequest request, ServerCallContext context)
        {
            var token = context.GetBearerToken();
            var response = await _walletApiV1Client.HotWalletPlaceLimitOrderAsync(
                new HotWalletLimitOperation
                {
                    AssetPair = request.AssetPairId,
                    AssetId = request.AssetId,
                    Price = request.Price,
                    Volume = request.Volume
                },
                token,
                _walletApiConfig.Secret);

            var result = new PlaceOrderResponse();

            if (response.Result != null)
            {
                result.Body = _mapper.Map<OrderModel>(response.Result.Order);
            }

            if (response.Error != null)
            {
                result.Error = response.Error.ToApiError();
            }

            return result;
        }

        public override async Task<PlaceOrderResponse> PlaceMarketOrder(MarketOrderRequest request, ServerCallContext context)
        {
            var token = context.GetBearerToken();
            var response = await _walletApiV1Client.HotWalletPlaceMarketOrderAsync(
                new HotWalletOperation
                {
                    AssetPair = request.AssetPairId,
                    AssetId = request.AssetId,
                    Volume = request.Volume
                },
                token,
                _walletApiConfig.Secret);

            var result = new PlaceOrderResponse();

            if (response.Result != null)
            {
                result.Body = _mapper.Map<OrderModel>(response.Result.Order);
            }

            if (response.Error != null)
            {
                result.Error = response.Error.ToApiError();
            }

            return result;
        }

        public override async Task<CancelOrderResponse> CancelAllOrders(CancelOrdersRequest request, ServerCallContext context)
        {
            MeResponseModel response;

            if (!string.IsNullOrEmpty(request.AssetPairId))
            {
                var result = await _validationService.ValidateAssetPairAsync(request.AssetPairId);

                if (result != null)
                {
                    var res = new CancelOrderResponse
                    {
                        Error = new ErrorResponseBody{Code = ErrorCode.InvalidField, Message = result.Message}
                    };

                    res.Error.Fields.Add(result.FieldName, result.Message);
                }

                bool? isBuy;

                if (request.OptionalSideCase == CancelOrdersRequest.OptionalSideOneofCase.None)
                {
                    isBuy = null;
                }
                else
                {
                    switch (request.Side)
                    {
                        case Side.Buy:
                            isBuy = true;
                            break;
                        case Side.Sell:
                            isBuy = false;
                            break;
                        default:
                            isBuy = null;
                            break;
                    }
                }

                var model = new LimitOrderMassCancelModel
                {
                    Id = Guid.NewGuid().ToString(),
                    AssetPairId = request.AssetPairId,
                    ClientId = context.GetClientId(),
                    IsBuy = isBuy
                };

                response = await _matchingEngineClient.MassCancelLimitOrdersAsync(model);
            }
            else
            {
                var orders = await GetOrders(new LimitOrdersRequest(), context);

                if (orders.Body?.Orders.Any() ?? false)
                {
                    var orderIds = orders.Body.Orders.Select(x => x.Id).ToList();
                    response = await _matchingEngineClient.CancelLimitOrdersAsync(orderIds);
                }
                else
                {
                    response = new MeResponseModel{Status = MeStatusCodes.Ok};
                }
            }

            if (response == null)
            {
                return new CancelOrderResponse
                {
                    Error = new ErrorResponseBody{Code = ErrorCode.Runtime, Message = ErrorMessages.MeNotAvailable}
                };
            }

            if (response.Status == MeStatusCodes.Ok)
                return new CancelOrderResponse
                {
                    Body = new CancelOrderResponse.Types.Body
                    {
                        Payload = true
                    }
                };

            return new CancelOrderResponse
            {
                Error = new ErrorResponseBody{Code = ErrorCode.Runtime, Message = response.Message ?? response.Status.ToString()}
            };
        }

        public override async Task<CancelOrderResponse> CancelOrder(CancelOrderRequest request, ServerCallContext context)
        {
            MeResponseModel response = await _matchingEngineClient.CancelLimitOrderAsync(request.OrderId);

            if (response == null)
            {
                return new CancelOrderResponse
                {
                    Error = new ErrorResponseBody{Code = ErrorCode.Runtime, Message = ErrorMessages.MeNotAvailable}
                };
            }

            if (response.Status == MeStatusCodes.Ok)
                return new CancelOrderResponse {Body = new CancelOrderResponse.Types.Body {Payload = true}};

            return new CancelOrderResponse
            {
                Error = new ErrorResponseBody{Code = ErrorCode.Runtime, Message = response.Message ?? response.Status.ToString()}
            };
        }

        public override async Task<TradesResponse> GetTrades(TradesRequest request, ServerCallContext context)
        {
            var result = new TradesResponse();

            var token = context.GetBearerToken();
            var wallets = await _clientAccountClient.Wallets.GetClientWalletsFilteredAsync(context.GetClientId(), WalletType.Trading);

            var walletId = wallets.FirstOrDefault()?.Id;

            var response = await _walletApiV2Client.GetTradesByWalletIdAsync(
                walletId, request.AssetPairId, request.Take, request.Skip,
                request.OptionalFromDateCase == TradesRequest.OptionalFromDateOneofCase.None ? (DateTimeOffset?) null : request.From.ToDateTimeOffset(),
                request.OptionalToDateCase == TradesRequest.OptionalToDateOneofCase.None ? (DateTimeOffset?) null : request.To.ToDateTimeOffset(),
                request.OptionalTradeTypeCase == TradesRequest.OptionalTradeTypeOneofCase.None ? null : (TradeType?)Enum.Parse(typeof(TradeType?), request.TradeType),
                token);

            if (response != null)
            {
                result.Body = new TradesResponse.Types.Body();
                result.Body.Trades.AddRange(_mapper.Map<List<TradesResponse.Types.TradeModel>>(response));
            }

            return result;
        }

        public override async Task<AssetTradesResponse> GetAssetTrades(AssetTradesRequest request, ServerCallContext context)
        {
            var result = new AssetTradesResponse();

            var token = context.GetBearerToken();
            var wallets = await _clientAccountClient.Wallets.GetClientWalletsFilteredAsync(context.GetClientId(), WalletType.Trading);

            var walletId = wallets.FirstOrDefault()?.Id;

            var response = await _walletApiV2Client.GetByWalletIdAsync(
                walletId, new List<string>{}, request.AssetId, null, request.Take, request.Skip,
                token);

            var assetPairs = await _assetsService.AssetPairGetAllAsync();

            if (response != null)
            {
                foreach (var group in response.GroupBy(x => x.Id))
                {
                    var pair = group.ToList();

                    if (pair.Count == 0 || pair.Count != 2)
                        continue;

                    string assetPairId = pair[0].AssetPair;

                    var assetPair = assetPairs.FirstOrDefault(x => x.Id == assetPairId);

                    if (assetPair == null)
                        continue;

                    var assetTrade = new AssetTradesResponse.Types.AssetTradeModel
                    {
                        Id = pair[0].Id,
                        AssetPairId = assetPairId,
                        BaseAssetId = assetPair.BaseAssetId,
                        QuoteAssetId = assetPair.QuotingAssetId,
                        Price = pair[0].Price?.ToString() ?? string.Empty,
                        Timestamp = Timestamp.FromDateTime(pair[0].DateTime.UtcDateTime)
                    };

                    foreach (var item in pair)
                    {
                        if (item.Asset == assetPair.BaseAssetId)
                        {
                            assetTrade.BaseVolume = item.Amount.ToString(CultureInfo.InvariantCulture);
                        }
                        else
                        {
                            assetTrade.QuoteVolume = item.Amount.ToString(CultureInfo.InvariantCulture);
                        }
                    }

                    result.Body = new AssetTradesResponse.Types.Body();
                    result.Body.Trades.Add(assetTrade);
                }
            }

            return result;
        }

        public override async Task<PublicTradesResponse> GetPublicTrades(PublicTradesRequest request, ServerCallContext context)
        {
            var result = new PublicTradesResponse();

            if (string.IsNullOrEmpty(request.AssetPairId))
            {

                result.Error = new ErrorResponseBody
                {
                    Code = ErrorCode.InvalidField,
                    Message = ErrorMessages.CantBeEmpty(nameof(request.AssetPairId))
                };

                result.Error.Fields.Add(nameof(request.AssetPairId), result.Error.Message);

                return result;
            }

            var response = await _tradesAdapterClient.GetTradesByAssetPairIdAsync(request.AssetPairId, request.Skip, request.Take);

            if (response.Records != null)
            {
                result.Body = new PublicTradesResponse.Types.Body();
                result.Body.Result.AddRange(_mapper.Map<List<PublicTrade>>(response.Records));
            }

            if (response.Error != null)
            {
                result.Error = new ErrorResponseBody
                {
                    Code = ErrorCode.Runtime,
                    Message = response.Error.Message
                };
            }

            return result;
        }
    }
}

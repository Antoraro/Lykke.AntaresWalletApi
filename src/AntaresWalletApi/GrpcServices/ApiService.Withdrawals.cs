using System.Threading.Tasks;
using AntaresWalletApi.Extensions;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Lykke.ApiClients.V1;
using Newtonsoft.Json;
using Swisschain.Lykke.AntaresWalletApi.ApiContract;
using ApiExceptionV1 = Lykke.ApiClients.V1.ApiException;
using ApiExceptionV2 = Lykke.ApiClients.V2.ApiException;
using Status = Grpc.Core.Status;

namespace AntaresWalletApi.GrpcServices
{
    public partial class ApiService
    {
        #region crypto

        public override async Task<WithdrawalCryptoInfoResponse> GetWithdrawalCryptoInfo(WithdrawalCryptoInfoRequest request, ServerCallContext context)
        {
            var result = new WithdrawalCryptoInfoResponse();

            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV2Client.GetAssetInfoAsync(request.AssetId, token);

                if (response != null)
                {
                    result.WithdrawalInfo = _mapper.Map<WithdrawalCryptoInfoResponse.Types.WithdrawalCryptoInfo>(response);
                }

                return result;
            }
            catch (ApiExceptionV2 ex)
            {
                if (ex.StatusCode == 401)
                    throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid token"));

                if (ex.StatusCode == 400)
                {
                    result.Error = JsonConvert.DeserializeObject<ErrorV2>(ex.Response);
                    return result;
                }

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        public override async Task<CheckCryptoAddressResponse> IsCryptoAddressValid(CheckCryptoAddressRequest request, ServerCallContext context)
        {
            var result = new CheckCryptoAddressResponse();

            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV1Client.HotWalletAddressesValidityAsync(request.AddressExtension,
                    request.Address, request.AssetId, token);

                if (response.Result != null)
                {
                    result.Result = new CheckCryptoAddressResponse.Types.CheckCryptoAddressPayload
                    {
                        IsValid = response.Result.IsValid
                    };
                }

                if (response.Error != null)
                {
                    result.Error = _mapper.Map<ErrorV1>(response.Error);
                }

                return result;
            }
            catch (ApiExceptionV1 ex)
            {
                if (ex.StatusCode == 401)
                    throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid token"));

                if (ex.StatusCode == 500)
                {
                    result = JsonConvert.DeserializeObject<CheckCryptoAddressResponse>(ex.Response);
                    return result;
                }

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        public override async Task<EmptyResponse> CryptoCashout(CryptoCashoutRequest request, ServerCallContext context)
        {
            var result = new EmptyResponse();

            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV1Client.HotWalletCashoutAsync(_mapper.Map<HotWalletCashoutOperation>(request),
                    token, _walletApiConfig.Secret);

                if (response.Error != null)
                {
                    result.Error = _mapper.Map<ErrorV1>(response.Error);
                }

                return result;
            }
            catch (ApiExceptionV1 ex)
            {
                if (ex.StatusCode == 401)
                    throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid token"));

                if (ex.StatusCode == 500)
                {
                    result = JsonConvert.DeserializeObject<EmptyResponse>(ex.Response);
                    return result;
                }

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        #endregion

        #region fiat

        public override async Task<SwiftCashoutInfoResponse> GetSwiftCashoutInfo(Empty request, ServerCallContext context)
        {
            var result = new SwiftCashoutInfoResponse();

            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV1Client.OffchainGetCashoutSwiftLastDataAsync(token);

                if (response.Result != null)
                {
                    result.Result = _mapper.Map<SwiftCashoutInfoResponse.Types.SwiftCashoutInfo>(response.Result);
                }

                if (response.Error != null)
                {
                    result.Error = _mapper.Map<ErrorV1>(response.Error);
                }

                return result;
            }
            catch (ApiExceptionV1 ex)
            {
                if (ex.StatusCode == 401)
                    throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid token"));

                if (ex.StatusCode == 500)
                {
                    result = JsonConvert.DeserializeObject<SwiftCashoutInfoResponse>(ex.Response);
                    return result;
                }

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        public override async Task<SwiftCashoutFeeResponse> GetSwiftCashoutFee(SwiftCashoutFeeRequest request, ServerCallContext context)
        {
            var result = new SwiftCashoutFeeResponse();

            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV1Client.OffchainCashoutSwiftFeeAsync(request.AssetId, request.CountryCode, token);

                if (response.Result != null)
                {
                    result.Result = _mapper.Map<SwiftCashoutFeeResponse.Types.SwiftCashoutFee>(response.Result);
                }

                if (response.Error != null)
                {
                    result.Error = _mapper.Map<ErrorV1>(response.Error);
                }

                return result;
            }
            catch (ApiExceptionV1 ex)
            {
                if (ex.StatusCode == 401)
                    throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid token"));

                if (ex.StatusCode == 500)
                {
                    result = JsonConvert.DeserializeObject<SwiftCashoutFeeResponse>(ex.Response);
                    return result;
                }

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        public override async Task<SwiftCashoutResponse> SwiftCashout(SwiftCashoutRequest request, ServerCallContext context)
        {
            var result = new SwiftCashoutResponse();

            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV1Client.OffchainCashoutSwiftAsync(_mapper.Map<OffchainCashoutSwiftModel>(request), token);

                if (response.Result != null)
                {
                    var finalizeResponse =
                        await _walletApiV1Client.OffchainFinalizeAsync(new OffchainFinalizeModel
                        {
                            TransferId = response.Result.TransferId,
                            ClientRevokePubKey = response.Result.TransferId,
                            ClientRevokeEncryptedPrivateKey = response.Result.TransferId,
                            SignedTransferTransaction = response.Result.TransferId
                        }, token);

                    if (finalizeResponse.Result != null)
                    {
                        result.Result = new SwiftCashoutResponse.Types.SwiftCashoutData
                        {
                            TransferId = finalizeResponse.Result.TransferId
                        };
                    }

                    if (response.Error != null)
                    {
                        result.Error = _mapper.Map<ErrorV1>(response.Error);
                    }
                }

                if (response.Error != null)
                {
                    result.Error = _mapper.Map<ErrorV1>(response.Error);
                }

                return result;
            }
            catch (ApiExceptionV1 ex)
            {
                if (ex.StatusCode == 401)
                    throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid token"));

                if (ex.StatusCode == 500)
                {
                    result = JsonConvert.DeserializeObject<SwiftCashoutResponse>(ex.Response);
                    return result;
                }

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        #endregion
    }
}

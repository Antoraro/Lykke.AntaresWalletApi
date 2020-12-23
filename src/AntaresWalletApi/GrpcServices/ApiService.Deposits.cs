﻿using System.Threading.Tasks;
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

        public override async Task<CryptoDepositAddressResponse> GetCryptoDepositAddress(CryptoDepositAddressRequest request, ServerCallContext context)
        {
            var result = new CryptoDepositAddressResponse();

            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV2Client.GetCryptosDepositAddressesAsync(request.AssetId, token);

                if (response != null)
                {
                    result.Address = new CryptoDepositAddressResponse.Types.CryptoDepositAddress
                    {
                        Address = response.BaseAddress,
                        Tag = response.AddressExtension ?? string.Empty
                    };
                }
                else
                {

                    await _walletApiV2Client.PostCryptosDepositAddressesAsync(request.AssetId, token);
                    response = await _walletApiV2Client.GetCryptosDepositAddressesAsync(request.AssetId, token);

                    if (response != null)
                    {
                        result.Address = new CryptoDepositAddressResponse.Types.CryptoDepositAddress
                        {
                            Address = response.BaseAddress,
                            Tag = response.AddressExtension
                        };
                    }
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

        #endregion

        #region fiat

         public override async Task<SwiftCredentialsResponse> GetSwiftCredentials(SwiftCredentialsRequest request, ServerCallContext context)
        {
            var result = new SwiftCredentialsResponse();

            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV1Client.SwiftCredentialsGetByAssetIdAsync(request.AssetId, token);

                if (response.Result != null)
                {
                    result.Result = _mapper.Map<SwiftCredentialsResponse.Types.SwiftCredentials>(response.Result);
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
                    result = JsonConvert.DeserializeObject<SwiftCredentialsResponse>(ex.Response);
                    return result;
                }

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        public override async Task<EmptyResponse> SendBankTransferRequest(BankTransferRequest request, ServerCallContext context)
        {
            var result = new EmptyResponse();

            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV1Client.BankTransferRequestAsync(
                new TransferReqModel
                {
                    AssetId = request.AssetId,
                    BalanceChange = request.BalanceChange
                }, token);

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

        public override async Task<BankCardPaymentDetailsResponse> GetBankCardPaymentDetails(Empty request, ServerCallContext context)
        {
            var result = new BankCardPaymentDetailsResponse();

            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV1Client.GetBankCardPaymentUrlFormValuesAsync(token);

                if (response.Result != null)
                {
                    result.Result = _mapper.Map<BankCardPaymentDetailsResponse.Types.BankCardPaymentDetails>(response.Result);
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
                    result = JsonConvert.DeserializeObject<BankCardPaymentDetailsResponse>(ex.Response);
                    return result;
                }

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        public override async Task<BankCardPaymentUrlResponse> GetBankCardPaymentUrl(BankCardPaymentUrlRequest request, ServerCallContext context)
        {
            var result = new BankCardPaymentUrlResponse();

            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV1Client.BankCardPaymentUrlAsync(_mapper.Map<BankCardPaymentUrlInputModel>(request), string.Empty, token);

                if (response.Result != null)
                {
                    result.Result = _mapper.Map<BankCardPaymentUrlResponse.Types.BankCardPaymentUrl>(response.Result);
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
                    result = JsonConvert.DeserializeObject<BankCardPaymentUrlResponse>(ex.Response);
                    return result;
                }

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        #endregion
    }
}

using System.Threading.Tasks;
using AntaresWalletApi.Common.Domain;
using AntaresWalletApi.Extensions;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Lykke.ApiClients.V1;
using Lykke.ApiClients.V2;
using Newtonsoft.Json;
using Swisschain.Lykke.AntaresWalletApi.ApiContract;
using ApiExceptionV1 = Lykke.ApiClients.V1.ApiException;
using ApiExceptionV2 = Lykke.ApiClients.V2.ApiException;

namespace AntaresWalletApi.GrpcServices
{
    public partial class ApiService
    {
        #region crypto

        public override async Task<CryptoDepositAddressResponse> GetCryptoDepositAddress(
            CryptoDepositAddressRequest request,
            ServerCallContext context)
        {
            var result = new CryptoDepositAddressResponse();
            var token = context.GetBearerToken();

            CryptoDepositAddressRespModel response;

            try
            {
                response = await _walletApiV2Client.GetCryptosDepositAddressesAsync(request.AssetId, token);
            }
            catch (ApiExceptionV2 ex)
            {
                if (ex.StatusCode == 400)
                {
                    var error = JsonConvert.DeserializeObject<ErrorV2Model>(ex.Response);

                    if (error.Error == "BlockchainWalletDepositAddressNotGenerated")
                    {
                        var address = await GenerateAndGetAddressAsync(request.AssetId, token);

                        if (address != null)
                        {
                            result.Body = address;
                        }

                        return result;
                    }
                }

                throw;
            }

            if (response != null)
            {
                result.Body = new CryptoDepositAddressResponse.Types.Body
                {
                    Address = string.IsNullOrEmpty(response.BaseAddress) ? response.Address : response.BaseAddress,
                    Tag = response.AddressExtension ?? string.Empty
                };
            }

            return result;
        }

        #endregion

        #region fiat

        public override async Task<SwiftCredentialsResponse> GetSwiftCredentials(SwiftCredentialsRequest request, ServerCallContext context)
        {
            var result = new SwiftCredentialsResponse();

            var token = context.GetBearerToken();
            var response = await _walletApiV1Client.SwiftCredentialsGetByAssetIdAsync(request.AssetId, token);

            if (response.Result != null)
            {
                result.Body = _mapper.Map<SwiftCredentialsResponse.Types.Body>(response.Result);
            }

            if (response.Error != null)
            {
                result.Error = response.Error.ToApiError();
            }

            return result;
        }

        public override async Task<EmptyResponse> SendBankTransferRequest(BankTransferRequest request, ServerCallContext context)
        {
            var result = new EmptyResponse();

            var token = context.GetBearerToken();
            var response = await _walletApiV1Client.BankTransferRequestAsync(
                new TransferReqModel
                {
                    AssetId = request.AssetId,
                    BalanceChange = request.BalanceChange
                },
                token);

            if (response.Error != null)
            {
                result.Error = response.Error.ToApiError();
            }

            return result;
        }

        public override async Task<BankCardPaymentDetailsResponse> GetBankCardPaymentDetails(Empty request, ServerCallContext context)
        {
            var result = new BankCardPaymentDetailsResponse();

            var token = context.GetBearerToken();
            var response = await _walletApiV1Client.GetBankCardPaymentUrlFormValuesAsync(token);

            if (response.Result != null)
            {
                result.Body = _mapper.Map<BankCardPaymentDetailsResponse.Types.Body>(response.Result);
            }

            if (response.Error != null)
            {
                result.Error = response.Error.ToApiError();
            }

            return result;
        }

        public override async Task<BankCardPaymentUrlResponse> GetBankCardPaymentUrl(BankCardPaymentUrlRequest request, ServerCallContext context)
        {
            var result = new BankCardPaymentUrlResponse();

            var token = context.GetBearerToken();
            var response =
                await _walletApiV1Client.BankCardPaymentUrlAsync(_mapper.Map<BankCardPaymentUrlInputModel>(request),
                    string.Empty,
                    token);

            if (response.Result != null)
            {
                result.Body = _mapper.Map<BankCardPaymentUrlResponse.Types.Body>(response.Result);
            }

            if (response.Error != null)
            {
                result.Error = response.Error.ToApiError();
            }

            return result;
        }

        #endregion

        private async Task<CryptoDepositAddressResponse.Types.Body> GenerateAndGetAddressAsync(string assetId, string token)
        {
            await _walletApiV2Client.PostCryptosDepositAddressesAsync(assetId, token);
            var response = await _walletApiV2Client.GetCryptosDepositAddressesAsync(assetId, token);

            if (response != null)
            {
                return new CryptoDepositAddressResponse.Types.Body
                {
                    Address = response.BaseAddress,
                    Tag = response.AddressExtension
                };
            }

            return null;
        }
    }
}

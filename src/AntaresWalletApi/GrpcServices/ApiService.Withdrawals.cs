using System.Threading.Tasks;
using AntaresWalletApi.Extensions;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Lykke.ApiClients.V1;
using Swisschain.Lykke.AntaresWalletApi.ApiContract;

namespace AntaresWalletApi.GrpcServices
{
    public partial class ApiService
    {
        #region crypto

        public override async Task<WithdrawalCryptoInfoResponse> GetWithdrawalCryptoInfo(WithdrawalCryptoInfoRequest request, ServerCallContext context)
        {
            var result = new WithdrawalCryptoInfoResponse();

            var token = context.GetBearerToken();
            var response = await _walletApiV2Client.GetAssetInfoAsync(request.AssetId, token);

            if (response != null)
            {
                result.Body = _mapper.Map<WithdrawalCryptoInfoResponse.Types.Body>(response);
            }

            return result;
        }

        public override async Task<CheckCryptoAddressResponse> IsCryptoAddressValid(CheckCryptoAddressRequest request, ServerCallContext context)
        {
            var result = new CheckCryptoAddressResponse();

            var token = context.GetBearerToken();
            var response = await _walletApiV1Client.HotWalletAddressesValidityAsync(request.AddressExtension,
                request.Address, request.AssetId, token);

            if (response.Result != null)
            {
                result.Body = new CheckCryptoAddressResponse.Types.Body
                {
                    IsValid = response.Result.IsValid
                };
            }

            if (response.Error != null)
            {
                result.Error = response.Error.ToApiError();
            }

            return result;
        }

        public override async Task<EmptyResponse> CryptoCashout(CryptoCashoutRequest request, ServerCallContext context)
        {
            var result = new EmptyResponse();

            var token = context.GetBearerToken();
            var response = await _walletApiV1Client.HotWalletCashoutAsync(_mapper.Map<HotWalletCashoutOperation>(request),
                token, _walletApiConfig.Secret);

            if (response.Error != null)
            {
                result.Error = response.Error.ToApiError();
            }

            return result;
        }

        #endregion

        #region fiat

        public override async Task<SwiftCashoutInfoResponse> GetSwiftCashoutInfo(Empty request, ServerCallContext context)
        {
            var result = new SwiftCashoutInfoResponse();

            var token = context.GetBearerToken();
            var response = await _walletApiV1Client.OffchainGetCashoutSwiftLastDataAsync(token);

            if (response.Result != null)
            {
                result.Body = _mapper.Map<SwiftCashoutInfoResponse.Types.Body>(response.Result);
            }

            if (response.Error != null)
            {
                result.Error = response.Error.ToApiError();
            }

            return result;
        }

        public override async Task<SwiftCashoutFeeResponse> GetSwiftCashoutFee(SwiftCashoutFeeRequest request, ServerCallContext context)
        {
            var result = new SwiftCashoutFeeResponse();

            var token = context.GetBearerToken();
            var response = await _walletApiV1Client.OffchainCashoutSwiftFeeAsync(request.AssetId, request.CountryCode, token);

            if (response.Result != null)
            {
                result.Body = _mapper.Map<SwiftCashoutFeeResponse.Types.Body>(response.Result);
            }

            if (response.Error != null)
            {
                result.Error = response.Error.ToApiError();
            }

            return result;
        }

        public override async Task<SwiftCashoutResponse> SwiftCashout(SwiftCashoutRequest request, ServerCallContext context)
        {
            var result = new SwiftCashoutResponse();

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
                    result.Body = new SwiftCashoutResponse.Types.Body
                    {
                        TransferId = finalizeResponse.Result.TransferId
                    };
                }

                if (response.Error != null)
                {
                    result.Error = response.Error.ToApiError();
                }
            }

            if (response.Error != null)
            {
                result.Error = response.Error.ToApiError();
            }

            return result;
        }

        #endregion
    }
}

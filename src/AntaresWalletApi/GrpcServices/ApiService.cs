using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using AntaresWalletApi.Extensions;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Lykke.ApiClients.V1;
using Lykke.Service.ClientAccount.Client.Models;
using Microsoft.AspNetCore.Authorization;
using Newtonsoft.Json;
using Swisschain.Lykke.AntaresWalletApi.ApiContract;
using ApiExceptionV1 = Lykke.ApiClients.V1.ApiException;
using ApiExceptionV2 = Lykke.ApiClients.V2.ApiException;
using CashOutFee = Swisschain.Lykke.AntaresWalletApi.ApiContract.CashOutFee;
using Status = Grpc.Core.Status;

namespace AntaresWalletApi.GrpcServices
{
    public partial class ApiService
    {
        public override async Task<PendingActionsResponse> GetPendingActions(Empty request, ServerCallContext context)
        {
            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV1Client.ClientGetPendingActionsAsync(token);

                var result = new PendingActionsResponse();

                if (response.Result != null)
                {
                    result.Result = _mapper.Map<PendingActionsResponse.Types.PendingActionsPayload>(response.Result);
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

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        public override async Task<FundsResponse> GetFunds(FundsRequest request, ServerCallContext context)
        {
            var result = new FundsResponse();

            try
            {
                var token = context.GetBearerToken();
                var wallets = await _clientAccountClient.Wallets.GetClientWalletsFilteredAsync(context.GetClientId(), WalletType.Trading);

                var walletId = wallets.FirstOrDefault()?.Id;

                var response = await _walletApiV2Client.GetFundsByWalletIdAsync(
                    walletId, null, request.AssetId, request.Take, request.Skip,
                    request.OptionalFromDateCase == FundsRequest.OptionalFromDateOneofCase.None ? (DateTimeOffset?) null : request.From.ToDateTimeOffset(),
                    request.OptionalToDateCase == FundsRequest.OptionalToDateOneofCase.None ? (DateTimeOffset?) null : request.To.ToDateTimeOffset(),
                    token);

                if (response != null)
                {
                    result.Funds.AddRange(_mapper.Map<List<FundsResponse.Types.FundsModel>>(response));
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

        public override async Task<ExplorerLinksResponse> GetExplorerLinks(ExplorerLinksRequest request, ServerCallContext context)
        {
            var result = new ExplorerLinksResponse();

            try
            {
                var token = context.GetBearerToken();
                var wallets = await _clientAccountClient.Wallets.GetClientWalletsFilteredAsync(context.GetClientId(), WalletType.Trading);

                var walletId = wallets.FirstOrDefault()?.Id;

                var response = await _walletApiV2Client.GetExplorerLinksAsync(request.AssetId, request.TransactionHash,
                    token);

                if (response != null)
                {
                    result.Links.AddRange(_mapper.Map<List<ExplorerLinksResponse.Types.ExplorerLinkModel>>(response.Links));
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

        public override async Task<WalletsResponse> GetWallets(Empty request, ServerCallContext context)
        {
            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV1Client.WalletsGetAsync(token);

                var result = new WalletsResponse();

                if (response.Result != null)
                {
                    result.Result = new WalletsResponse.Types.GetWalletsPayload
                    {
                        Lykke = new WalletsResponse.Types.LykkeWalletsPayload{Equity = response.Result.Lykke.Equity.ToString(CultureInfo.InvariantCulture)},
                        MultiSig = response.Result.MultiSig,
                        ColoredMultiSig = response.Result.ColoredMultiSig,
                        SolarCoinAddress = response.Result.SolarCoinAddress
                    };
                    result.Result.Lykke.Assets.AddRange(_mapper.Map<List<WalletsResponse.Types.WalletAsset>>(response.Result.Lykke.Assets));
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

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        public override async Task<WalletResponse> GetWallet(WalletRequest request, ServerCallContext context)
        {
            var result = new WalletResponse();

            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV1Client.WalletsGetByIdAsync(request.AssetId, token);

                if (response.Result != null)
                {
                    result.Result = _mapper.Map<WalletResponse.Types.WalletPayload>(response.Result);
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
                    result = JsonConvert.DeserializeObject<WalletResponse>(ex.Response);
                    return result;
                }

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }


        [AllowAnonymous]
        public override async Task<CountryPhoneCodesResponse> GetCountryPhoneCodes(Empty request, ServerCallContext context)
        {
            var result = new CountryPhoneCodesResponse();

            try
            {
                var response = await _walletApiV1Client.GetCountryPhoneCodesAsync();

                if (response.Result != null)
                {
                    result.Result = new CountryPhoneCodesResponse.Types.CountryPhoneCodes
                    {
                        Current = response.Result.Current
                    };
                    result.Result.CountriesList.AddRange(_mapper.Map<List<Country>>(response.Result.CountriesList));
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
                    result = JsonConvert.DeserializeObject<CountryPhoneCodesResponse>(ex.Response);
                    return result;
                }

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        public override async Task<EthereumSettingsResponse> GetEthereumSettings(Empty request, ServerCallContext context)
        {
            var result = new EthereumSettingsResponse();

            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV1Client.GetEthereumPrivateWalletSettingsAsync(token);

                if (response.Result != null)
                {
                    result.Result = _mapper.Map<EthereumSettingsResponse.Types.EthereumSettings>(response.Result);
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
                    result = JsonConvert.DeserializeObject<EthereumSettingsResponse>(ex.Response);
                    return result;
                }

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        public override async Task<AppSettingsResponse> GetAppSettings(Empty request, ServerCallContext context)
        {
            var result = new AppSettingsResponse();

            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV1Client.GetAppSettingsAsync(token);

                if (response.Result != null)
                {
                    result.Result = _mapper.Map<AppSettingsResponse.Types.AppSettingsData>(response.Result);

                    result.Result.FeeSettings.CashOut.AddRange(
                        _mapper.Map<CashOutFee[]>(response.Result.FeeSettings.CashOut?.ToArray() ??
                                                  Array.Empty<Lykke.ApiClients.V1.CashOutFee>()));
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
                    result = JsonConvert.DeserializeObject<AppSettingsResponse>(ex.Response);
                    return result;
                }

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        public override async Task<PrivateWalletsResponse> GetPrivateWallets(Empty request, ServerCallContext context)
        {
            var result = new PrivateWalletsResponse();

            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV1Client.PrivateWalletGetAsync(token);

                if (response.Result != null)
                {
                    result.Result = new PrivateWalletsResponse.Types.PrivateWalletsPayload();

                    foreach (var wallet in response.Result.Wallets)
                    {
                        var res = _mapper.Map<PrivateWallet>(wallet);
                        res.Balances.AddRange(_mapper.Map<List<BalanceRecord>>(wallet.Balances));
                        result.Result.Wallets.Add(res);
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
                    result = JsonConvert.DeserializeObject<PrivateWalletsResponse>(ex.Response);
                    return result;
                }

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        public override async Task<AssetDisclaimersResponse> GetAssetDisclaimers(Empty request, ServerCallContext context)
        {
            var result = new AssetDisclaimersResponse();

            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV1Client.GetAssetDisclaimersAsync(token);

                if (response.Result != null)
                {
                    result.Result = new AssetDisclaimersResponse.Types.AssetDisclaimersPayload();

                    foreach (var disclaimer in response.Result.Disclaimers)
                    {
                        var res = new AssetDisclaimer{Id = disclaimer.Id, Text = disclaimer.Text};
                        result.Result.Disclaimers.Add(res);
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
                    result = JsonConvert.DeserializeObject<AssetDisclaimersResponse>(ex.Response);
                    return result;
                }

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        public override async Task<EmptyResponse> ApproveAssetDisclaimer(AssetDisclaimerRequest request, ServerCallContext context)
        {
            var result = new EmptyResponse();

            try
            {
                var token = context.GetBearerToken();

                var response = await _walletApiV1Client.ApproveAssetDisclaimerAsync(request.DisclaimerId, token);

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

        public override async Task<EmptyResponse> DeclineAssetDisclaimer(AssetDisclaimerRequest request, ServerCallContext context)
        {
            var result = new EmptyResponse();

            try
            {
                var token = context.GetBearerToken();

                var response = await _walletApiV1Client.DeclineAssetDisclaimerAsync(request.DisclaimerId, token);

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
    }
}

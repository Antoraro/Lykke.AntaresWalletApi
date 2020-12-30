using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using AntaresWalletApi.Extensions;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Lykke.Service.ClientAccount.Client.Models;
using Microsoft.AspNetCore.Authorization;
using Swisschain.Lykke.AntaresWalletApi.ApiContract;
using ApiExceptionV1 = Lykke.ApiClients.V1.ApiException;
using ApiExceptionV2 = Lykke.ApiClients.V2.ApiException;
using CashOutFee = Swisschain.Lykke.AntaresWalletApi.ApiContract.CashOutFee;

namespace AntaresWalletApi.GrpcServices
{
    public partial class ApiService
    {
        public override async Task<PendingActionsResponse> GetPendingActions(Empty request, ServerCallContext context)
        {
            var token = context.GetBearerToken();
            var response = await _walletApiV1Client.ClientGetPendingActionsAsync(token);

            var result = new PendingActionsResponse();

            if (response.Result != null)
            {
                result.Body = _mapper.Map<PendingActionsResponse.Types.Body>(response.Result);
            }

            if (response.Error != null)
            {
                result.Error = response.Error.ToApiError();
            }

            return result;
        }

        public override async Task<FundsResponse> GetFunds(FundsRequest request, ServerCallContext context)
        {
            var result = new FundsResponse();

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
                result.Body = new FundsResponse.Types.Body();
                result.Body.Funds.AddRange(_mapper.Map<List<FundsResponse.Types.FundsModel>>(response));
            }

            return result;
        }

        public override async Task<ExplorerLinksResponse> GetExplorerLinks(ExplorerLinksRequest request, ServerCallContext context)
        {
            var result = new ExplorerLinksResponse();

            var token = context.GetBearerToken();

            var response = await _walletApiV2Client.GetExplorerLinksAsync(request.AssetId, request.TransactionHash,
                token);

            if (response != null)
            {
                result.Body = new ExplorerLinksResponse.Types.Body();
                result.Body.Links.AddRange(_mapper.Map<List<ExplorerLinksResponse.Types.ExplorerLinkModel>>(response.Links));
            }

            return result;
        }

        public override async Task<WalletsResponse> GetWallets(Empty request, ServerCallContext context)
        {
            var token = context.GetBearerToken();
            var response = await _walletApiV1Client.WalletsGetAsync(token);

            var result = new WalletsResponse();

            if (response.Result != null)
            {
                result.Body = new WalletsResponse.Types.Body
                {
                    Lykke = new WalletsResponse.Types.LykkeWalletsPayload{Equity = response.Result.Lykke.Equity.ToString(CultureInfo.InvariantCulture)},
                    MultiSig = response.Result.MultiSig,
                    ColoredMultiSig = response.Result.ColoredMultiSig,
                    SolarCoinAddress = response.Result.SolarCoinAddress
                };

                result.Body.Lykke.Assets.AddRange(_mapper.Map<List<WalletsResponse.Types.WalletAsset>>(response.Result.Lykke.Assets));
            }

            if (response.Error != null)
            {
                result.Error = response.Error.ToApiError();
            }

            return result;
        }

        public override async Task<WalletResponse> GetWallet(WalletRequest request, ServerCallContext context)
        {
            var result = new WalletResponse();

            var token = context.GetBearerToken();
            var response = await _walletApiV1Client.WalletsGetByIdAsync(request.AssetId, token);

            if (response.Result != null)
            {
                result.Body = _mapper.Map<WalletResponse.Types.Body>(response.Result);
            }

            if (response.Error != null)
            {
                result.Error = response.Error.ToApiError();
            }

            return result;
        }


        [AllowAnonymous]
        public override async Task<CountryPhoneCodesResponse> GetCountryPhoneCodes(Empty request, ServerCallContext context)
        {
            var result = new CountryPhoneCodesResponse();

            var response = await _walletApiV1Client.GetCountryPhoneCodesAsync();

            if (response.Result != null)
            {
                result.Body = new CountryPhoneCodesResponse.Types.Body
                {
                    Current = response.Result.Current
                };

                result.Body.CountriesList.AddRange(_mapper.Map<List<Country>>(response.Result.CountriesList));
            }

            if (response.Error != null)
            {
                result.Error = response.Error.ToApiError();
            }

            return result;
        }

        public override async Task<EthereumSettingsResponse> GetEthereumSettings(Empty request, ServerCallContext context)
        {
            var result = new EthereumSettingsResponse();

            var token = context.GetBearerToken();
            var response = await _walletApiV1Client.GetEthereumPrivateWalletSettingsAsync(token);

            if (response.Result != null)
            {
                result.Body = _mapper.Map<EthereumSettingsResponse.Types.Body>(response.Result);
            }

            if (response.Error != null)
            {
                result.Error = response.Error.ToApiError();
            }

            return result;
        }

        public override async Task<AppSettingsResponse> GetAppSettings(Empty request, ServerCallContext context)
        {
            var result = new AppSettingsResponse();

            var token = context.GetBearerToken();
            var response = await _walletApiV1Client.GetAppSettingsAsync(token);

            if (response.Result != null)
            {
                result.Body = _mapper.Map<AppSettingsResponse.Types.Body>(response.Result);

                result.Body.FeeSettings.CashOut.AddRange(
                    _mapper.Map<CashOutFee[]>(response.Result.FeeSettings.CashOut?.ToArray() ??
                                              Array.Empty<Lykke.ApiClients.V1.CashOutFee>()));
            }

            if (response.Error != null)
            {
                result.Error = response.Error.ToApiError();
            }

            return result;
        }

        public override async Task<PrivateWalletsResponse> GetPrivateWallets(Empty request, ServerCallContext context)
        {
            var result = new PrivateWalletsResponse();

            var token = context.GetBearerToken();
            var response = await _walletApiV1Client.PrivateWalletGetAsync(token);

            if (response.Result != null)
            {
                result.Body = new PrivateWalletsResponse.Types.Body();

                foreach (var wallet in response.Result.Wallets)
                {
                    var res = _mapper.Map<PrivateWallet>(wallet);
                    res.Balances.AddRange(_mapper.Map<List<BalanceRecord>>(wallet.Balances));
                    result.Body.Wallets.Add(res);
                }
            }

            if (response.Error != null)
            {
                result.Error = response.Error.ToApiError();
            }

            return result;
        }

        public override async Task<AssetDisclaimersResponse> GetAssetDisclaimers(Empty request, ServerCallContext context)
        {
            var result = new AssetDisclaimersResponse();

            var token = context.GetBearerToken();
            var response = await _walletApiV1Client.GetAssetDisclaimersAsync(token);

            if (response.Result != null)
            {
                result.Body = new AssetDisclaimersResponse.Types.Body();

                foreach (var disclaimer in response.Result.Disclaimers)
                {
                    var res = new AssetDisclaimer{Id = disclaimer.Id, Text = disclaimer.Text};
                    result.Body.Disclaimers.Add(res);
                }
            }

            if (response.Error != null)
            {
                result.Error = response.Error.ToApiError();
            }

            return result;
        }

        public override async Task<EmptyResponse> ApproveAssetDisclaimer(AssetDisclaimerRequest request, ServerCallContext context)
        {
            var result = new EmptyResponse();

            var token = context.GetBearerToken();

            var response = await _walletApiV1Client.ApproveAssetDisclaimerAsync(request.DisclaimerId, token);

            if (response.Error != null)
            {
                result.Error = response.Error.ToApiError();
            }

            return result;
        }

        public override async Task<EmptyResponse> DeclineAssetDisclaimer(AssetDisclaimerRequest request, ServerCallContext context)
        {
            var result = new EmptyResponse();

            var token = context.GetBearerToken();

            var response = await _walletApiV1Client.DeclineAssetDisclaimerAsync(request.DisclaimerId, token);

            if (response.Error != null)
            {
                result.Error = response.Error.ToApiError();
            }

            return result;
        }
    }
}

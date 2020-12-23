using System;
using System.Threading.Tasks;
using AntaresWalletApi.Extensions;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Lykke.ApiClients.V1;
using Lykke.ApiClients.V2;
using Newtonsoft.Json;
using Swisschain.Lykke.AntaresWalletApi.ApiContract;
using ApiException = Lykke.ApiClients.V1.ApiException;
using Status = Grpc.Core.Status;

namespace AntaresWalletApi.GrpcServices
{
    public partial class ApiService
    {
        public override async Task<PushSettingsResponse> GetPushSettings(Empty request, ServerCallContext context)
        {
            var result = new PushSettingsResponse();

            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV1Client.PushSettingsGetAsync(token);

                if (response.Result != null)
                {
                    result.Result = new PushSettingsResponse.Types.PushSettingsPayload
                    {
                        Enabled = response.Result.Enabled
                    };
                }

                if (response.Error != null)
                {
                    result.Error = _mapper.Map<ErrorV1>(response.Error);
                }

                return result;
            }
            catch (ApiException ex)
            {
                if (ex.StatusCode == 401)
                    throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid token"));

                if (ex.StatusCode == 500)
                {
                    result = JsonConvert.DeserializeObject<PushSettingsResponse>(ex.Response);
                    return result;
                }

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        public override async Task<EmptyResponse> SetPushSettings(PushSettingsRequest request, ServerCallContext context)
        {
            var result = new EmptyResponse();

            try
            {
                var token = context.GetBearerToken();

                var response = await _walletApiV1Client.PushSettingsPostAsync(new PushNotificationsSettingsModel{Enabled = request.Enabled}, token);

                if (response.Error != null)
                {
                    result.Error = _mapper.Map<ErrorV1>(response.Error);
                }

                return result;
            }
            catch (ApiException ex)
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

        public override async Task<RegisterPushResponse> RegisterPushNotifications(RegisterPushRequest request, ServerCallContext context)
        {
            var result = new RegisterPushResponse();

            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV2Client.RegisterInstallationAsync(new PushRegistrationModel
                {
                    InstallationId = Guid.NewGuid().ToString(),
                    Platform = request.Platform == MobileOsPlatform.Ios ? PushRegistrationModelPlatform.Ios : PushRegistrationModelPlatform.Android,
                    PushChannel = request.PushChannel
                }, token);

                if (response != null)
                {
                    result.Result = new RegisterPushResponse.Types.InstallationPayload
                    {
                        InstallationId = response.InstallationId
                    };
                }

                return result;
            }
            catch (Lykke.ApiClients.V2.ApiException ex)
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

    }
}

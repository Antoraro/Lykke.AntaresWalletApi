using System;
using System.Threading.Tasks;
using AntaresWalletApi.Extensions;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Lykke.ApiClients.V1;
using Lykke.ApiClients.V2;
using Lykke.Service.PushNotifications.Client.Models;
using Swisschain.Lykke.AntaresWalletApi.ApiContract;

namespace AntaresWalletApi.GrpcServices
{
    public partial class ApiService
    {
        public override async Task<PushSettingsResponse> GetPushSettings(Empty request, ServerCallContext context)
        {
            var result = new PushSettingsResponse();

            var token = context.GetBearerToken();
            var response = await _walletApiV1Client.PushSettingsGetAsync(token);

            if (response.Result != null)
            {
                result.Body = new PushSettingsResponse.Types.Body
                {
                    Enabled = response.Result.Enabled
                };
            }

            if (response.Error != null)
            {
                result.Error = response.Error.ToApiError();
            }

            return result;
        }

        public override async Task<EmptyResponse> SetPushSettings(PushSettingsRequest request, ServerCallContext context)
        {
            var result = new EmptyResponse();

            var token = context.GetBearerToken();

            var response = await _walletApiV1Client.PushSettingsPostAsync(new PushNotificationsSettingsModel{Enabled = request.Enabled}, token);

            if (response.Error != null)
            {
                result.Error = response.Error.ToApiError();
            }

            return result;
        }

        public override async Task<EmptyResponse> RegisterPushNotifications(RegisterPushRequest request, ServerCallContext context)
        {
            var result = new EmptyResponse();

            var clientId = context.GetClientId();

            var clientInfo = await _clientAccountClient.ClientAccountInformation.GetByIdAsync(clientId);

            var sessionId = context.GetSessionId();

            await _pushNotificationsClient.FcmTokens.RegisterAsync(new FcmTokenModel
            {
                NotificationId = clientInfo.NotificationsId,
                ClientId = clientId,
                SessionId = sessionId,
                FcmToken = request.FcmToken
            });

            return result;
        }
    }
}

using System.Threading.Tasks;
using AntaresWalletApi.Common.Domain;
using AntaresWalletApi.Extensions;
using Common;
using Grpc.Core;
using Lykke.ApiClients.V1;
using Lykke.Common.Extensions;
using Lykke.Service.Registration.Contract.Client.Enums;
using Microsoft.AspNetCore.Authorization;
using Swisschain.Lykke.AntaresWalletApi.ApiContract;
using ApiExceptionV1 = Lykke.ApiClients.V1.ApiException;
using ApiExceptionV2 = Lykke.ApiClients.V2.ApiException;

namespace AntaresWalletApi.GrpcServices
{
    public partial class ApiService
    {
        [AllowAnonymous]
        public override async Task<LoginResponse> Login(LoginRequest request, ServerCallContext context)
        {
            var validateResult = ValidateLoginRequest(request);

            if (validateResult != null)
                return validateResult;

            var result = new LoginResponse();

            var response = await _registrationServiceClient.LoginApi.AuthenticateAsync(new Lykke.Service.Registration.Contract.Client.Models.AuthenticateModel
            {
                Email = request.Email,
                Password = request.Password,
                Ip = context.GetHttpContext().GetIp(),
                UserAgent = context.GetHttpContext().GetUserAgent()
            });

            if (response.Status == AuthenticationStatus.Error)
            {
                result.Error = new ErrorResponseBody
                {
                    Code = ErrorCode.Unauthorized,
                    Message = response.ErrorMessage
                };

                return result;
            }

            string sessionId = await _sessionService.CreateSessionAsync(response.Token, request.PublicKey);

            result.Body = new LoginResponse.Types.Body
            {
                SessionId = sessionId,
                NotificationId = response.NotificationsId
            };

            return result;
        }

        [AllowAnonymous]
        public override async Task<EmptyResponse> SendLoginSms(LoginSmsRequest request, ServerCallContext context)
        {
            var result = new EmptyResponse();

            var session = _sessionService.GetSession(request.SessionId);

            if (session == null)
            {
                result.Error = new ErrorResponseBody
                {
                    Code = ErrorCode.InvalidField,
                    Message = ErrorMessages.InvalidFieldValue(nameof(request.SessionId))
                };

                result.Error.Fields.Add(nameof(request.SessionId),result.Error.Message);

                return result;
            }

            var response = await _walletApiV1Client.RequestCodesAsync($"Bearer {session.Token}");

            if (response.Error != null)
            {
                result.Error = response.Error.ToApiError();
            }

            return result;
        }

        [AllowAnonymous]
        public override async Task<VerifyLoginSmsResponse> VerifyLoginSms(VerifyLoginSmsRequest request, ServerCallContext context)
        {
            var result = new VerifyLoginSmsResponse();

            var session = _sessionService.GetSession(request.SessionId);

            if (session == null)
            {
                result.Error = new ErrorResponseBody
                {
                    Code = ErrorCode.InvalidField,
                    Message = ErrorMessages.InvalidFieldValue(nameof(request.SessionId))
                };

                result.Error.Fields.Add(nameof(request.SessionId),result.Error.Message);

                return result;
            }

            var response = await _walletApiV1Client.SubmitCodeAsync(new SubmitCodeModel
            {
                Code = request.Code
            }, $"Bearer {session.Token}");

            if (response.Result != null)
            {
                result.Body = new VerifyLoginSmsResponse.Types.Body{Passed = true};
                session.Sms = true;

                if (session.Pin)
                    session.Verified = true;

                await _sessionService.SaveSessionAsync(session);
            }

            if (response.Error != null)
            {
                var error = response.Error.ToApiError();

                if (response.Error.Code == ErrorModelCode.WrongConfirmationCode)
                {
                    result.Body = new VerifyLoginSmsResponse.Types.Body{Passed = false};
                    return result;
                }

                result.Error = error;
            }

            return result;
        }

        [AllowAnonymous]
        public override async Task<CheckPinResponse> CheckPin(CheckPinRequest request, ServerCallContext context)
        {
            var result = new CheckPinResponse();

            var session = _sessionService.GetSession(request.SessionId);

            if (session == null)
            {
                result.Error = new ErrorResponseBody
                {
                    Code = ErrorCode.InvalidField,
                    Message = ErrorMessages.InvalidFieldValue(nameof(request.SessionId))
                };

                result.Error.Fields.Add(nameof(request.SessionId),result.Error.Message);

                return result;
            }

            var response = await _walletApiV1Client.PinSecurityCheckPinCodePostAsync(new PinSecurityCheckRequestModel
            {
                Pin = request.Pin
            }, $"Bearer {session.Token}");

            if (response.Result != null)
            {
                result.Body = new CheckPinResponse.Types.Body{Passed = response.Result.Passed};

                if (result.Body.Passed)
                {
                    if (session.Verified)
                    {
                        await _sessionService.ProlongateSessionAsync(session);
                    }
                    else
                    {
                        session.Pin = true;

                        if (session.Sms)
                            session.Verified = true;

                        await _sessionService.SaveSessionAsync(session);
                    }
                }
            }

            if (response.Error != null)
            {
                result.Error = response.Error.ToApiError();
            }

            return result;
        }

        private LoginResponse ValidateLoginRequest(LoginRequest request)
        {
            var errorResponse = new LoginResponse {Error = new ErrorResponseBody()};

            if (string.IsNullOrEmpty(request.Email))
            {
                errorResponse.Error.Code = ErrorCode.InvalidField;
                errorResponse.Error.Message = ErrorMessages.CantBeEmpty(nameof(request.Email));
                errorResponse.Error.Fields.Add(nameof(request.Email), errorResponse.Error.Message);
                return errorResponse;
            }

            if (!request.Email.IsValidEmailAndRowKey())
            {
                errorResponse.Error.Code = ErrorCode.InvalidField;
                errorResponse.Error.Message = ErrorMessages.InvalidFieldValue(nameof(request.Email));
                errorResponse.Error.Fields.Add(nameof(request.Email), errorResponse.Error.Message);
                return errorResponse;
            }

            if (string.IsNullOrEmpty(request.Password))
            {
                errorResponse.Error.Code = ErrorCode.InvalidField;
                errorResponse.Error.Message = ErrorMessages.CantBeEmpty(nameof(request.Password));
                errorResponse.Error.Fields.Add(nameof(request.Password), errorResponse.Error.Message);
                return errorResponse;
            }

            return null;
        }
    }
}

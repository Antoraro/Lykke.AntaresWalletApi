using System;
using System.Threading.Tasks;
using AntaresWalletApi.Common.Domain;
using Common;
using Grpc.Core;
using Lykke.ApiClients.V1;
using Lykke.Common.Extensions;
using Lykke.Service.Registration.Contract.Client.Enums;
using Microsoft.AspNetCore.Authorization;
using Newtonsoft.Json;
using Swisschain.Lykke.AntaresWalletApi.ApiContract;
using ApiExceptionV1 = Lykke.ApiClients.V1.ApiException;
using ApiExceptionV2 = Lykke.ApiClients.V2.ApiException;
using Status = Grpc.Core.Status;

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

            try
            {
                var response = await _registrationServiceClient.LoginApi.AuthenticateAsync(new Lykke.Service.Registration.Contract.Client.Models.AuthenticateModel
                {
                    Email = request.Email,
                    Password = request.Password,
                    Ip = context.GetHttpContext().GetIp(),
                    UserAgent = context.GetHttpContext().GetUserAgent()
                });

                if (response.Status == AuthenticationStatus.Error)
                {
                    result.Error = new ErrorV1
                    {
                        Code = "2",
                        Message = response.ErrorMessage
                    };

                    return result;
                }

                string sessionId = await _sessionService.CreateSessionAsync(response.Token, request.PublicKey);

                result.Result = new LoginResponse.Types.LoginPayload
                {
                    SessionId = sessionId,
                    NotificationId = response.NotificationsId
                };

                return result;
            }
            catch (Exception ex)
            {
                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        [AllowAnonymous]
        public override async Task<EmptyResponse> SendLoginSms(LoginSmsRequest request, ServerCallContext context)
        {
            var result = new EmptyResponse();

            var session = _sessionService.GetSession(request.SessionId);

            if (session == null)
            {
                result.Error = new ErrorV1
                {
                    Code = ErrorModelCode.InvalidInputField.ToString(),
                    Message = ErrorMessages.InvalidFieldValue(nameof(request.SessionId)),
                    Field = nameof(request.SessionId)
                };

                return result;
            }

            try
            {
                var response = await _walletApiV1Client.RequestCodesAsync($"Bearer {session.Token}");

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

        [AllowAnonymous]
        public override async Task<VerifyLoginSmsResponse> VerifyLoginSms(VerifyLoginSmsRequest request, ServerCallContext context)
        {
            var result = new VerifyLoginSmsResponse();

            var session = _sessionService.GetSession(request.SessionId);

            if (session == null)
            {
                result.Error = new ErrorV1
                {
                    Code = ErrorModelCode.InvalidInputField.ToString(),
                    Message = ErrorMessages.InvalidFieldValue(nameof(request.SessionId)),
                    Field = nameof(request.SessionId)
                };

                return result;
            }

            try
            {
                var response = await _walletApiV1Client.SubmitCodeAsync(new SubmitCodeModel
                {
                    Code = request.Code
                }, $"Bearer {session.Token}");

                if (response.Result != null)
                {
                    result.Result = new VerifyLoginSmsResponse.Types.VerifyLoginSmsPayload{Passed = true};
                    session.Sms = true;

                    if (session.Pin)
                        session.Verified = true;

                    await _sessionService.SaveSessionAsync(session);
                }

                if (response.Error != null)
                {
                    var error = _mapper.Map<ErrorV1>(response.Error);

                    if (error.Code == "WrongConfirmationCode")
                    {
                        result.Result = new VerifyLoginSmsResponse.Types.VerifyLoginSmsPayload{Passed = false};
                        return result;
                    }

                    result.Error = error;
                }

                return result;
            }
            catch (ApiExceptionV1 ex)
            {
                if (ex.StatusCode == 401)
                    throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid token"));

                if (ex.StatusCode == 500)
                {
                    result = JsonConvert.DeserializeObject<VerifyLoginSmsResponse>(ex.Response);
                    return result;
                }

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        [AllowAnonymous]
        public override async Task<CheckPinResponse> CheckPin(CheckPinRequest request, ServerCallContext context)
        {
            var result = new CheckPinResponse();

            var session = _sessionService.GetSession(request.SessionId);

            if (session == null)
            {
                result.Error = new ErrorV1
                {
                    Code = ErrorModelCode.InvalidInputField.ToString(),
                    Message = ErrorMessages.InvalidFieldValue(nameof(request.SessionId)),
                    Field = nameof(request.SessionId)
                };

                return result;
            }

            try
            {
                var response = await _walletApiV1Client.PinSecurityCheckPinCodePostAsync(new PinSecurityCheckRequestModel
                {
                    Pin = request.Pin
                }, $"Bearer {session.Token}");

                if (response.Result != null)
                {
                    result.Result = new CheckPinResponse.Types.CheckPinPayload{Passed = response.Result.Passed};

                    if (result.Result.Passed)
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
                    result = JsonConvert.DeserializeObject<CheckPinResponse>(ex.Response);
                    return result;
                }

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        private LoginResponse ValidateLoginRequest(LoginRequest request)
        {
            if (string.IsNullOrEmpty(request.Email))
                return new LoginResponse
                {
                    Error = new ErrorV1
                    {
                        Code = ErrorModelCode.InvalidInputField.ToString(),
                        Message = ErrorMessages.CantBeEmpty(nameof(request.Email)),
                        Field = nameof(request.Email)
                    }
                };

            if (!request.Email.IsValidEmailAndRowKey())
                return new LoginResponse
                {
                    Error = new ErrorV1
                    {
                        Code = ErrorModelCode.InvalidInputField.ToString(),
                        Message = ErrorMessages.InvalidFieldValue(nameof(request.Email)),
                        Field = nameof(request.Email)
                    }
                };

            if (string.IsNullOrEmpty(request.Password))
                return new LoginResponse
                {
                    Error = new ErrorV1
                    {
                        Code = ErrorModelCode.InvalidInputField.ToString(),
                        Message = ErrorMessages.CantBeEmpty(nameof(request.Password)),
                        Field = nameof(request.Password)
                    }
                };

            return null;
        }
    }
}

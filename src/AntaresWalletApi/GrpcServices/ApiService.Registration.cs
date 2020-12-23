using System.Threading.Tasks;
using Grpc.Core;
using Lykke.ApiClients.V1;
using Microsoft.AspNetCore.Authorization;
using Newtonsoft.Json;
using Swisschain.Lykke.AntaresWalletApi.ApiContract;
using Status = Grpc.Core.Status;
using ApiExceptionV1 = Lykke.ApiClients.V1.ApiException;
using ApiExceptionV2 = Lykke.ApiClients.V2.ApiException;

namespace AntaresWalletApi.GrpcServices
{
    public partial class ApiService
    {
        [AllowAnonymous]
        public override async Task<VerificationEmailResponse> SendVerificationEmail(VerificationEmailRequest request, ServerCallContext context)
        {
            var result = new VerificationEmailResponse();

            try
            {
                var response = await _walletApiV1Client.SendVerificationEmailAsync(new PostEmailModel{Email = request.Email});

                if (response.Result != null)
                {
                    result.Result = new VerificationEmailResponse.Types.VerificationEmailPayload { Token = response.Result.Token };
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
                    result = JsonConvert.DeserializeObject<VerificationEmailResponse>(ex.Response);
                    return result;
                }

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        [AllowAnonymous]
        public override async Task<EmptyResponse> SendVerificationSms(VerificationSmsRequest request, ServerCallContext context)
        {
            var result = new EmptyResponse();

            try
            {
                var response = await _walletApiV1Client.SendVerificationSmsAsync(new PostPhoneModel{PhoneNumber = request.Phone, Token = request.Token});

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
        public override async Task<VerifyResponse> VerifyEmail(VerifyEmailRequest request, ServerCallContext context)
        {
            var result = new VerifyResponse();

            try
            {
                var response = await _walletApiV1Client.VerifyEmailAsync(new VerifyEmailRequestModel
                {
                    Email = request.Email,
                    Code = request.Code,
                    Token = request.Token
                });

                if (response.Result != null)
                {
                    result.Result = new VerifyResponse.Types.VerifyPayload{ Passed = response.Result.Passed};
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
                    result = JsonConvert.DeserializeObject<VerifyResponse>(ex.Response);
                    return result;
                }

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        [AllowAnonymous]
        public override async Task<VerifyResponse> VerifyPhone(VerifyPhoneRequest request, ServerCallContext context)
        {
            var result = new VerifyResponse();

            try
            {
                var response = await _walletApiV1Client.VerifyPhoneAsync(new VerifyPhoneModel
                {
                    PhoneNumber = request.Phone,
                    Code = request.Code,
                    Token = request.Token
                });

                if (response.Result != null)
                {
                    result.Result = new VerifyResponse.Types.VerifyPayload{ Passed = response.Result.Passed};
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
                    result = JsonConvert.DeserializeObject<VerifyResponse>(ex.Response);
                    return result;
                }

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        [AllowAnonymous]
        public override async Task<RegisterResponse> Register(RegisterRequest request, ServerCallContext context)
        {
            var result = new RegisterResponse();

            try
            {
                var response = await _walletApiV1Client.RegisterAsync(new RegistrationModel
                {
                    FullName = request.FullName,
                    Email = request.Email,
                    Phone = request.Phone,
                    Password = request.Password,
                    Hint = request.Hint,
                    CountryIso3Poa = request.CountryIso3Code,
                    Pin = request.Pin,
                    Token = request.Token
                });

                if (response.Result != null)
                {
                    result.Result = _mapper.Map<RegisterResponse.Types.RegisterPayload>(response.Result);
                    string sessionId = await _sessionService.CreateVerifiedSessionAsync(response.Result.Token, request.PublicKey);
                    result.Result.SessionId = sessionId;
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
                    result = JsonConvert.DeserializeObject<RegisterResponse>(ex.Response);
                    return result;
                }

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }
    }
}

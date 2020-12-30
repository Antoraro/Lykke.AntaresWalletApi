using System.Threading.Tasks;
using AntaresWalletApi.Extensions;
using Grpc.Core;
using Lykke.ApiClients.V1;
using Microsoft.AspNetCore.Authorization;
using Swisschain.Lykke.AntaresWalletApi.ApiContract;

namespace AntaresWalletApi.GrpcServices
{
    public partial class ApiService
    {
        [AllowAnonymous]
        public override async Task<VerificationEmailResponse> SendVerificationEmail(VerificationEmailRequest request, ServerCallContext context)
        {
            var result = new VerificationEmailResponse();

            var response = await _walletApiV1Client.SendVerificationEmailAsync(new PostEmailModel{Email = request.Email});

            if (response.Result != null)
            {
                result.Body = new VerificationEmailResponse.Types.Body { Token = response.Result.Token };
            }

            if (response.Error != null)
            {
                result.Error = response.Error.ToApiError();
            }

            return result;
        }

        [AllowAnonymous]
        public override async Task<EmptyResponse> SendVerificationSms(VerificationSmsRequest request, ServerCallContext context)
        {
            var result = new EmptyResponse();

            var response = await _walletApiV1Client.SendVerificationSmsAsync(new PostPhoneModel{PhoneNumber = request.Phone, Token = request.Token});

            if (response.Error != null)
            {
                result.Error = response.Error.ToApiError();
            }

            return result;
        }

        [AllowAnonymous]
        public override async Task<VerifyResponse> VerifyEmail(VerifyEmailRequest request, ServerCallContext context)
        {
            var result = new VerifyResponse();

            var response = await _walletApiV1Client.VerifyEmailAsync(new VerifyEmailRequestModel
            {
                Email = request.Email,
                Code = request.Code,
                Token = request.Token
            });

            if (response.Result != null)
            {
                result.Body = new VerifyResponse.Types.Body{ Passed = response.Result.Passed};
            }

            if (response.Error != null)
            {
                result.Error = response.Error.ToApiError();
            }

            return result;
        }

        [AllowAnonymous]
        public override async Task<VerifyResponse> VerifyPhone(VerifyPhoneRequest request, ServerCallContext context)
        {
            var result = new VerifyResponse();

            var response = await _walletApiV1Client.VerifyPhoneAsync(new VerifyPhoneModel
            {
                PhoneNumber = request.Phone,
                Code = request.Code,
                Token = request.Token
            });

            if (response.Result != null)
            {
                result.Body = new VerifyResponse.Types.Body{ Passed = response.Result.Passed};
            }

            if (response.Error != null)
            {
                result.Error = response.Error.ToApiError();
            }

            return result;
        }

        [AllowAnonymous]
        public override async Task<RegisterResponse> Register(RegisterRequest request, ServerCallContext context)
        {
            var result = new RegisterResponse();

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
                result.Body = _mapper.Map<RegisterResponse.Types.Body>(response.Result);
                string sessionId = await _sessionService.CreateVerifiedSessionAsync(response.Result.Token, request.PublicKey);
                result.Body.SessionId = sessionId;
            }

            if (response.Error != null)
            {
                result.Error = response.Error.ToApiError();
            }

            return result;
        }
    }
}

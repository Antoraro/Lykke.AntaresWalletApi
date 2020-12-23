using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AntaresWalletApi.Common.Domain;
using AntaresWalletApi.Extensions;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Lykke.ApiClients.V1;
using Lykke.ApiClients.V2;
using Microsoft.AspNetCore.StaticFiles;
using Newtonsoft.Json;
using Swisschain.Lykke.AntaresWalletApi.ApiContract;
using AnswersRequest = Swisschain.Lykke.AntaresWalletApi.ApiContract.AnswersRequest;
using QuestionnaireResponse = Swisschain.Lykke.AntaresWalletApi.ApiContract.QuestionnaireResponse;
using Status = Grpc.Core.Status;
using ApiExceptionV1 = Lykke.ApiClients.V1.ApiException;
using ApiExceptionV2 = Lykke.ApiClients.V2.ApiException;

namespace AntaresWalletApi.GrpcServices
{
    public partial class ApiService
    {
        public override async Task<KycDocumentsResponse> GetKycDocuments(Empty request, ServerCallContext context)
        {
            var result = new KycDocumentsResponse();

            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV1Client.GetKycProfilesDocumentsByProfileTypeAsync("LykkeEurope", token);

                if (response.Result != null)
                {
                    foreach (var item in response.Result)
                    {
                        var document = _mapper.Map<KycDocumentsResponse.Types.KycDocument>(item.Value);
                        if (item.Value.Files.Any())
                        {
                            document.Files.AddRange(_mapper.Map<List<KycDocumentsResponse.Types.KycFile>>(item.Value.Files));
                        }

                        result.Result.Add(item.Key, document);
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
                    result = JsonConvert.DeserializeObject<KycDocumentsResponse>(ex.Response);
                    return result;
                }

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        public override async Task<EmptyResponseV2> SetAddress(SetAddressRequest request, ServerCallContext context)
        {
            var result = new EmptyResponseV2();

            try
            {
                var token = context.GetBearerToken();
                await _walletApiV2Client.UpdateAddressAsync(new AddressModel{Address = request.Address}, token);
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

        public override async Task<EmptyResponseV2> SetZip(SetZipRequest request, ServerCallContext context)
        {
            var result = new EmptyResponseV2();

            try
            {
                var token = context.GetBearerToken();
                await _walletApiV2Client.UpdateZipCodeAsync(new ZipCodeModel{Zip = request.Zip}, token);
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

        public override async Task<EmptyResponse> UploadKycFile(KycFileRequest request, ServerCallContext context)
        {
            var result = new EmptyResponse();

            try
            {
                var token = context.GetBearerToken();

                if (request.File.IsEmpty)
                    return result;

                var maxSize = _config.MaxReceiveMessageSizeInMb * 1024 * 1024;

                if (request.File.Length > maxSize)
                {
                    result.Error = new ErrorV1
                    {
                        Code = ErrorModelCode.InvalidInputField.ToString(),
                        Message = ErrorMessages.TooBig(nameof(request.File),
                            request.File.Length.ToString(),
                            maxSize.ToString()),
                        Field = nameof(request.File)
                    };

                    return result;
                }

                var provider = new FileExtensionContentTypeProvider();
                if(!provider.TryGetContentType(request.Filename, out var contentType))
                {
                    contentType = "image/jpeg";
                }

                using (var ms = new MemoryStream(request.File.ToByteArray()))
                {
                    await _walletApiV1Client.KycFilesUploadFileAsync(request.DocumentType, string.Empty,
                        new FileParameter(ms, request.Filename, contentType), token);

                    return result;
                }
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

        public override async Task<QuestionnaireResponse> GetQuestionnaire(Empty request, ServerCallContext context)
        {
            var result = new QuestionnaireResponse();

            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV1Client.TiersGetQuestionnaireAsync(token);

                if (response.Result != null)
                {
                    result.Result = new QuestionnaireResponse.Types.QuestionnairePayload();

                    foreach (var question in response.Result.Questionnaire)
                    {
                        var q = _mapper.Map<QuestionnaireResponse.Types.Question>(question);
                        q.Answers.AddRange(_mapper.Map<List<QuestionnaireResponse.Types.Answer>>(question.Answers));
                        result.Result.Questionnaire.Add(q);
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
                    result = JsonConvert.DeserializeObject<QuestionnaireResponse>(ex.Response);
                    return result;
                }

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        public override async Task<EmptyResponse> SaveQuestionnaire(AnswersRequest request, ServerCallContext context)
        {
            var result = new EmptyResponse();

            try
            {
                var token = context.GetBearerToken();

                var req = new Lykke.ApiClients.V1.AnswersRequest
                {
                    Answers = _mapper.Map<List<ChoiceModel>>(request.Answers.ToList())
                };

                var response = await _walletApiV1Client.TiersSaveQuestionnaireAsync(req, token);

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

        public override async Task<EmptyResponse> SubmitProfile(SubmitProfileRequest request, ServerCallContext context)
        {
            var result = new EmptyResponse();

            try
            {
                var token = context.GetBearerToken();
                Tier? tier = request.OptionalTierCase == SubmitProfileRequest.OptionalTierOneofCase.None
                    ? (Tier?)null
                    : request.Tier == TierUpgrade.Advanced ? Tier.Advanced : Tier.ProIndividual;

                var response = await _walletApiV1Client.KycProfilesSubmitAsync("LykkeEurope", tier, token);

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

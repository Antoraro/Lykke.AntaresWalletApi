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
using Swisschain.Lykke.AntaresWalletApi.ApiContract;
using AnswersRequest = Swisschain.Lykke.AntaresWalletApi.ApiContract.AnswersRequest;
using QuestionnaireResponse = Swisschain.Lykke.AntaresWalletApi.ApiContract.QuestionnaireResponse;

namespace AntaresWalletApi.GrpcServices
{
    public partial class ApiService
    {
        public override async Task<KycDocumentsResponse> GetKycDocuments(Empty request, ServerCallContext context)
        {
            var result = new KycDocumentsResponse();

            var token = context.GetBearerToken();
            var response = await _walletApiV1Client.GetKycProfilesDocumentsByProfileTypeAsync("LykkeEurope", token);

            if (response.Result != null)
            {
                foreach (var item in response.Result)
                {
                    var document = _mapper.Map<KycDocument>(item.Value);
                    if (item.Value.Files.Any())
                    {
                        document.Files.AddRange(_mapper.Map<List<KycFile>>(item.Value.Files));
                    }

                    result.Body = new KycDocumentsResponse.Types.Body();
                    result.Body.Result.Add(item.Key, document);
                }
            }

            if (response.Error != null)
            {
                result.Error = response.Error.ToApiError();
            }

            return result;
        }

        public override async Task<EmptyResponse> SetAddress(SetAddressRequest request, ServerCallContext context)
        {
            var result = new EmptyResponse();

            var token = context.GetBearerToken();
            await _walletApiV2Client.UpdateAddressAsync(new AddressModel{Address = request.Address}, token);
            return result;
        }

        public override async Task<EmptyResponse> SetZip(SetZipRequest request, ServerCallContext context)
        {
            var result = new EmptyResponse();

            var token = context.GetBearerToken();
            await _walletApiV2Client.UpdateZipCodeAsync(new ZipCodeModel{Zip = request.Zip}, token);
            return result;
        }

        public override async Task<EmptyResponse> UploadKycFile(KycFileRequest request, ServerCallContext context)
        {
            var result = new EmptyResponse();

            var token = context.GetBearerToken();

            if (request.File.IsEmpty)
                return result;

            var maxSize = _config.MaxReceiveMessageSizeInMb * 1024 * 1024;

            if (request.File.Length > maxSize)
            {
                result.Error = new ErrorResponseBody
                {
                    Code = ErrorCode.InvalidField,
                    Message = ErrorMessages.TooBig(nameof(request.File),
                        request.File.Length.ToString(),
                        maxSize.ToString()),
                };

                result.Error.Fields.Add(nameof(request.File), result.Error.Message);

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

        public override async Task<QuestionnaireResponse> GetQuestionnaire(Empty request, ServerCallContext context)
        {
            var result = new QuestionnaireResponse();

            var token = context.GetBearerToken();
            var response = await _walletApiV1Client.TiersGetQuestionnaireAsync(token);

            if (response.Result != null)
            {
                result.Body = new QuestionnaireResponse.Types.Body();

                foreach (var question in response.Result.Questionnaire)
                {
                    var q = _mapper.Map<QuestionnaireResponse.Types.Question>(question);
                    q.Answers.AddRange(_mapper.Map<List<QuestionnaireResponse.Types.Answer>>(question.Answers));
                    result.Body.Questionnaire.Add(q);
                }
            }

            if (response.Error != null)
            {
                result.Error = response.Error.ToApiError();
            }

            return result;
        }

        public override async Task<EmptyResponse> SaveQuestionnaire(AnswersRequest request, ServerCallContext context)
        {
            var result = new EmptyResponse();

            var token = context.GetBearerToken();

            var req = new Lykke.ApiClients.V1.AnswersRequest
            {
                Answers = _mapper.Map<List<ChoiceModel>>(request.Answers.ToList())
            };

            var response = await _walletApiV1Client.TiersSaveQuestionnaireAsync(req, token);

            if (response.Error != null)
            {
                result.Error = response.Error.ToApiError();
            }

            return result;
        }

        public override async Task<EmptyResponse> SubmitProfile(SubmitProfileRequest request, ServerCallContext context)
        {
            var result = new EmptyResponse();

            var token = context.GetBearerToken();
            var tier = request.OptionalTierCase == SubmitProfileRequest.OptionalTierOneofCase.None
                ? (Tier?)null
                : request.Tier == TierUpgrade.Advanced ? Tier.Advanced : Tier.ProIndividual;

            var response = await _walletApiV1Client.KycProfilesSubmitAsync("LykkeEurope", tier, token);

            if (response.Error != null)
            {
                result.Error = response.Error.ToApiError();
            }

            return result;
        }
    }
}

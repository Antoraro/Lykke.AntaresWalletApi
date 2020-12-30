using System.Threading.Tasks;
using AntaresWalletApi.Extensions;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Swisschain.Lykke.AntaresWalletApi.ApiContract;

namespace AntaresWalletApi.GrpcServices
{
    public partial class ApiService
    {
        public override async Task<TierInfoResponse> GetTierInfo(Empty request, ServerCallContext context)
        {
            var token = context.GetBearerToken();
            var response = await _walletApiV1Client.TiersGetInfoAsync(token);

            var result = new TierInfoResponse();

            if (response.Result != null)
            {
                result.Body = new TierInfoPayload
                {
                    CurrentTier = _mapper.Map<CurrentTier>(response.Result.CurrentTier),
                    NextTier = _mapper.Map<NextTier>(response.Result.NextTier),
                    UpgradeRequest = _mapper.Map<UpgradeRequest>(response.Result.UpgradeRequest),
                    QuestionnaireAnswered = response.Result.QuestionnaireAnswered
                };

                result.Body.NextTier?.Documents.AddRange(response.Result.NextTier?.Documents);
            }

            if (response.Error != null)
            {
                result.Error = response.Error.ToApiError();
            }

            return result;
        }

        public override async Task<PersonalDataResponse> GetPersonalData(Empty request, ServerCallContext context)
        {
            var result = new PersonalDataResponse();

            var token = context.GetBearerToken();
            var response = await _walletApiV1Client.GetPersonalDataAsync(token);

            if (response.Result != null)
            {
                result.Body = _mapper.Map<PersonalData>(response.Result);
            }

            if (response.Error != null)
            {
                result.Error = response.Error.ToApiError();
            }

            return result;
        }
    }
}

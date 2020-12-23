using System.Threading.Tasks;
using AntaresWalletApi.Extensions;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Newtonsoft.Json;
using Swisschain.Lykke.AntaresWalletApi.ApiContract;
using ApiExceptionV1 = Lykke.ApiClients.V1.ApiException;
using ApiExceptionV2 = Lykke.ApiClients.V2.ApiException;

namespace AntaresWalletApi.GrpcServices
{
    public partial class ApiService
    {
        public override async Task<TierInfoRespone> GetTierInfo(Empty request, ServerCallContext context)
        {
            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV1Client.TiersGetInfoAsync(token);

                var result = new TierInfoRespone();

                if (response.Result != null)
                {
                    result.Result = new TierInfoPayload
                    {
                        CurrentTier = _mapper.Map<CurrentTier>(response.Result.CurrentTier),
                        NextTier = _mapper.Map<NextTier>(response.Result.NextTier),
                        UpgradeRequest = _mapper.Map<UpgradeRequest>(response.Result.UpgradeRequest),
                        QuestionnaireAnswered = response.Result.QuestionnaireAnswered
                    };

                    result.Result.NextTier?.Documents.AddRange(response.Result.NextTier?.Documents);
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

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }

        public override async Task<PersonalDataResponse> GetPersonalData(Empty request, ServerCallContext context)
        {
            var result = new PersonalDataResponse();

            try
            {
                var token = context.GetBearerToken();
                var response = await _walletApiV1Client.GetPersonalDataAsync(token);

                if (response.Result != null)
                {
                    result.Result = _mapper.Map<PersonalData>(response.Result);
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
                    result = JsonConvert.DeserializeObject<PersonalDataResponse>(ex.Response);
                    return result;
                }

                throw new RpcException(new Status(StatusCode.Unknown, ex.Message));
            }
        }
    }
}

using System;
using System.Threading.Tasks;
using AntaresWalletApi.Common.Domain;
using Common;
using Common.Log;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Lykke.Common.Log;
using Newtonsoft.Json;
using Swisschain.Lykke.AntaresWalletApi.ApiContract;
using ApiExceptionV1 = Lykke.ApiClients.V1.ApiException;
using ApiExceptionV2 = Lykke.ApiClients.V2.ApiException;

namespace AntaresWalletApi.Infrastructure
{
    public class ErrorInterceptor : Interceptor
    {
        private readonly ILog _log;

        public ErrorInterceptor(
            ILogFactory logFactory
            )
        {
            _log = logFactory.CreateLog(this);
        }

        public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(TRequest request,
            ServerCallContext context,
            UnaryServerMethod<TRequest, TResponse> continuation)
        {
            try
            {
                return await base.UnaryServerHandler(request, context, continuation);
            }
            catch (ApiExceptionV2 ex)
            {
                string res;

                switch (ex.StatusCode)
                {
                    case 401:
                        res = new { error = new ErrorResponseBody{Code = ErrorCode.Unauthorized, Message = ErrorMessages.InvalidToken}, result = "error"}.ToJson();
                        break;
                    default:
                        res = new { error = new ErrorResponseBody{Code = ErrorCode.Unknown, Message = ErrorMessages.UnknownError}, result = "error"}.ToJson();
                        break;
                }

                return JsonConvert.DeserializeObject<TResponse>(res);
            }
            catch (ApiExceptionV1 ex)
            {
                string res;

                switch (ex.StatusCode)
                {
                    case 401:
                        res = new { error = new ErrorResponseBody{Code = ErrorCode.Unauthorized, Message = ErrorMessages.InvalidToken}, result = "error"}.ToJson();
                        break;
                    default:
                        res = new { error = new ErrorResponseBody{Code = ErrorCode.Unknown, Message = ErrorMessages.UnknownError}, result = "error"}.ToJson();
                        break;
                }

                return JsonConvert.DeserializeObject<TResponse>(res);
            }
            catch (Exception ex)
            {
                _log.Error(ex);
                var res = new { error = new ErrorResponseBody{Code = ErrorCode.Runtime, Message = ErrorMessages.RuntimeError}, result = "error"}.ToJson();
                return JsonConvert.DeserializeObject<TResponse>(res);
            }
        }
    }
}

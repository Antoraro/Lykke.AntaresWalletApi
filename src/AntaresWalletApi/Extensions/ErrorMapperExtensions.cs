using System;
using Lykke.ApiClients.V1;
using Swisschain.Lykke.AntaresWalletApi.ApiContract;

namespace AntaresWalletApi.Extensions
{
    public static class ErrorMapperExtensions
    {
        public static ErrorResponseBody ToApiError(this ErrorModel error)
        {
            var result = new ErrorResponseBody
            {
                Code = error.Code switch
                {
                    ErrorModelCode.InvalidInputField => ErrorCode.InvalidField,
                    ErrorModelCode.InconsistentData => ErrorCode.Unknown,
                    ErrorModelCode.NotAuthenticated => ErrorCode.Unknown,
                    ErrorModelCode.InvalidUsernameOrPassword => ErrorCode.Unknown,
                    ErrorModelCode.AssetNotFound => ErrorCode.Unknown,
                    ErrorModelCode.NotEnoughFunds => ErrorCode.Unknown,
                    ErrorModelCode.VersionNotSupported => ErrorCode.Unknown,
                    ErrorModelCode.RuntimeProblem => ErrorCode.Runtime,
                    ErrorModelCode.WrongConfirmationCode => ErrorCode.Unknown,
                    ErrorModelCode.BackupWarning => ErrorCode.Unknown,
                    ErrorModelCode.BackupRequired => ErrorCode.Unknown,
                    ErrorModelCode.MaintananceMode => ErrorCode.Unknown,
                    ErrorModelCode.NoData => ErrorCode.Unknown,
                    ErrorModelCode.ShouldOpenNewChannel => ErrorCode.Unknown,
                    ErrorModelCode.ShouldProvideNewTempPubKey => ErrorCode.Unknown,
                    ErrorModelCode.ShouldProcesOffchainRequest => ErrorCode.Unknown,
                    ErrorModelCode.NoOffchainLiquidity => ErrorCode.Unknown,
                    ErrorModelCode.GeneralError => ErrorCode.Unknown,
                    ErrorModelCode.AddressShouldBeGenerated => ErrorCode.Unknown,
                    ErrorModelCode.ExpiredAccessToken => ErrorCode.Unknown,
                    ErrorModelCode.BadAccessToken => ErrorCode.Unknown,
                    ErrorModelCode.NoEncodedMainKey => ErrorCode.Unknown,
                    ErrorModelCode.PreviousTransactionsWereNotCompleted => ErrorCode.Unknown,
                    ErrorModelCode.LimitationCheckFailed => ErrorCode.Unknown,
                    ErrorModelCode.TransactionAlreadyExists => ErrorCode.Unknown,
                    ErrorModelCode.UnknownTrustedTransferDirection => ErrorCode.Unknown,
                    ErrorModelCode.InvalidGuidValue => ErrorCode.Unknown,
                    ErrorModelCode.BadTempAccessToken => ErrorCode.Unknown,
                    ErrorModelCode.NotEnoughLiquidity => ErrorCode.Unknown,
                    ErrorModelCode.InvalidCashoutAddress => ErrorCode.Unknown,
                    ErrorModelCode.MinVolumeViolation => ErrorCode.Unknown,
                    ErrorModelCode.PendingDisclaimer => ErrorCode.Unknown,
                    ErrorModelCode.AmountIsLessThanLimit => ErrorCode.Unknown,
                    ErrorModelCode.AmountIsBiggerThanLimit => ErrorCode.Unknown,
                    ErrorModelCode.AmountIsBiggerThanLimitAndUpgrade => ErrorCode.Unknown,
                    ErrorModelCode.DepositWalletDuplication => ErrorCode.Unknown,
                    ErrorModelCode.BadRequest => ErrorCode.Unknown,
                    ErrorModelCode.NotEnoughGas => ErrorCode.Unknown,
                    _ => throw new ArgumentOutOfRangeException()
                },
                Message = error.Message
            };

            result.Fields.Add(error.Field, error.Message);

            return result;
        }
    }
}

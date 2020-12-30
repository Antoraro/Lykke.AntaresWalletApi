namespace AntaresWalletApi.Common.Domain
{
    public static class ErrorMessages
    {
        public const string AssetNotFound = "Asset not found";
        public const string AssetPairNotFound = "Asset pair not found";
        public const string AssetPairDisabled = "Asset pair is disabled";
        public static string LessThanZero(string name) => $"{name} cannot be less than zero";
        public static string MustBeGreaterThan(string name, string minValue) => $"{name} must be greater than {minValue}";
        public static string TooBig(string name, string value, string maxValue) =>
            $"{name} '{value}' is too big, maximum is '{maxValue}'";

        public const string NotEnoughFunds = "Not enough funds";
        public const string MeNotAvailable = "ME not available";
        public static string CantBeEmpty(string name) => $"{name} cannot be empty";
        public static string InvalidFieldValue(string name) => $"{name} value is invalid";
        public static string InvalidToken = "Invalid token";
        public static string RuntimeError = "Runtime error";
        public static string UnknownError = "Unknown error";

    }
}

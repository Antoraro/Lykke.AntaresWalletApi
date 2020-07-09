using System.Globalization;
using System.Threading.Tasks;
using AntaresWalletApi.Common.Domain;
using Lykke.MatchingEngine.Connector.Models.Common;
using Lykke.Service.Assets.Client;
using Lykke.Service.Balances.AutorestClient.Models;
using Lykke.Service.Balances.Client;

namespace AntaresWalletApi.Services
{
    public class ValidationService
    {
        private readonly IAssetsServiceWithCache _assetsService;
        private readonly IBalancesClient _balancesClient;
        private const int MaxPageSize = 500;

        public ValidationService(
            IAssetsServiceWithCache assetsService,
            IBalancesClient balancesClient
            )
        {
            _assetsService = assetsService;
            _balancesClient = balancesClient;
        }

        public async Task<ValidationResult> ValidateLimitOrderAsync(string walletId, string assetPairId, OrderAction side, decimal price, decimal volume)
        {
            if (price <= 0)
            {
                return new ValidationResult
                {
                    Message = ErrorMessages.LessThanZero(nameof(price)),
                    FieldName = nameof(price)
                };
            }

            if (volume <= 0)
            {
                return new ValidationResult
                {
                    Message = ErrorMessages.LessThanZero(nameof(volume)),
                    FieldName = nameof(volume)
                };
            }

            var assetPair = await _assetsService.TryGetAssetPairAsync(assetPairId);

            if (assetPair == null)
            {
                return new ValidationResult
                {
                    Message = ErrorMessages.AssetPairNotFound,
                    FieldName = nameof(assetPairId)
                };
            }

            if (volume < (decimal)assetPair.MinVolume)
            {
                return new ValidationResult
                {
                    Message = ErrorMessages.MustBeGreaterThan(nameof(volume), assetPair.MinVolume.ToString(CultureInfo.InvariantCulture)),
                    FieldName = nameof(volume)
                };
            }

            decimal totalVolume;
            string asset;

            if (side == OrderAction.Buy)
            {
                asset = assetPair.QuotingAssetId;
                totalVolume = price * volume;
            }
            else
            {
                asset = assetPair.BaseAssetId;
                totalVolume = volume;
            }

            var assetBalance = await _balancesClient.GetClientBalanceByAssetId(
                new ClientBalanceByAssetIdModel
                {
                    ClientId = walletId,
                    AssetId = asset
                });

            if (assetBalance == null || assetBalance.Balance - assetBalance.Reserved < totalVolume)
            {
                return new ValidationResult
                {
                    Message = ErrorMessages.NotEnoughFunds,
                    FieldName = nameof(volume)
                };
            }

            return null;
        }

        public async Task<ValidationResult> ValidateMarketOrderAsync(string assetPairId, decimal volume)
        {
            if (volume <= 0)
            {
                return new ValidationResult
                {
                    Message = ErrorMessages.LessThanZero(nameof(volume)),
                    FieldName = nameof(volume)
                };
            }

            var assetPair = await _assetsService.TryGetAssetPairAsync(assetPairId);

            if (assetPair == null)
            {
                return new ValidationResult
                {
                    Message = ErrorMessages.AssetPairNotFound,
                    FieldName = nameof(assetPairId)
                };
            }

            if (volume < (decimal)assetPair.MinVolume)
            {
                return new ValidationResult
                {
                    Message = ErrorMessages.MustBeGreaterThan(nameof(volume), assetPair.MinVolume.ToString(CultureInfo.InvariantCulture)),
                    FieldName = nameof(volume)
                };
            }

            return null;
        }

        public async Task<ValidationResult> ValidateOrdersRequestAsync(string assetPairId, int? offset, int? take)
        {
            var assetPairResult = await ValidateAssetPairAsync(assetPairId);

            if (assetPairResult != null)
                return assetPairResult;

            if (offset.HasValue && offset < 0)
            {
                return new ValidationResult
                {
                    Message = ErrorMessages.LessThanZero(nameof(offset)),
                    FieldName = nameof(offset)
                };
            }

            if (take.HasValue && take < 0)
            {
                return new ValidationResult
                {
                    Message = ErrorMessages.LessThanZero(nameof(take)),
                    FieldName = nameof(take)
                };
            }

            if (take.HasValue && take > MaxPageSize)
            {
                return new ValidationResult
                {
                    Message = ErrorMessages.TooBig(nameof(take), take.Value.ToString(), MaxPageSize.ToString()),
                    FieldName = nameof(take)
                };
            }

            return null;
        }

        public async Task<ValidationResult> ValidateTradesRequestAsync(string assetPairId, int? offset, int? take)
        {
            var assetPairResult = await ValidateAssetPairAsync(assetPairId);

            if (assetPairResult != null)
                return assetPairResult;

            if (offset.HasValue && offset < 0)
            {
                return new ValidationResult
                {
                    Message = ErrorMessages.LessThanZero(nameof(offset)),
                    FieldName = nameof(offset)
                };
            }

            if (take.HasValue && take < 0)
            {
                return new ValidationResult
                {
                    Message = ErrorMessages.LessThanZero(nameof(take)),
                    FieldName = nameof(take)
                };
            }

            if (take.HasValue && take > MaxPageSize)
            {
                return new ValidationResult
                {
                    Message = ErrorMessages.TooBig(nameof(take), take.Value.ToString(), MaxPageSize.ToString()),
                    FieldName = nameof(take)
                };
            }

            return null;
        }

        public async Task<ValidationResult> ValidateAssetPairAsync(string assetPairId)
        {
            if (string.IsNullOrEmpty(assetPairId))
                return null;

            var assetPair = await _assetsService.TryGetAssetPairAsync(assetPairId);

            if (assetPair == null)
            {
                return new ValidationResult
                {
                    Message = ErrorMessages.AssetPairNotFound,
                    FieldName = nameof(assetPairId)
                };
            }

            if (assetPair.IsDisabled)
            {
                return new ValidationResult
                {
                    Message = ErrorMessages.AssetPairDisabled,
                    FieldName = nameof(assetPairId)
                };
            }

            return null;
        }

        public async Task<ValidationResult> ValidateAssetAsync(string assetId)
        {
            if (string.IsNullOrEmpty(assetId))
                return null;

            var asset = await _assetsService.TryGetAssetAsync(assetId);

            if (asset == null)
            {
                return new ValidationResult
                {
                    Message = ErrorMessages.AssetNotFound,
                    FieldName = nameof(assetId)
                };
            }

            return null;
        }
    }

    public class ValidationResult
    {
        public string Message { get; set; }
        public string FieldName { get; set; }
    }
}

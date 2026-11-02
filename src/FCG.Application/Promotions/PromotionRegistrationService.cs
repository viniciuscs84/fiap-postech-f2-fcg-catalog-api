using FCG.Domain.Promotions;

namespace FCG.Application.Promotions;

/// <summary>Implements the promotion registration use case.</summary>
public sealed class PromotionRegistrationService(IPromotionRepository repository) : IPromotionRegistrationService
{
    /// <inheritdoc />
    public async Task<PromotionRegistrationOutcome> RegisterAsync(CreatePromotionCommand command, Guid createdByUserId, CancellationToken cancellationToken)
    {
        var validationErrors = PromotionRegistrationRules.Validate(command);
        if (validationErrors.Count > 0)
        {
            return new PromotionRegistrationOutcome.ValidationFailure(validationErrors);
        }

        var normalizedCode = PromotionRegistrationRules.NormalizeCode(command.Code);
        var existingPromotion = await repository.FindByNormalizedCodeAsync(normalizedCode, cancellationToken);
        if (existingPromotion is not null)
        {
            return new PromotionRegistrationOutcome.Conflict();
        }

        var promotion = Promotion.Create(
            PromotionRegistrationRules.Normalize(command.Name),
            PromotionRegistrationRules.Normalize(command.Code),
            command.DiscountPercentage,
            command.StartsAtUtc,
            command.EndsAtUtc,
            createdByUserId,
            DateTime.UtcNow);

        await repository.AddAsync(promotion, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return new PromotionRegistrationOutcome.Success(new RegisteredPromotionResponse(
            promotion.Id,
            promotion.Name,
            promotion.Code,
            promotion.DiscountPercentage,
            promotion.StartsAtUtc,
            promotion.EndsAtUtc,
            promotion.CreatedByUserId,
            promotion.CreatedAtUtc));
    }
}

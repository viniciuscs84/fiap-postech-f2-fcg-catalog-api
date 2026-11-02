namespace FCG.Application.Promotions;

/// <summary>Represents the result of promotion registration.</summary>
public abstract record PromotionRegistrationOutcome
{
    private PromotionRegistrationOutcome()
    {
    }

    /// <summary>Indicates successful registration.</summary>
    public sealed record Success(RegisteredPromotionResponse Promotion) : PromotionRegistrationOutcome;

    /// <summary>Indicates validation failure.</summary>
    public sealed record ValidationFailure(IReadOnlyDictionary<string, string[]> Errors) : PromotionRegistrationOutcome;

    /// <summary>Indicates that the promotion code already exists.</summary>
    public sealed record Conflict : PromotionRegistrationOutcome;
}

/// <summary>Represents a promotion returned after registration.</summary>
public sealed record RegisteredPromotionResponse(
    Guid Id,
    string Name,
    string Code,
    decimal DiscountPercentage,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    Guid CreatedByUserId,
    DateTime CreatedAtUtc);

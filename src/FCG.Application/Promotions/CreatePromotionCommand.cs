namespace FCG.Application.Promotions;

/// <summary>Contains the data required to create a promotion.</summary>
public sealed record CreatePromotionCommand(
    string Name,
    string Code,
    decimal DiscountPercentage,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc);

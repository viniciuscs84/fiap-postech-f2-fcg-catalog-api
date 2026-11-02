namespace FCG.Application.Promotions;

/// <summary>Coordinates promotion registration.</summary>
public interface IPromotionRegistrationService
{
    /// <summary>Validates and registers a promotion.</summary>
    Task<PromotionRegistrationOutcome> RegisterAsync(CreatePromotionCommand command, Guid createdByUserId, CancellationToken cancellationToken);
}

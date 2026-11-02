namespace FCG.Application.Promotions;

/// <summary>Applies validation and normalization rules to promotions.</summary>
public static class PromotionRegistrationRules
{
    /// <summary>Validates the promotion creation command.</summary>
    public static IReadOnlyDictionary<string, string[]> Validate(CreatePromotionCommand command)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            AddError(errors, nameof(command.Name), "O nome da promoção é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(command.Code))
        {
            AddError(errors, nameof(command.Code), "O código da promoção é obrigatório.");
        }

        if (command.DiscountPercentage <= 0 || command.DiscountPercentage > 100)
        {
            AddError(errors, nameof(command.DiscountPercentage), "O percentual de desconto deve estar entre 0 e 100.");
        }

        var startsAtUtc = DateTime.SpecifyKind(command.StartsAtUtc, DateTimeKind.Utc);
        var endsAtUtc = DateTime.SpecifyKind(command.EndsAtUtc, DateTimeKind.Utc);
        if (endsAtUtc <= startsAtUtc)
        {
            AddError(errors, nameof(command.EndsAtUtc), "A data final da promoção deve ser posterior à data inicial.");
        }

        return errors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Removes surrounding whitespace from a value.</summary>
    public static string Normalize(string value) => value.Trim();

    /// <summary>Normalizes a promotion code for case-insensitive comparisons.</summary>
    public static string NormalizeCode(string value) => value.Trim().ToUpperInvariant();

    private static void AddError(IDictionary<string, List<string>> errors, string field, string message)
    {
        if (!errors.TryGetValue(field, out var fieldErrors))
        {
            fieldErrors = [];
            errors[field] = fieldErrors;
        }

        fieldErrors.Add(message);
    }
}

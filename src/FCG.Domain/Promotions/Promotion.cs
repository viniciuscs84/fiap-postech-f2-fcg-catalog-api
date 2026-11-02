namespace FCG.Domain.Promotions;

/// <summary>Represents a time-bounded catalog promotion.</summary>
public sealed class Promotion
{
    private Promotion()
    {
    }

    /// <summary>Gets the promotion identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the promotion name.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Gets the promotion code as entered by the administrator.</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>Gets the canonical code used for uniqueness checks.</summary>
    public string NormalizedCode { get; private set; } = string.Empty;

    /// <summary>Gets the discount percentage.</summary>
    public decimal DiscountPercentage { get; private set; }

    /// <summary>Gets the promotion start timestamp in UTC.</summary>
    public DateTime StartsAtUtc { get; private set; }

    /// <summary>Gets the promotion end timestamp in UTC.</summary>
    public DateTime EndsAtUtc { get; private set; }

    /// <summary>Gets the creating administrator identifier.</summary>
    public Guid CreatedByUserId { get; private set; }

    /// <summary>Gets the creation timestamp in UTC.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Creates a valid promotion.</summary>
    /// <param name="name">Promotion name.</param>
    /// <param name="code">Promotion code.</param>
    /// <param name="discountPercentage">Discount between 0 and 100.</param>
    /// <param name="startsAtUtc">Start timestamp.</param>
    /// <param name="endsAtUtc">End timestamp.</param>
    /// <param name="createdByUserId">Creating administrator identifier.</param>
    /// <param name="createdAtUtc">Creation timestamp.</param>
    /// <returns>A new promotion.</returns>
    public static Promotion Create(
        string name,
        string code,
        decimal discountPercentage,
        DateTime startsAtUtc,
        DateTime endsAtUtc,
        Guid createdByUserId,
        DateTime createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        if (discountPercentage <= 0 || discountPercentage > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(discountPercentage), "The discount percentage must be between 0 and 100.");
        }

        if (createdByUserId == Guid.Empty)
        {
            throw new ArgumentException("A creator user id is required.", nameof(createdByUserId));
        }

        var normalizedStartsAtUtc = DateTime.SpecifyKind(startsAtUtc, DateTimeKind.Utc);
        var normalizedEndsAtUtc = DateTime.SpecifyKind(endsAtUtc, DateTimeKind.Utc);
        if (normalizedEndsAtUtc <= normalizedStartsAtUtc)
        {
            throw new ArgumentException("The promotion end date must be after the start date.");
        }

        var trimmedName = name.Trim();
        var trimmedCode = code.Trim();

        return new Promotion
        {
            Id = Guid.NewGuid(),
            Name = trimmedName,
            Code = trimmedCode,
            NormalizedCode = trimmedCode.ToUpperInvariant(),
            DiscountPercentage = discountPercentage,
            StartsAtUtc = normalizedStartsAtUtc,
            EndsAtUtc = normalizedEndsAtUtc,
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc)
        };
    }
}

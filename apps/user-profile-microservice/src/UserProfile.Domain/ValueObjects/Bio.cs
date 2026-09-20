namespace UserProfile.Domain.ValueObjects;

public sealed record Bio
{
    public static Bio Empty { get; } = new(string.Empty);

    public string Value { get; }

    public Bio(string? value)
    {
        Value = value?.Trim() ?? string.Empty;
    }

    public override string ToString() => Value;
}

namespace UserProfile.Domain.ValueObjects;

public sealed record DisplayName
{
    public string Value { get; }

    public DisplayName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Display name cannot be empty.", nameof(value));
        }

        Value = value.Trim();
    }

    public override string ToString() => Value;
}

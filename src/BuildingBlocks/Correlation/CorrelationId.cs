namespace IUMP.BuildingBlocks.Correlation;

public readonly record struct CorrelationId(string Value)
{
    public const string HeaderName = "X-Correlation-ID";

    public static CorrelationId Create(string? candidate = null)
    {
        var normalized = candidate?.Trim();
        var value = IsSafe(normalized) ? normalized! : Guid.NewGuid().ToString("D");

        return new CorrelationId(value);
    }

    private static bool IsSafe(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128)
        {
            return false;
        }

        return value.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '-' or '.' or '_' or ':');
    }

    public override string ToString() => Value;
}

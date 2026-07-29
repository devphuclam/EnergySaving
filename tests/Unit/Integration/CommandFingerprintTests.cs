using IUMP.Modules.Integration.Contracts;

namespace IUMP.Tests.Unit.Integration;

public static class CommandFingerprintTests
{
    // T171 request/response canonical contract: UUID, integer, decimal and timestamp fields
    // are normalized in the request, while response/replay metadata never enters the digest.
    public static int TestCount { get; private set; }
    public static int AssertionCount { get; private set; }
    public static int FailureCount { get; private set; }

    public static List<string> Run()
    {
        var failures = new List<string>();
        var assertions = 0;
        var caller = Guid.Parse("11111111-2222-4333-8444-555555555555");
        var input = new CommandFingerprintInput(
            "Organization.CreateSite.v1", caller, null, null, null, null, null,
            new[] { CommandFingerprintField.String("name", "Cafe\u0301") });
        var first = CommandFingerprintV1.Compute(input);
        var normalized = CommandFingerprintV1.Compute(input with
        { Fields = new[] { CommandFingerprintField.String("name", "Café") } });
        assertions++; if (!first.SequenceEqual(normalized)) failures.Add("NFC normalization must be stable.");
        var changed = CommandFingerprintV1.Compute(input with
        { Fields = new[] { CommandFingerprintField.String("name", "Cafe") } });
        assertions++; if (first.SequenceEqual(changed)) failures.Add("Field changes must change the fingerprint.");
        var withIfMatch = CommandFingerprintV1.Compute(input with { ExpectedVersion = 2 });
        assertions++; if (first.SequenceEqual(withIfMatch)) failures.Add("If-Match must be included.");
        assertions++; if (input.Fields.Any(f => f.Name.Contains("password", StringComparison.OrdinalIgnoreCase)))
            failures.Add("Secrets may not be fingerprint fields.");
        var typed = input with
        {
            Fields = new[]
            {
                CommandFingerprintField.Uuid("resourceId", caller),
                CommandFingerprintField.Int64("sequence", 7),
                CommandFingerprintField.Decimal("limit", 1.25m),
                CommandFingerprintField.Timestamp("at", DateTime.SpecifyKind(new DateTime(2026, 1, 2, 3, 4, 5), DateTimeKind.Utc)),
                CommandFingerprintField.String("Idempotency-Key", "transport-only"),
                CommandFingerprintField.String("Authorization", "transport-only")
            }
        };
        var reordered = typed with { Fields = typed.Fields.Reverse().ToArray() };
        assertions++; if (!CommandFingerprintV1.Compute(typed).SequenceEqual(CommandFingerprintV1.Compute(reordered)))
            failures.Add("field order must not change the canonical digest");
        var excludedOnly = typed with { Fields = typed.Fields.Where(field => field.Name is not "Idempotency-Key" and not "Authorization").ToArray() };
        assertions++; if (!CommandFingerprintV1.Compute(typed).SequenceEqual(CommandFingerprintV1.Compute(excludedOnly)))
            failures.Add("Idempotency-Key and auth headers must be excluded from the digest");
        TestCount = 6; AssertionCount = assertions; FailureCount = failures.Count;
        return failures;
    }
}

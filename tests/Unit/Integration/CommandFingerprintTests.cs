using IUMP.Modules.Integration.Application;
using IUMP.Modules.Integration.Contracts;

namespace IUMP.Tests.Unit.Integration;

public static class CommandFingerprintTests
{
    public static List<string> Run()
    {
        var failures = new List<string>();
        var caller = Guid.Parse("11111111-2222-4333-8444-555555555555");
        var input = new CommandFingerprintInput(
            "Organization.CreateSite.v1", caller, null, null, null, null, null,
            new[] { CommandFingerprintField.String("name", "Cafe\u0301") });
        var first = CommandFingerprintV1.Compute(input);
        var normalized = CommandFingerprintV1.Compute(input with
        { Fields = new[] { CommandFingerprintField.String("name", "Café") } });
        if (!first.SequenceEqual(normalized)) failures.Add("NFC normalization must be stable.");
        var changed = CommandFingerprintV1.Compute(input with
        { Fields = new[] { CommandFingerprintField.String("name", "Cafe") } });
        if (first.SequenceEqual(changed)) failures.Add("Field changes must change the fingerprint.");
        var withIfMatch = CommandFingerprintV1.Compute(input with { ExpectedVersion = 2 });
        if (first.SequenceEqual(withIfMatch)) failures.Add("If-Match must be included.");
        if (input.Fields.Any(f => f.Name.Contains("password", StringComparison.OrdinalIgnoreCase)))
            failures.Add("Secrets may not be fingerprint fields.");
        return failures;
    }
}

using System.Security;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Runtime.Versioning;

internal static class Program
{
    private static readonly string[] RequiredFields =
    [
        "deploymentModel", "webHosting", "apiHosting", "workerServiceManager",
        "databaseHosting", "lifecycleRunbookReference", "rollbackRunbookReference",
        "approvalReference", "approvedBy", "approvedAtUtc"
    ];

    private static readonly string CommonDataRoot =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "IUMP");

    private static readonly string ProductionPolicyPath =
        Path.Combine(CommonDataRoot, "DeploymentTrustPolicy.json");

    private static readonly string SecretKeyPattern =
        "password|secret|token|credential|connectionstring|privatekey|apikey|accesskey";

    public static int Main(string[] args)
    {
        try
        {
            var options = ParseArguments(args);
            var synthetic = string.Equals(options.GetValueOrDefault("mode"), "synthetic", StringComparison.Ordinal);
            var result = Verify(options, synthetic);
            Console.WriteLine(JsonSerializer.Serialize(result));
            return result.ExitCode;
        }
        catch (CapabilityUnavailableException ex)
        {
            var result = new VerificationResult(
                "BLOCKED", "BLOCKED_BY_MISSING_TOOL", 20, "BLK-ENV-001", ex.Message, false, 0);
            Console.WriteLine(JsonSerializer.Serialize(result));
            return result.ExitCode;
        }
        catch (Exception)
        {
            var result = new VerificationResult(
                "FAIL", "RUNNABLE_NOW", 1, null, "deployment approval evidence could not be verified", false, 0);
            Console.WriteLine(JsonSerializer.Serialize(result));
            return result.ExitCode;
        }
    }

    private static VerificationResult Verify(IReadOnlyDictionary<string, string> options, bool synthetic)
    {
        var manifestPath = GetRequired(options, "manifest");
        var signaturePath = GetRequired(options, "signature");
        var expectedSha = options.GetValueOrDefault("expected-sha256");
        var policyPath = synthetic ? GetRequired(options, "policy") : ProductionPolicyPath;

        if (!File.Exists(manifestPath) || !File.Exists(signaturePath))
        {
            return Fail("approved manifest or detached signature is unavailable");
        }

        if (!synthetic && !IsCompanyManagedPolicyPath(policyPath))
        {
            return Blocked("company-managed deployment trust policy is unavailable");
        }

        if (!File.Exists(policyPath))
        {
            return synthetic
                ? Blocked("synthetic trust policy is unavailable")
                : Blocked("company-managed deployment trust policy is unavailable");
        }

        var manifestBytes = ReadBytesOnce(manifestPath, out var manifestReadCount);
        var signatureBytes = ReadBytesOnce(signaturePath, out _);
        var actualSha = Convert.ToHexString(SHA256.HashData(manifestBytes));
        if (!string.IsNullOrWhiteSpace(expectedSha) &&
            !string.Equals(actualSha, expectedSha, StringComparison.OrdinalIgnoreCase))
        {
            return Fail("approved manifest attestation does not match", manifestReadCount);
        }

        var policy = ReadPolicy(policyPath);
        var manifestFailure = ValidateManifest(manifestBytes);
        if (manifestFailure is not null)
        {
            return Fail(manifestFailure, manifestReadCount);
        }

        SignedCms cms;
        try
        {
            cms = new SignedCms(new ContentInfo(manifestBytes), detached: true);
            cms.Decode(signatureBytes);
            cms.CheckSignature(verifySignatureOnly: true);
        }
        catch (CryptographicException)
        {
            return Fail("detached CMS signature is malformed or does not match the manifest", manifestReadCount);
        }

        var signerCertificate = cms.SignerInfos.Count == 1 ? cms.SignerInfos[0].Certificate : null;
        if (signerCertificate is null)
        {
            return Fail("detached CMS signature must contain exactly one signer certificate", manifestReadCount);
        }

        using var signer = new X509Certificate2(signerCertificate);
        if (signer.NotBefore.ToUniversalTime() > DateTime.UtcNow || signer.NotAfter.ToUniversalTime() < DateTime.UtcNow)
        {
            return Fail("signer certificate is expired or not yet valid", manifestReadCount);
        }

        if (!policy.AllowedSignerThumbprints.Contains(NormalizeThumbprint(signer.Thumbprint)))
        {
            return Fail("signer certificate is not allowed by the deployment trust policy", manifestReadCount);
        }

        if (!HasRequiredEkus(signer, policy.RequiredEkuOids))
        {
            return Fail("signer certificate does not satisfy the deployment trust policy EKU/OID requirement", manifestReadCount);
        }

        if (!synthetic && !BuildCompanyChain(signer))
        {
            return Fail("signer certificate chain is not trusted by the LocalMachine certificate policy", manifestReadCount);
        }

        var evidence = synthetic
            ? "synthetic cryptographic contract evidence only; signer policy matched; manifest bytes verified once"
            : "company-managed trust policy verified; signer chain verified; manifest bytes verified once";
        return new VerificationResult("PASS", "RUNNABLE_NOW", 0, null, evidence, synthetic, manifestReadCount);
    }

    private static Dictionary<string, string> ParseArguments(IReadOnlyList<string> args)
    {
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < args.Count; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal) || i + 1 >= args.Count)
            {
                throw new ArgumentException("invalid verifier arguments");
            }

            options[args[i][2..]] = args[++i];
        }

        return options;
    }

    private static string GetRequired(IReadOnlyDictionary<string, string> options, string name) =>
        options.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ArgumentException("missing required verifier argument");

    private static byte[] ReadBytesOnce(string path, out int readCount)
    {
        readCount = 1;
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static TrustPolicy ReadPolicy(string path)
    {
        try
        {
            var bytes = ReadBytesOnce(path, out _);
            using var document = JsonDocument.Parse(bytes);
            var root = document.RootElement;
            var thumbprints = ReadStringArray(root, "allowedSignerThumbprints")
                .Select(NormalizeThumbprint)
                .Where(value => value.Length == 40)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var ekus = ReadStringArray(root, "requiredEkuOids").ToHashSet(StringComparer.Ordinal);
            if (thumbprints.Count == 0)
            {
                throw new InvalidDataException("deployment trust policy has no allowed signer");
            }

            return new TrustPolicy(thumbprints, ekus);
        }
        catch (JsonException)
        {
            throw new InvalidDataException("deployment trust policy is malformed");
        }
    }

    private static IEnumerable<string> ReadStringArray(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString() ?? string.Empty)
            .Where(item => !string.IsNullOrWhiteSpace(item));
    }

    private static string? ValidateManifest(byte[] manifestBytes)
    {
        try
        {
            var text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                .GetString(manifestBytes);
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return "manifest schema is invalid";
            }

            foreach (var field in RequiredFields)
            {
                if (!root.TryGetProperty(field, out var value) ||
                    value.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(value.GetString()))
                {
                    return "manifest schema requires non-empty scalar string fields";
                }
            }

            if (!string.Equals(root.GetProperty("deploymentModel").GetString(),
                "restricted-non-containerized", StringComparison.Ordinal))
            {
                return "deployment model is not the canonical restricted non-containerized value";
            }

            var approvedAt = root.GetProperty("approvedAtUtc").GetString() ?? string.Empty;
            if (!DateTimeOffset.TryParseExact(
                    approvedAt,
                    ["yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'",
                     "yyyy-MM-dd'T'HH:mm:sszzz", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz"],
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                    out var approvedAtUtc) ||
                (!approvedAt.EndsWith("Z", StringComparison.Ordinal) && !approvedAt.EndsWith("+00:00", StringComparison.Ordinal)) ||
                approvedAtUtc > DateTimeOffset.UtcNow.AddMinutes(5))
            {
                return "approvedAtUtc must be ISO-8601 UTC and not unreasonably in the future";
            }

            return ContainsSecretKey(root) ? "manifest contains a prohibited secret-like field name" : null;
        }
        catch (JsonException)
        {
            return "manifest JSON is malformed or unreadable";
        }
        catch (DecoderFallbackException)
        {
            return "manifest is not strict UTF-8";
        }
    }

    private static bool ContainsSecretKey(JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in value.EnumerateObject())
                {
                    if (System.Text.RegularExpressions.Regex.IsMatch(property.Name, SecretKeyPattern,
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant) ||
                        ContainsSecretKey(property.Value))
                    {
                        return true;
                    }
                }
                break;
            case JsonValueKind.Array:
                return value.EnumerateArray().Any(ContainsSecretKey);
        }

        return false;
    }

    private static bool HasRequiredEkus(X509Certificate2 certificate, IReadOnlySet<string> requiredEkus)
    {
        if (requiredEkus.Count == 0)
        {
            return true;
        }

        var extension = certificate.Extensions.OfType<X509EnhancedKeyUsageExtension>().FirstOrDefault();
        var actual = extension?.EnhancedKeyUsages.Cast<Oid>()
                .Where(oid => oid.Value is not null)
                .Select(oid => oid.Value!)
                .ToHashSet(StringComparer.Ordinal)
            ?? new HashSet<string>(StringComparer.Ordinal);
        return requiredEkus.All(actual.Contains);
    }

    private static bool BuildCompanyChain(X509Certificate2 signer)
    {
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.System;
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
        return chain.Build(signer);
    }

    [SupportedOSPlatform("windows")]
    private static bool IsCompanyManagedPolicyPath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var expectedPath = Path.GetFullPath(ProductionPolicyPath);
        if (!string.Equals(fullPath, expectedPath, StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(fullPath) ||
            (File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0)
        {
            return false;
        }

        try
        {
            var security = new FileInfo(fullPath).GetAccessControl();
            var current = WindowsIdentity.GetCurrent();
            var currentSid = current.User;
            var owner = security.GetOwner(typeof(SecurityIdentifier)) as SecurityIdentifier;
            if (currentSid is null || owner is null || owner.Value == currentSid.Value)
            {
                return false;
            }

            var principal = new WindowsPrincipal(current);
            foreach (FileSystemAccessRule rule in security.GetAccessRules(true, true, typeof(SecurityIdentifier)))
            {
                if (rule.AccessControlType != AccessControlType.Allow || !HasWritePermission(rule.FileSystemRights))
                {
                    continue;
                }

                var sid = (SecurityIdentifier)rule.IdentityReference;
                if (sid.Value == currentSid.Value ||
                    (principal.IsInRole(sid) && !IsSystemIdentity(sid)))
                {
                    return false;
                }
            }

            return true;
        }
        catch (SecurityException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    [SupportedOSPlatform("windows")]
    private static bool HasWritePermission(FileSystemRights rights) =>
        (rights & (FileSystemRights.WriteData | FileSystemRights.AppendData | FileSystemRights.WriteAttributes |
                   FileSystemRights.WriteExtendedAttributes | FileSystemRights.Delete |
                   FileSystemRights.DeleteSubdirectoriesAndFiles | FileSystemRights.ChangePermissions |
                   FileSystemRights.TakeOwnership | FileSystemRights.FullControl)) != 0;

    [SupportedOSPlatform("windows")]
    private static bool IsSystemIdentity(SecurityIdentifier sid) =>
        sid.IsWellKnown(WellKnownSidType.LocalSystemSid) ||
        sid.Value.Equals("S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeThumbprint(string? value) =>
        (value ?? string.Empty).Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();

    private static VerificationResult Blocked(string evidence) =>
        new("BLOCKED", "BLOCKED_BY_COMPANY_APPROVAL", 20, "BLK-ENV-005", evidence, false, 0);

    private static VerificationResult Fail(string evidence, int readCount = 0) =>
        new("FAIL", "RUNNABLE_NOW", 1, null, evidence, false, readCount);

    private sealed record TrustPolicy(IReadOnlySet<string> AllowedSignerThumbprints, IReadOnlySet<string> RequiredEkuOids);

    private sealed record VerificationResult(
        string Status,
        string Classification,
        int ExitCode,
        string? BlockerId,
        string Evidence,
        bool Synthetic,
        int ManifestReadCount);

    private sealed class CapabilityUnavailableException(string message) : Exception(message);
}

using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;

internal static class Program
{
    private static readonly string[] RequiredManifestFields =
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

    private static readonly HashSet<string> AcceptedDigestOids =
    [
        "2.16.840.1.101.3.4.2.1", // SHA-256
        "2.16.840.1.101.3.4.2.2", // SHA-384
        "2.16.840.1.101.3.4.2.3"  // SHA-512
    ];

    public static int Main(string[] args)
    {
        try
        {
            var options = ParseArguments(args);
            var synthetic = string.Equals(options.GetValueOrDefault("mode"), "synthetic", StringComparison.Ordinal);
            var result = Verify(options, synthetic);
            WriteResult(result);
            return result.ExitCode;
        }
        catch (CapabilityUnavailableException ex)
        {
            var result = new VerificationResult(
                "BLOCKED", "BLOCKED_BY_MISSING_TOOL", 20, "BLK-ENV-001", ex.Message, false, 0, 0);
            WriteResult(result);
            return result.ExitCode;
        }
        catch (Exception)
        {
            var result = new VerificationResult(
                "FAIL", "RUNNABLE_NOW", 1, null,
                "deployment approval evidence could not be verified", false, 0, 0);
            WriteResult(result);
            return result.ExitCode;
        }
    }

    private static void WriteResult(VerificationResult result) =>
        Console.WriteLine($"IUMP_VERIFICATION_RESULT={JsonSerializer.Serialize(result)}");

    private static VerificationResult Verify(IReadOnlyDictionary<string, string> options, bool synthetic)
    {
        var manifestPath = GetRequired(options, "manifest");
        var signaturePath = GetRequired(options, "signature");
        var trustedRoot = GetRequired(options, "trusted-root");
        var expectedSha = options.GetValueOrDefault("expected-sha256");
        var repositoryRoot = options.GetValueOrDefault("repository-root");
        var policyPath = synthetic ? GetRequired(options, "policy") : ProductionPolicyPath;

        var pathResult = ValidateEvidencePair(manifestPath, signaturePath, trustedRoot, repositoryRoot, synthetic);
        if (pathResult is not null)
        {
            return pathResult;
        }

        if (string.IsNullOrWhiteSpace(expectedSha))
        {
            return Blocked("approved manifest attestation is unavailable", synthetic);
        }

        if (!IsSha256(expectedSha))
        {
            return Fail("approved manifest attestation is malformed", synthetic);
        }

        byte[] manifestBytes;
        byte[] signatureBytes;
        try
        {
            manifestBytes = ReadBytesOnce(manifestPath, out var manifestReadCount);
            signatureBytes = ReadBytesOnce(signaturePath, out _);

            var actualSha = Convert.ToHexString(SHA256.HashData(manifestBytes));
            if (!string.Equals(actualSha, expectedSha, StringComparison.OrdinalIgnoreCase))
            {
                return Fail("approved manifest attestation does not match", synthetic, manifestReadCount, 0);
            }

            var manifestFailure = ValidateManifest(manifestBytes);
            if (manifestFailure is not null)
            {
                return Fail(manifestFailure, synthetic, manifestReadCount, 0);
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
                return Fail("detached CMS signature is malformed or does not match the manifest", synthetic, manifestReadCount, 0);
            }

            if (cms.SignerInfos.Count != 1 || cms.SignerInfos[0].Certificate is null)
            {
                return Fail("detached CMS signature must contain exactly one signer certificate", synthetic, manifestReadCount, 0);
            }

            var signerInfo = cms.SignerInfos[0];
            var signerCertificate = signerInfo.Certificate!;
            using var signer = new X509Certificate2(signerCertificate);
            if (!AcceptedDigestOids.Contains(signerInfo.DigestAlgorithm.Value ?? string.Empty))
            {
                return Fail("detached CMS signature uses a disallowed digest algorithm", synthetic, manifestReadCount, 0);
            }

            if (!HasStrongPublicKey(signer))
            {
                return Fail("signer certificate public key is below the deployment policy strength", synthetic, manifestReadCount, 0);
            }

            if (signer.NotBefore.ToUniversalTime() > DateTime.UtcNow ||
                signer.NotAfter.ToUniversalTime() < DateTime.UtcNow)
            {
                return Fail("signer certificate is expired or not yet valid", synthetic, manifestReadCount, 0);
            }

            TrustPolicy policy;
            int policyReadCount;
            try
            {
                policy = ReadPolicySnapshot(policyPath, enforceCompanySecurity: !synthetic, out policyReadCount);
            }
            catch (CompanyPolicyUnavailableException)
            {
                return Blocked("company-managed deployment trust policy is unavailable", synthetic, manifestReadCount, 0);
            }
            catch (CapabilityUnavailableException)
            {
                return MissingTool("deployment trust-policy security capability is unavailable", synthetic, manifestReadCount, 0);
            }
            catch (HandleSecurityCapabilityUnavailableException)
            {
                return MissingTool("deployment trust-policy handle-security capability is unavailable", synthetic, manifestReadCount, 0);
            }
            catch (FileNotFoundException)
            {
                return Blocked("deployment trust policy is unavailable", synthetic, manifestReadCount, 0);
            }
            catch (DirectoryNotFoundException)
            {
                return Blocked("deployment trust policy is unavailable", synthetic, manifestReadCount, 0);
            }
            catch (UnauthorizedAccessException)
            {
                return Blocked("deployment trust policy security could not be verified", synthetic, manifestReadCount, 0);
            }
            catch (InvalidDataException ex)
            {
                return Fail(ex.Message, synthetic, manifestReadCount, 1);
            }

            var signerFingerprint = Convert.ToHexString(SHA256.HashData(signer.RawData));
            if (!policy.AllowedSignerCertificateSha256.Contains(signerFingerprint))
            {
                return Fail("signer certificate SHA-256 fingerprint is not allowed by the deployment trust policy", synthetic, manifestReadCount, policyReadCount);
            }

            if (!HasRequiredEkus(signer, policy.RequiredEkuOids))
            {
                return Fail("signer certificate does not satisfy the deployment trust policy EKU/OID requirement", synthetic, manifestReadCount, policyReadCount);
            }

            if (!synthetic)
            {
                var chain = BuildCompanyChain(signer, policy.RevocationMode);
                if (chain == ChainDisposition.MissingTool)
                {
                    return MissingTool("certificate-chain capability is unavailable", false, manifestReadCount, policyReadCount);
                }

                if (chain == ChainDisposition.Blocked)
                {
                    return Blocked("certificate revocation or trust-chain evidence is unavailable", false, manifestReadCount, policyReadCount);
                }

                if (chain == ChainDisposition.Invalid)
                {
                    return Fail("signer certificate chain is not trusted by the company certificate policy", false, manifestReadCount, policyReadCount);
                }
            }

            var evidence = synthetic
                ? "synthetic cryptographic contract evidence only; policy v2 matched; manifest and policy snapshots verified once"
                : "company-managed policy v2 verified; signer chain and revocation policy verified; manifest and policy snapshots verified once";
            return new VerificationResult("PASS", "RUNNABLE_NOW", 0, null, evidence, synthetic, manifestReadCount, policyReadCount);
        }
        catch (DecoderFallbackException)
        {
            return Fail("manifest or policy is not strict UTF-8", synthetic);
        }
        catch (FileNotFoundException)
        {
            return Fail("approved manifest or detached signature is unavailable", synthetic);
        }
        catch (UnauthorizedAccessException)
        {
            return Fail("approved manifest or detached signature could not be read", synthetic);
        }
    }

    private static VerificationResult? ValidateEvidencePair(
        string manifestPath,
        string signaturePath,
        string trustedRoot,
        string? repositoryRoot,
        bool synthetic)
    {
        string rootFull;
        string manifestFull;
        string signatureFull;
        try
        {
            rootFull = CanonicalPathPolicy.CanonicalizeRoot(trustedRoot);
            manifestFull = Path.GetFullPath(manifestPath);
            signatureFull = Path.GetFullPath(signaturePath);
        }
        catch (Exception)
        {
            return Fail("trusted evidence path syntax is invalid", synthetic);
        }

        if (!Directory.Exists(rootFull))
        {
            return Blocked("trusted evidence root is unavailable", synthetic);
        }

        if (HasTraversalSegment(manifestPath) || HasTraversalSegment(signaturePath))
        {
            return Fail("trusted evidence path contains traversal", synthetic);
        }

        if (ContainsReparsePoint(rootFull))
        {
            return Blocked("trusted evidence root is a reparse point and cannot establish trust", synthetic);
        }

        foreach (var candidate in new[] { manifestFull, signatureFull })
        {
            if (!CanonicalPathPolicy.IsContainedPath(candidate, rootFull) || ContainsReparsePoint(candidate))
            {
                return Fail("manifest and signature must remain inside the trusted evidence root", synthetic);
            }

            if (!string.IsNullOrWhiteSpace(repositoryRoot))
            {
                try
                {
                    var repositoryFull = CanonicalPathPolicy.CanonicalizeRoot(repositoryRoot);
                    if (CanonicalPathPolicy.IsContainedPath(candidate, repositoryFull))
                    {
                        return Fail("signed evidence must not be loaded from the repository", synthetic);
                    }
                }
                catch (Exception)
                {
                    return Fail("repository path syntax is invalid", synthetic);
                }
            }

            if (!File.Exists(candidate) || (File.GetAttributes(candidate) & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
            {
                return Fail("manifest and signature must be existing regular files", synthetic);
            }
        }

        return null;
    }

    private static bool HasTraversalSegment(string path) => path
        .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
        .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)
        .Any(segment => segment == "..");

    private static bool ContainsReparsePoint(string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            var root = Path.GetPathRoot(fullPath) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(root))
            {
                return true;
            }

            var current = Path.GetFullPath(root);
            if (IsExistingReparsePoint(current))
            {
                return true;
            }

            var remainder = fullPath[root.Length..];
            foreach (var segment in remainder.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
            {
                current = Path.GetFullPath(Path.Combine(current, segment));
                if (IsExistingReparsePoint(current))
                {
                    return true;
                }
            }
            return false;
        }
        catch
        {
            return true;
        }
    }

    private static bool IsExistingReparsePoint(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            return false;
        }

        return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
    }

    private static Dictionary<string, string> ParseArguments(IReadOnlyList<string> args)
    {
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < args.Count; index++)
        {
            if (!args[index].StartsWith("--", StringComparison.Ordinal) || index + 1 >= args.Count)
            {
                throw new ArgumentException("invalid verifier arguments");
            }

            var name = args[index][2..];
            if (!options.TryAdd(name, args[++index]))
            {
                throw new ArgumentException("duplicate verifier argument");
            }
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
        return ReadAll(stream);
    }

    private static byte[] ReadAll(Stream stream)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    [SupportedOSPlatform("windows")]
    private static TrustPolicy ReadPolicySnapshot(string path, bool enforceCompanySecurity, out int readCount)
    {
        readCount = 0;
        if (enforceCompanySecurity && !string.Equals(Path.GetFullPath(path), Path.GetFullPath(ProductionPolicyPath), StringComparison.OrdinalIgnoreCase))
        {
            throw new CompanyPolicyUnavailableException("company-managed policy path is not canonical");
        }

        try
        {
            if (ContainsReparsePoint(path))
            {
                throw new CompanyPolicyUnavailableException("company-managed deployment trust policy is a reparse point");
            }

            using var policyHandle = HandleSecurityEvaluator.OpenReadOnly(path, directory: false);
            using var stream = new FileStream(policyHandle, FileAccess.Read, 4096, isAsync: false);
            var identityBefore = HandleSecurityEvaluator.ReadIdentity(policyHandle);
            readCount = 1;

            if (enforceCompanySecurity)
            {
                var policySecurity = HandleSecurityEvaluator.Assess(policyHandle, HandleSecurityTarget.PolicyFile);
                if (policySecurity.OwnedByCurrentUser || policySecurity.HasUnsafeEffectiveAccess)
                {
                    throw new CompanyPolicyUnavailableException("company-managed policy file access is not trust-bounded");
                }

                var parent = Directory.GetParent(path);
                if (parent is null || ContainsReparsePoint(parent.FullName))
                {
                    throw new CompanyPolicyUnavailableException("company-managed policy directory is not canonical");
                }

                using var parentHandle = HandleSecurityEvaluator.OpenReadOnly(parent.FullName, directory: true);
                var parentSecurity = HandleSecurityEvaluator.Assess(parentHandle, HandleSecurityTarget.ImmediateDirectory);
                if (parentSecurity.OwnedByCurrentUser || parentSecurity.HasUnsafeEffectiveAccess)
                {
                    throw new CompanyPolicyUnavailableException("company-managed policy directory access is not trust-bounded");
                }

                var ancestor = parent.Parent;
                while (ancestor is not null)
                {
                    if (ContainsReparsePoint(ancestor.FullName))
                    {
                        throw new CompanyPolicyUnavailableException("company-managed policy ancestor is not canonical");
                    }

                    using var ancestorHandle = HandleSecurityEvaluator.OpenReadOnly(ancestor.FullName, directory: true);
                    var ancestorSecurity = HandleSecurityEvaluator.Assess(ancestorHandle, HandleSecurityTarget.AncestorDirectory);
                    if (ancestorSecurity.OwnedByCurrentUser || ancestorSecurity.HasUnsafeEffectiveAccess)
                    {
                        throw new CompanyPolicyUnavailableException("company-managed policy ancestor access is not trust-bounded");
                    }

                    var root = Path.GetPathRoot(ancestor.FullName);
                    if (string.Equals(Path.GetFullPath(ancestor.FullName), Path.GetFullPath(root ?? string.Empty), StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }

                    ancestor = ancestor.Parent;
                }
            }

            var bytes = ReadAll(stream);
            var identityAfter = HandleSecurityEvaluator.ReadIdentity(policyHandle);
            if (identityBefore != identityAfter)
            {
                throw new CompanyPolicyUnavailableException("company-managed policy identity changed during verification");
            }

            try
            {
                using var document = JsonDocument.Parse(
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes));
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object ||
                    !root.TryGetProperty("policyVersion", out var version) ||
                    version.ValueKind != JsonValueKind.Number || version.GetInt32() != 2)
                {
                    throw new InvalidDataException("deployment trust policy version is unsupported");
                }

                var fingerprints = ReadStringArray(root, "allowedSignerCertificateSha256")
                    .Select(NormalizeFingerprint)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (fingerprints.Count == 0 || fingerprints.Any(value => !IsSha256(value)))
                {
                    throw new InvalidDataException("deployment trust policy requires 64-character SHA-256 certificate fingerprints");
                }

                var ekuOids = ReadStringArray(root, "requiredEkuOids").ToHashSet(StringComparer.Ordinal);
                var revocationText = root.TryGetProperty("revocationMode", out var revocation) &&
                    revocation.ValueKind == JsonValueKind.String
                    ? revocation.GetString()
                    : null;
                var revocationMode = revocationText switch
                {
                    "Online" => X509RevocationMode.Online,
                    "Offline" => X509RevocationMode.Offline,
                    _ => throw new InvalidDataException("deployment trust policy requires revocationMode Online or Offline")
                };

                return new TrustPolicy(fingerprints, ekuOids, revocationMode);
            }
            catch (JsonException)
            {
                throw new InvalidDataException("deployment trust policy is malformed");
            }
            catch (DecoderFallbackException)
            {
                throw new InvalidDataException("deployment trust policy is not strict UTF-8");
            }
        }
        catch (HandleSecurityCapabilityUnavailableException ex)
        {
            throw new CapabilityUnavailableException(ex.Message);
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

            foreach (var field in RequiredManifestFields)
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

    private static bool HasStrongPublicKey(X509Certificate2 certificate)
    {
        using var rsa = certificate.GetRSAPublicKey();
        if (rsa is not null)
        {
            return rsa.KeySize >= 2048;
        }

        using var ecdsa = certificate.GetECDsaPublicKey();
        if (ecdsa is not null)
        {
            var oid = ecdsa.ExportParameters(false).Curve.Oid.Value;
            return oid == "1.2.840.10045.3.1.7"; // NIST P-256
        }

        return false;
    }

    [SupportedOSPlatform("windows")]
    private static ChainDisposition BuildCompanyChain(X509Certificate2 signer, X509RevocationMode revocationMode)
    {
        try
        {
            using var chain = new X509Chain();
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.System;
            chain.ChainPolicy.RevocationMode = revocationMode;
            chain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
            chain.ChainPolicy.UrlRetrievalTimeout = TimeSpan.FromSeconds(5);
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
            if (chain.Build(signer))
            {
                return ChainDisposition.Valid;
            }

            return ChainStatusClassifier.Classify(chain.ChainStatus.Select(status => status.Status), buildSucceeded: false);
        }
        catch (PlatformNotSupportedException)
        {
            return ChainStatusClassifier.ClassifyException(new PlatformNotSupportedException());
        }
        catch (CryptographicException)
        {
            return ChainStatusClassifier.ClassifyException(new CryptographicException());
        }
    }

    private static bool IsSha256(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length == 64 &&
        value.All(character => Uri.IsHexDigit(character));

    private static string NormalizeFingerprint(string value) =>
        value.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();

    private static VerificationResult Blocked(string evidence, bool synthetic, int manifestReadCount = 0, int policyReadCount = 0) =>
        new("BLOCKED", "BLOCKED_BY_COMPANY_APPROVAL", 20, "BLK-ENV-005", evidence, synthetic, manifestReadCount, policyReadCount);

    private static VerificationResult Fail(string evidence, bool synthetic = false, int manifestReadCount = 0, int policyReadCount = 0) =>
        new("FAIL", "RUNNABLE_NOW", 1, null, evidence, synthetic, manifestReadCount, policyReadCount);

    private static VerificationResult MissingTool(string evidence, bool synthetic = false, int manifestReadCount = 0, int policyReadCount = 0) =>
        new("BLOCKED", "BLOCKED_BY_MISSING_TOOL", 20, "BLK-ENV-001", evidence, synthetic, manifestReadCount, policyReadCount);

    private sealed record TrustPolicy(
        IReadOnlySet<string> AllowedSignerCertificateSha256,
        IReadOnlySet<string> RequiredEkuOids,
        X509RevocationMode RevocationMode);

    private sealed record VerificationResult(
        string Status,
        string Classification,
        int ExitCode,
        string? BlockerId,
        string Evidence,
        bool Synthetic,
        int ManifestReadCount,
        int PolicyReadCount);

    private sealed class CapabilityUnavailableException(string message) : Exception(message);

    private sealed class CompanyPolicyUnavailableException(string message) : Exception(message);
}

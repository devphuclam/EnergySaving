using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

var options = Parse(args);
var variant = options.GetValueOrDefault("variant") ?? "valid";

if (options.TryGetValue("chain-status", out var chainScenario))
{
    var statuses = chainScenario switch
    {
        "fatal" => new[] { X509ChainStatusFlags.Revoked, X509ChainStatusFlags.NotTimeValid },
        "mixed" => new[] { X509ChainStatusFlags.Revoked, X509ChainStatusFlags.OfflineRevocation },
        "revocation-unavailable" => new[] { X509ChainStatusFlags.RevocationStatusUnknown, X509ChainStatusFlags.OfflineRevocation },
        "empty" => Array.Empty<X509ChainStatusFlags>(),
        _ => throw new ArgumentException("unknown chain-status scenario")
    };
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        disposition = ChainStatusClassifier.Classify(statuses, buildSucceeded: false).ToString()
    }));
    Environment.Exit(0);
}

if (options.TryGetValue("chain-exception", out var chainException))
{
    Exception exception = chainException switch
    {
        "crypto" => new CryptographicException(),
        "platform" => new PlatformNotSupportedException(),
        _ => throw new ArgumentException("unknown chain-exception scenario")
    };
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        disposition = ChainStatusClassifier.ClassifyException(exception).ToString()
    }));
    Environment.Exit(0);
}

if (options.TryGetValue("acl-status", out var aclScenario))
{
    var currentSid = WindowsIdentity.GetCurrent().User ?? throw new InvalidOperationException("current user SID unavailable");
    var principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
    var unsafeRights = aclScenario switch
    {
        "replacement" => FileSystemRights.ChangePermissions | FileSystemRights.TakeOwnership,
        "delete-child" => FileSystemRights.DeleteSubdirectoriesAndFiles,
        _ => FileSystemRights.WriteData
    };
    var targetIsDirectory = aclScenario != "inherited-file";
    bool isUnsafe;
    if (aclScenario is "inherited" or "inherited-file")
    {
        var aclRoot = Path.Combine(Path.GetTempPath(), "iump-acl-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(aclRoot);
        try
        {
            var parent = new DirectoryInfo(aclRoot);
            var parentSecurity = parent.GetAccessControl();
            var inheritance = aclScenario == "inherited"
                ? InheritanceFlags.ContainerInherit
                : InheritanceFlags.ObjectInherit;
            parentSecurity.AddAccessRule(new FileSystemAccessRule(
                currentSid, unsafeRights, inheritance, PropagationFlags.None, AccessControlType.Allow));
            parent.SetAccessControl(parentSecurity);
            FileSystemSecurity targetSecurity;
            if (aclScenario == "inherited")
            {
                var child = Directory.CreateDirectory(Path.Combine(aclRoot, "child"));
                targetSecurity = child.GetAccessControl();
            }
            else
            {
                var childFile = new FileInfo(Path.Combine(aclRoot, "child.txt"));
                using (childFile.Create()) { }
                targetSecurity = childFile.GetAccessControl();
            }
            isUnsafe = PolicyAclEvaluator.HasEffectiveUnsafePermission(
                targetSecurity.GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier))
                    .Cast<FileSystemAccessRule>(), currentSid, principal, unsafeRights, targetIsDirectory);
        }
        finally
        {
            try { Directory.Delete(aclRoot, recursive: true); } catch { }
        }
    }
    else
    {
        IEnumerable<FileSystemAccessRule> rules = aclScenario switch
        {
            "allow" => new[] { new FileSystemAccessRule(currentSid, unsafeRights, AccessControlType.Allow) },
            "deny" => new[]
            {
                new FileSystemAccessRule(currentSid, unsafeRights, AccessControlType.Allow),
                new FileSystemAccessRule(currentSid, unsafeRights, AccessControlType.Deny)
            },
            "inherit-only" => new[]
            {
                new FileSystemAccessRule(currentSid, unsafeRights, InheritanceFlags.ContainerInherit,
                    PropagationFlags.InheritOnly, AccessControlType.Allow)
            },
            "replacement" or "delete-child" => new[]
            {
                new FileSystemAccessRule(currentSid, unsafeRights, AccessControlType.Allow)
            },
            "other-user" => new[]
            {
                new FileSystemAccessRule(new SecurityIdentifier("S-1-5-21-111111111-222222222-333333333-999"),
                    unsafeRights, AccessControlType.Allow)
            },
            _ => throw new ArgumentException("unknown acl-status scenario")
        };
        isUnsafe = PolicyAclEvaluator.HasEffectiveUnsafePermission(
            rules, currentSid, principal, unsafeRights, targetIsDirectory);
    }
    Console.WriteLine(JsonSerializer.Serialize(new { unsafePermission = isUnsafe }));
    Environment.Exit(0);
}

if (options.TryGetValue("root-path", out var rootPathScenario))
{
    var input = rootPathScenario switch
    {
        "drive" => Path.GetPathRoot(Environment.CurrentDirectory) ?? throw new InvalidOperationException("drive root unavailable"),
        "unc" => "\\\\server\\share\\",
        _ => throw new ArgumentException("unknown root-path scenario")
    };
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        canonicalRoot = CanonicalPathPolicy.CanonicalizeRoot(input)
    }));
    Environment.Exit(0);
}

var root = Require(options, "root");
Directory.CreateDirectory(root);

var manifestPath = Path.Combine(root, "manifest.json");
var signaturePath = Path.Combine(root, "manifest.p7s");
var policyPath = Path.Combine(root, "policy.json");
var manifest = new Dictionary<string, string>
{
    ["deploymentModel"] = "restricted-non-containerized",
    ["webHosting"] = "static-files",
    ["apiHosting"] = "Windows Service",
    ["workerServiceManager"] = "Windows Service",
    ["databaseHosting"] = "internal PostgreSQL",
    ["lifecycleRunbookReference"] = "RUNBOOK-DEPLOYMENT-001",
    ["rollbackRunbookReference"] = "RUNBOOK-ROLLBACK-001",
    ["approvalReference"] = "APPROVAL-TEST-001",
    ["approvedBy"] = "Infrastructure Security",
    ["approvedAtUtc"] = DateTimeOffset.UtcNow.AddMinutes(-1).ToString("O")
};

if (variant == "secret")
{
    manifest["apiKey"] = "synthetic-only";
}

if (variant == "wrong-model")
{
    manifest["deploymentModel"] = "containerized";
}
else if (variant == "non-utc")
{
    manifest["approvedAtUtc"] = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss");
}
else if (variant == "future")
{
    manifest["approvedAtUtc"] = DateTimeOffset.UtcNow.AddDays(1).ToString("O");
}
else if (variant == "missing-field")
{
    manifest.Remove("webHosting");
}

var manifestBytes = variant switch
{
    "malformed-json" => Encoding.UTF8.GetBytes("{\"deploymentModel\":"),
    "non-scalar" => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new Dictionary<string, object?>
    {
        ["deploymentModel"] = "restricted-non-containerized",
        ["webHosting"] = new { value = "static-files" },
        ["apiHosting"] = "Windows Service",
        ["workerServiceManager"] = "Windows Service",
        ["databaseHosting"] = "internal PostgreSQL",
        ["lifecycleRunbookReference"] = "RUNBOOK-DEPLOYMENT-001",
        ["rollbackRunbookReference"] = "RUNBOOK-ROLLBACK-001",
        ["approvalReference"] = "APPROVAL-TEST-001",
        ["approvedBy"] = "Infrastructure Security",
        ["approvedAtUtc"] = DateTimeOffset.UtcNow.AddMinutes(-1).ToString("O")
    })),
    _ => JsonSerializer.SerializeToUtf8Bytes(manifest)
};
File.WriteAllBytes(manifestPath, manifestBytes);

var now = DateTimeOffset.UtcNow;
var notBefore = variant == "expired" ? now.AddYears(-2) : now.AddMinutes(-5);
var notAfter = variant == "expired" ? now.AddYears(-1) : now.AddYears(1);
using var rsa = RSA.Create(variant == "weak-rsa" ? 1024 : 2048);
var request = new CertificateRequest(
    "CN=IUMP Synthetic Deployment Signer",
    rsa,
    HashAlgorithmName.SHA256,
    RSASignaturePadding.Pkcs1);
using var certificate = request.CreateSelfSigned(notBefore, notAfter);
var cms = new SignedCms(new ContentInfo(manifestBytes), detached: true);
var cmsSigner = new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, certificate);
if (variant == "sha1")
{
    cmsSigner.DigestAlgorithm = new Oid("1.3.14.3.2.26");
}
else if (variant == "md5")
{
    cmsSigner.DigestAlgorithm = new Oid("1.2.840.113549.2.5");
}
cms.ComputeSignature(cmsSigner);
File.WriteAllBytes(signaturePath, cms.Encode());

var certificateSha256 = Convert.ToHexString(SHA256.HashData(certificate.RawData));
var allowedCertificateSha256 = variant == "wrong-signer"
    ? new string('0', 64)
    : certificateSha256;
var requiredEkus = variant == "eku-mismatch" ? ["1.2.3.4.5.6.7"] : Array.Empty<string>();
var policy = new
{
    policyVersion = variant == "policy-v1" ? 1 : 2,
    allowedSignerCertificateSha256 = new[] { allowedCertificateSha256 },
    requiredEkuOids = requiredEkus,
    revocationMode = "Offline"
};
File.WriteAllText(policyPath, JsonSerializer.Serialize(policy));

Console.WriteLine(JsonSerializer.Serialize(new
{
    manifestPath,
    signaturePath,
    policyPath,
    thumbprint = certificate.Thumbprint
}));

static Dictionary<string, string> Parse(IReadOnlyList<string> args)
{
    var result = new Dictionary<string, string>(StringComparer.Ordinal);
    for (var index = 0; index < args.Count; index++)
    {
        if (!args[index].StartsWith("--", StringComparison.Ordinal) || index + 1 >= args.Count)
        {
            throw new ArgumentException("invalid fixture arguments");
        }

        result[args[index][2..]] = args[++index];
    }

    return result;
}

static string Require(IReadOnlyDictionary<string, string> options, string key) =>
    options.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
        ? value
        : throw new ArgumentException("missing fixture argument");

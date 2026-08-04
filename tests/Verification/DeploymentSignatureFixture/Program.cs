using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

var options = Parse(args);
var root = Require(options, "root");
var variant = options.GetValueOrDefault("variant") ?? "valid";
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

var manifestBytes = JsonSerializer.SerializeToUtf8Bytes(manifest);
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

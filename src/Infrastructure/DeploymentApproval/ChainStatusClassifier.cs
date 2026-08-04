using System.Security.Cryptography.X509Certificates;

internal enum ChainDisposition
{
    Valid,
    Invalid,
    Blocked,
    MissingTool
}

internal static class ChainStatusClassifier
{
    public static ChainDisposition ClassifyException(Exception exception) =>
        exception is PlatformNotSupportedException
            ? ChainDisposition.MissingTool
            : ChainDisposition.Invalid;

    public static ChainDisposition Classify(IEnumerable<X509ChainStatusFlags> statuses, bool buildSucceeded)
    {
        if (buildSucceeded)
        {
            return ChainDisposition.Valid;
        }

        var combined = statuses.Aggregate(X509ChainStatusFlags.NoError, (current, status) => current | status);
        var revocationUnavailable = X509ChainStatusFlags.RevocationStatusUnknown |
            X509ChainStatusFlags.OfflineRevocation;
        if ((combined & ~revocationUnavailable) != X509ChainStatusFlags.NoError)
        {
            return ChainDisposition.Invalid;
        }

        return (combined & revocationUnavailable) != X509ChainStatusFlags.NoError
            ? ChainDisposition.Blocked
            : ChainDisposition.Invalid;
    }
}

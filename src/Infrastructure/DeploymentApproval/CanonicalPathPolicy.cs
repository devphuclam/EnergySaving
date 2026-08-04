internal static class CanonicalPathPolicy
{
    public static string CanonicalizeRoot(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var pathRoot = Path.GetPathRoot(fullPath);
        if (string.IsNullOrWhiteSpace(pathRoot))
        {
            throw new ArgumentException("path has no rooted path");
        }

        return string.Equals(fullPath, pathRoot, StringComparison.OrdinalIgnoreCase)
            ? pathRoot
            : fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    public static bool IsContainedPath(string candidate, string root)
    {
        var canonicalRoot = CanonicalizeRoot(root);
        var canonicalCandidate = Path.GetFullPath(candidate);
        if (string.Equals(canonicalCandidate, canonicalRoot, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var prefix = canonicalRoot.EndsWith(Path.DirectorySeparatorChar) || canonicalRoot.EndsWith(Path.AltDirectorySeparatorChar)
            ? canonicalRoot
            : canonicalRoot + Path.DirectorySeparatorChar;
        return canonicalCandidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
}

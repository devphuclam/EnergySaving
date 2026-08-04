using System.Security.AccessControl;
using System.Security.Principal;
using System.Runtime.Versioning;

[SupportedOSPlatform("windows")]
internal static class PolicyAclEvaluator
{
    public static bool HasEffectiveUnsafePermission(
        IEnumerable<FileSystemAccessRule> rules,
        SecurityIdentifier currentSid,
        WindowsPrincipal principal,
        FileSystemRights unsafeRights,
        bool targetIsDirectory)
    {
        var denied = (FileSystemRights)0;
        var allowed = (FileSystemRights)0;

        foreach (var rule in rules)
        {
            if (!IsApplicableAccessRule(rule, targetIsDirectory, currentSid, principal))
            {
                continue;
            }

            var applicableRights = rule.FileSystemRights & unsafeRights;
            if (applicableRights == (FileSystemRights)0)
            {
                continue;
            }

            if (rule.AccessControlType == AccessControlType.Deny)
            {
                denied |= applicableRights;
            }
            else
            {
                allowed |= applicableRights;
            }
        }

        return (allowed & ~denied & unsafeRights) != (FileSystemRights)0;
    }

    private static bool IsApplicableAccessRule(
        FileSystemAccessRule rule,
        bool targetIsDirectory,
        SecurityIdentifier currentSid,
        WindowsPrincipal principal)
    {
        if (rule.IdentityReference is not SecurityIdentifier sid ||
            (sid.Value != currentSid.Value && !principal.IsInRole(sid)))
        {
            return false;
        }

        if ((rule.PropagationFlags & PropagationFlags.InheritOnly) != 0)
        {
            return false;
        }

        if (!rule.IsInherited)
        {
            return true;
        }

        var requiredInheritance = targetIsDirectory
            ? InheritanceFlags.ContainerInherit
            : InheritanceFlags.ObjectInherit;
        return (rule.InheritanceFlags == InheritanceFlags.None ||
            (rule.InheritanceFlags & requiredInheritance) != 0) &&
            (rule.PropagationFlags & PropagationFlags.InheritOnly) == 0;
    }
}

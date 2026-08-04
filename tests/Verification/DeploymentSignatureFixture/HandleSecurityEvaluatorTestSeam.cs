using System;
using System.Runtime.Versioning;

[SupportedOSPlatform("windows")]
internal static partial class HandleSecurityEvaluator
{
    // Fixture-only seam: exercise the production AccessCheck implementation without making an
    // in-memory descriptor a production trust authority.
    internal static bool HasUnsafeEffectiveAccessForTest(
        IntPtr securityDescriptor,
        HandleSecurityTarget target)
    {
        if (securityDescriptor == IntPtr.Zero || !IsValidSecurityDescriptor(securityDescriptor))
        {
            throw new HandleSecurityCapabilityUnavailableException(
                "Windows test security descriptor is invalid");
        }

        using var token = OpenCurrentProcessToken();
        return HasAnyEffectiveAccess(securityDescriptor, token, GetUnsafeRights(target));
    }
}

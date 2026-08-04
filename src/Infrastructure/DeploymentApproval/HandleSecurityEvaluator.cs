using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;

[SupportedOSPlatform("windows")]
internal static class HandleSecurityEvaluator
{
    private const uint GenericRead = 0x80000000;
    private const uint ReadControl = 0x00020000;
    private const uint FileReadAttributes = 0x00000080;
    private const uint FileAddFile = 0x00000002;
    private const uint FileAddSubdirectory = 0x00000004;
    private const uint FileDeleteChild = 0x00000040;
    private const uint FileWriteData = 0x00000002;
    private const uint FileAppendData = 0x00000004;
    private const uint FileWriteExtendedAttributes = 0x00000010;
    private const uint FileWriteAttributes = 0x00000100;
    private const uint Delete = 0x00010000;
    private const uint WriteDac = 0x00040000;
    private const uint WriteOwner = 0x00080000;
    private const uint FileShareRead = 0x00000001;
    private const uint OpenExisting = 3;
    private const uint FileFlagBackupSemantics = 0x02000000;
    private const uint TokenQuery = 0x0008;
    private const uint TokenDuplicate = 0x0002;
    private const uint TokenImpersonate = 0x0004;
    private const uint OwnerSecurityInformation = 0x00000001;
    private const uint GroupSecurityInformation = 0x00000002;
    private const uint DaclSecurityInformation = 0x00000004;
    private const uint ErrorFileNotFound = 2;
    private const uint ErrorPathNotFound = 3;
    private const uint ErrorAccessDenied = 5;
    private const uint ErrorInsufficientBuffer = 122;
    private const uint InvalidSecurityDescriptor = 0;

    private static readonly uint[] PolicyFileUnsafeRights =
    [
        FileWriteData,
        FileAppendData,
        FileWriteExtendedAttributes,
        FileWriteAttributes,
        Delete,
        WriteDac,
        WriteOwner
    ];

    private static readonly uint[] ImmediateDirectoryUnsafeRights =
    [
        FileAddFile,
        FileAddSubdirectory,
        FileDeleteChild,
        Delete,
        WriteDac,
        WriteOwner
    ];

    private static readonly uint[] AncestorUnsafeRights =
    [
        Delete,
        WriteDac,
        WriteOwner
    ];

    public static SafeFileHandle OpenReadOnly(string path, bool directory)
    {
        var desiredAccess = directory
            ? ReadControl | FileReadAttributes
            : GenericRead | ReadControl;
        var flags = directory ? FileFlagBackupSemantics : 0u;
        var handle = CreateFile(
            path,
            desiredAccess,
            FileShareRead,
            IntPtr.Zero,
            OpenExisting,
            flags,
            IntPtr.Zero);

        if (handle.IsInvalid)
        {
            var error = (uint)Marshal.GetLastWin32Error();
            handle.Dispose();
            if (error is ErrorFileNotFound or ErrorPathNotFound)
            {
                throw directory
                    ? new DirectoryNotFoundException("policy directory is unavailable")
                    : new FileNotFoundException("policy file is unavailable");
            }

            if (error == ErrorAccessDenied)
            {
                throw new UnauthorizedAccessException("policy handle could not be opened");
            }

            throw new HandleSecurityCapabilityUnavailableException(
                $"Windows policy handle capability returned error {error}");
        }

        return handle;
    }

    public static FileIdentity ReadIdentity(SafeFileHandle handle)
    {
        if (!GetFileInformationByHandle(handle, out var info))
        {
            throw new HandleSecurityCapabilityUnavailableException(
                "Windows file identity capability is unavailable");
        }

        return new FileIdentity(
            info.VolumeSerialNumber,
            ((ulong)info.FileIndexHigh << 32) | info.FileIndexLow);
    }

    public static HandleSecurityAssessment Assess(
        SafeFileHandle handle,
        HandleSecurityTarget target)
    {
        using var currentIdentity = WindowsIdentity.GetCurrent(TokenAccessLevels.Query);
        var currentSid = currentIdentity.User ?? throw new HandleSecurityCapabilityUnavailableException(
            "current process token has no user SID");

        using var token = OpenCurrentProcessToken();
        var descriptor = ReadSecurityDescriptor(handle, out var owner);
        try
        {
            var ownerSid = owner == IntPtr.Zero
                ? null
                : new SecurityIdentifier(owner);
            var ownedByCurrentUser = ownerSid is not null && ownerSid.Value == currentSid.Value;
            var unsafeRights = target switch
            {
                HandleSecurityTarget.PolicyFile => PolicyFileUnsafeRights,
                HandleSecurityTarget.ImmediateDirectory => ImmediateDirectoryUnsafeRights,
                HandleSecurityTarget.AncestorDirectory => AncestorUnsafeRights,
                _ => throw new ArgumentOutOfRangeException(nameof(target))
            };
            var hasUnsafeAccess = HasAnyEffectiveAccess(descriptor, token, unsafeRights);
            return new HandleSecurityAssessment(
                ReadIdentity(handle),
                ownedByCurrentUser,
                hasUnsafeAccess);
        }
        finally
        {
            if (descriptor != IntPtr.Zero)
            {
                _ = LocalFree(descriptor);
            }
        }
    }

    private static SafeAccessTokenHandle OpenCurrentProcessToken()
    {
        if (!OpenProcessToken(GetCurrentProcess(), TokenQuery | TokenDuplicate, out var token) || token.IsInvalid)
        {
            token?.Dispose();
            throw new HandleSecurityCapabilityUnavailableException(
                "current process token capability is unavailable");
        }

        using (token)
        {
            if (!DuplicateTokenEx(
                    token.DangerousGetHandle(),
                    TokenQuery | TokenImpersonate | TokenDuplicate,
                    IntPtr.Zero,
                    SecurityImpersonationLevel.Impersonation,
                    TokenType.Impersonation,
                    out var impersonationToken) || impersonationToken.IsInvalid)
            {
                impersonationToken?.Dispose();
                throw new HandleSecurityCapabilityUnavailableException(
                    "Windows impersonation-token capability is unavailable");
            }

            return impersonationToken;
        }
    }

    private static IntPtr ReadSecurityDescriptor(SafeFileHandle handle, out IntPtr owner)
    {
        var result = GetSecurityInfo(
            handle,
            SeObjectType.File,
            OwnerSecurityInformation | GroupSecurityInformation | DaclSecurityInformation,
            out owner,
            out _,
            out _,
            out _,
            out var descriptor);
        if (result != 0 || descriptor == InvalidSecurityDescriptor)
        {
            throw new HandleSecurityCapabilityUnavailableException(
                "Windows handle security-descriptor capability is unavailable");
        }

        if (!IsValidSecurityDescriptor(descriptor))
        {
            throw new HandleSecurityCapabilityUnavailableException(
                "Windows handle returned an invalid security descriptor");
        }

        return descriptor;
    }

    private static bool HasAnyEffectiveAccess(
        IntPtr descriptor,
        SafeAccessTokenHandle token,
        IReadOnlyList<uint> rights)
    {
        foreach (var right in rights)
        {
            if (CheckEffectiveAccess(descriptor, token, right))
            {
                return true;
            }
        }

        return false;
    }

    private static bool CheckEffectiveAccess(
        IntPtr descriptor,
        SafeAccessTokenHandle token,
        uint desiredAccess)
    {
        var mapping = new GenericMapping(
            genericRead: 0x00120089,
            genericWrite: 0x00120116,
            genericExecute: 0x001200A0,
            genericAll: 0x001F01FF);
        var privilegeSetLength = 1024u;
        var privilegeSet = Marshal.AllocHGlobal((int)privilegeSetLength);
        for (var index = 0; index < privilegeSetLength; index++)
        {
            Marshal.WriteByte(privilegeSet, (int)index, 0);
        }
        try
        {
            if (AccessCheck(
                    descriptor,
                    token,
                    desiredAccess,
                    ref mapping,
                    privilegeSet,
                    ref privilegeSetLength,
                    out _,
                    out var accessStatus))
            {
                return accessStatus;
            }

            var error = (uint)Marshal.GetLastWin32Error();
            if (error == ErrorInsufficientBuffer)
            {
                Marshal.FreeHGlobal(privilegeSet);
                privilegeSet = Marshal.AllocHGlobal((int)privilegeSetLength);
                if (AccessCheck(
                        descriptor,
                        token,
                        desiredAccess,
                        ref mapping,
                        privilegeSet,
                        ref privilegeSetLength,
                        out _,
                        out accessStatus))
                {
                    return accessStatus;
                }
            }

            throw new HandleSecurityCapabilityUnavailableException(
                $"Windows effective-access capability returned error {Marshal.GetLastWin32Error()}");
        }
        finally
        {
            Marshal.FreeHGlobal(privilegeSet);
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle handle,
        out ByHandleFileInformation information);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(
        IntPtr processHandle,
        uint desiredAccess,
        out SafeAccessTokenHandle tokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DuplicateTokenEx(
        IntPtr existingToken,
        uint desiredAccess,
        IntPtr tokenAttributes,
        SecurityImpersonationLevel impersonationLevel,
        TokenType tokenType,
        out SafeAccessTokenHandle duplicateToken);

    [DllImport("advapi32.dll", SetLastError = false)]
    private static extern uint GetSecurityInfo(
        SafeFileHandle handle,
        SeObjectType objectType,
        uint securityInformation,
        out IntPtr owner,
        out IntPtr group,
        out IntPtr dacl,
        out IntPtr sacl,
        out IntPtr securityDescriptor);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsValidSecurityDescriptor(IntPtr securityDescriptor);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AccessCheck(
        IntPtr securityDescriptor,
        SafeAccessTokenHandle clientToken,
        uint desiredAccess,
        ref GenericMapping genericMapping,
        IntPtr privilegeSet,
        ref uint privilegeSetLength,
        out uint grantedAccess,
        [MarshalAs(UnmanagedType.Bool)] out bool accessStatus);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LocalFree(IntPtr memory);

    private enum SeObjectType : uint
    {
        File = 1
    }

    private enum SecurityImpersonationLevel
    {
        Anonymous,
        Identification,
        Impersonation,
        Delegation
    }

    private enum TokenType
    {
        Primary = 1,
        Impersonation = 2
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct GenericMapping(uint genericRead, uint genericWrite, uint genericExecute, uint genericAll)
    {
        public readonly uint GenericRead = genericRead;
        public readonly uint GenericWrite = genericWrite;
        public readonly uint GenericExecute = genericExecute;
        public readonly uint GenericAll = genericAll;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct ByHandleFileInformation
    {
        public readonly uint FileAttributes;
        public readonly System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
        public readonly System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
        public readonly System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
        public readonly uint VolumeSerialNumber;
        public readonly uint FileSizeHigh;
        public readonly uint FileSizeLow;
        public readonly uint NumberOfLinks;
        public readonly uint FileIndexHigh;
        public readonly uint FileIndexLow;
    }
}

internal enum HandleSecurityTarget
{
    PolicyFile,
    ImmediateDirectory,
    AncestorDirectory
}

internal readonly record struct FileIdentity(uint VolumeSerialNumber, ulong FileIndex);

internal readonly record struct HandleSecurityAssessment(
    FileIdentity Identity,
    bool OwnedByCurrentUser,
    bool HasUnsafeEffectiveAccess);

internal sealed class HandleSecurityCapabilityUnavailableException(string message) : Exception(message);

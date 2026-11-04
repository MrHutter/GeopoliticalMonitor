using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using GeopoliticalMonitor.Application.Firms;

namespace GeopoliticalMonitor.Infrastructure.Firms;

/// <summary>
/// Stores the single app-entered NASA FIRMS key as a generic credential in Windows Credential Manager (advapi32
/// CredReadW, CredWriteW and CredDeleteW; no package). Windows protects the credential with the signed-in user's
/// account. It is persisted for this user on this computer only (CRED_PERSIST_LOCAL_MACHINE, so it does not roam).
/// Other programs running as the same Windows user can read it: this is account protection, not a vault.
/// The target name is fixed here and is never supplied by the UI or the bridge.
/// </summary>
public sealed class WindowsCredentialFirmsKeyStore : IFirmsKeyStore
{
    public const string CredentialTargetName = "GeopoliticalMonitor/NASA-FIRMS-MAP_KEY";

    private const string CredentialComment = "NASA FIRMS MAP_KEY entered in the app";
    private const uint CredentialTypeGeneric = 1;
    private const uint CredentialPersistLocalMachine = 2;
    private const int ErrorNotFound = 1168;

    public FirmsKeyStoreRead Read()
    {
        if (!OperatingSystem.IsWindows())
        {
            return new FirmsKeyStoreRead(FirmsKeyStoreOutcome.Unavailable);
        }

        if (!CredRead(CredentialTargetName, CredentialTypeGeneric, 0, out var credentialPointer))
        {
            return new FirmsKeyStoreRead(Marshal.GetLastWin32Error() == ErrorNotFound
                ? FirmsKeyStoreOutcome.NotFound
                : FirmsKeyStoreOutcome.Unavailable);
        }

        try
        {
            var credential = Marshal.PtrToStructure<NativeCredential>(credentialPointer);
            if (credential.CredentialBlobSize == 0 || credential.CredentialBlob == IntPtr.Zero)
            {
                return new FirmsKeyStoreRead(FirmsKeyStoreOutcome.NotFound);
            }

            var blob = new byte[credential.CredentialBlobSize];
            try
            {
                Marshal.Copy(credential.CredentialBlob, blob, 0, blob.Length);
                var key = FirmsMapKey.FromInput(Encoding.UTF8.GetString(blob));
                return key is null
                    ? new FirmsKeyStoreRead(FirmsKeyStoreOutcome.NotFound)
                    : new FirmsKeyStoreRead(FirmsKeyStoreOutcome.Succeeded, key);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(blob);
            }
        }
        finally
        {
            CredFree(credentialPointer);
        }
    }

    public FirmsKeyStoreOutcome Write(FirmsMapKey key)
    {
        if (!OperatingSystem.IsWindows())
        {
            return FirmsKeyStoreOutcome.Unavailable;
        }

        var blob = Encoding.UTF8.GetBytes(key.RevealForProviderRequest());
        var blobPointer = Marshal.AllocHGlobal(blob.Length);
        var targetPointer = Marshal.StringToHGlobalUni(CredentialTargetName);
        var commentPointer = Marshal.StringToHGlobalUni(CredentialComment);
        try
        {
            Marshal.Copy(blob, 0, blobPointer, blob.Length);
            var credential = new NativeCredential
            {
                Type = CredentialTypeGeneric,
                TargetName = targetPointer,
                Comment = commentPointer,
                CredentialBlobSize = (uint)blob.Length,
                CredentialBlob = blobPointer,
                Persist = CredentialPersistLocalMachine,
            };
            return CredWrite(ref credential, 0) ? FirmsKeyStoreOutcome.Succeeded : FirmsKeyStoreOutcome.Unavailable;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(blob);
            for (var offset = 0; offset < blob.Length; offset++)
            {
                Marshal.WriteByte(blobPointer, offset, 0);
            }

            Marshal.FreeHGlobal(blobPointer);
            Marshal.FreeHGlobal(targetPointer);
            Marshal.FreeHGlobal(commentPointer);
        }
    }

    public FirmsKeyStoreOutcome Delete()
    {
        if (!OperatingSystem.IsWindows())
        {
            return FirmsKeyStoreOutcome.Unavailable;
        }

        if (CredDelete(CredentialTargetName, CredentialTypeGeneric, 0))
        {
            return FirmsKeyStoreOutcome.Succeeded;
        }

        return Marshal.GetLastWin32Error() == ErrorNotFound ? FirmsKeyStoreOutcome.NotFound : FirmsKeyStoreOutcome.Unavailable;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeCredential
    {
        public uint Flags;
        public uint Type;
        public IntPtr TargetName;
        public IntPtr Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public IntPtr TargetAlias;
        public IntPtr UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, uint type, uint reservedFlag, out IntPtr credential);

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite(ref NativeCredential credential, uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string target, uint type, uint flags);

    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr buffer);
}

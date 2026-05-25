using System.Runtime.InteropServices;
using System.Text;

namespace CodexProfileTray;

internal static class WindowsCredentialStore
{
    private const int CredTypeGeneric = 1;
    private const int CredPersistLocalMachine = 2;

    public static void WriteSecret(string providerId, string secret)
    {
        if (string.IsNullOrWhiteSpace(providerId))
        {
            throw new ArgumentException("Provider id is required.", nameof(providerId));
        }

        var target = MakeTarget(providerId);
        var blob = Encoding.Unicode.GetBytes(secret);
        var blobPtr = Marshal.AllocCoTaskMem(blob.Length);

        try
        {
            Marshal.Copy(blob, 0, blobPtr, blob.Length);
            var credential = new Credential
            {
                Type = CredTypeGeneric,
                TargetName = target,
                CredentialBlobSize = (uint)blob.Length,
                CredentialBlob = blobPtr,
                Persist = CredPersistLocalMachine,
                UserName = Environment.UserName
            };

            if (!CredWrite(ref credential, 0))
            {
                throw new InvalidOperationException($"Windows Credential Manager rejected the key. Error {Marshal.GetLastWin32Error()}.");
            }
        }
        finally
        {
            Marshal.FreeCoTaskMem(blobPtr);
        }
    }

    public static string? ReadSecret(string providerId)
    {
        if (!CredRead(MakeTarget(providerId), CredTypeGeneric, 0, out var credentialPtr))
        {
            return null;
        }

        try
        {
            var credential = Marshal.PtrToStructure<Credential>(credentialPtr);
            if (credential.CredentialBlob == IntPtr.Zero || credential.CredentialBlobSize == 0)
            {
                return null;
            }

            var bytes = new byte[credential.CredentialBlobSize];
            Marshal.Copy(credential.CredentialBlob, bytes, 0, bytes.Length);
            return Encoding.Unicode.GetString(bytes);
        }
        finally
        {
            CredFree(credentialPtr);
        }
    }

    public static void DeleteSecret(string providerId)
    {
        CredDelete(MakeTarget(providerId), CredTypeGeneric, 0);
    }

    public static bool HasSecret(string providerId)
    {
        return ReadSecret(providerId) is not null;
    }

    private static string MakeTarget(string providerId)
    {
        return "CodexProfileTray/" + providerId;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite(ref Credential userCredential, uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, int type, int reservedFlag, out IntPtr credentialPtr);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string target, int type, int flags);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern void CredFree(IntPtr buffer);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Credential
    {
        public uint Flags;
        public uint Type;
        public string TargetName;
        public string Comment;
        public long LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string TargetAlias;
        public string UserName;
    }
}

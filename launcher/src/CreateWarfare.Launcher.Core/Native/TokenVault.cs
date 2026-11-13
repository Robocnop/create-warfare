using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace CreateWarfare.Launcher.Core.Native;

/// <summary>
/// Chiffrement des jetons avec DPAPI (CryptProtectData, portée utilisateur) : le texte stocké dans settings.json
/// n'est lisible que par ce compte Windows sur ce PC. Une entropie propre au launcher évite qu'un autre programme
/// qui appellerait DPAPI sans elle relise le jeton par hasard. Jamais de jeton en clair dans le journal.
/// </summary>
public static class TokenVault
{
    static readonly byte[] DefaultEntropy = Encoding.UTF8.GetBytes("CreateWarfareLauncher/msa/v1");

    /// <summary>Chiffre et renvoie du base64 (null si <paramref name="secret"/> est vide).</summary>
    public static string? Protect(string? secret, byte[]? entropy = null)
    {
        if (string.IsNullOrEmpty(secret)) return null;
        return Convert.ToBase64String(Crypt(Encoding.UTF8.GetBytes(secret), entropy ?? DefaultEntropy, protect: true));
    }

    /// <summary>Déchiffre ; null si la valeur est vide, corrompue ou chiffrée par un autre compte Windows.</summary>
    public static string? Unprotect(string? protectedB64, byte[]? entropy = null)
    {
        if (string.IsNullOrEmpty(protectedB64)) return null;
        try
        {
            return Encoding.UTF8.GetString(Crypt(Convert.FromBase64String(protectedB64), entropy ?? DefaultEntropy, protect: false));
        }
        catch (Exception e) when (e is FormatException or Win32Exception) { return null; }
    }

    static byte[] Crypt(byte[] data, byte[] entropy, bool protect)
    {
        var input = Blob.From(data);
        var ent = Blob.From(entropy);
        var output = default(Blob);
        try
        {
            const int CRYPTPROTECT_UI_FORBIDDEN = 0x1;
            var ok = protect
                ? CryptProtectData(ref input, null, ref ent, IntPtr.Zero, IntPtr.Zero, CRYPTPROTECT_UI_FORBIDDEN, out output)
                : CryptUnprotectData(ref input, IntPtr.Zero, ref ent, IntPtr.Zero, IntPtr.Zero, CRYPTPROTECT_UI_FORBIDDEN, out output);
            if (!ok) throw new Win32Exception(Marshal.GetLastWin32Error());
            var result = new byte[output.Size];
            Marshal.Copy(output.Data, result, 0, output.Size);
            return result;
        }
        finally
        {
            input.Free();
            ent.Free();
            if (output.Data != IntPtr.Zero) LocalFree(output.Data);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct Blob
    {
        public int Size;
        public IntPtr Data;

        public static Blob From(byte[] bytes)
        {
            var b = new Blob { Size = bytes.Length, Data = Marshal.AllocHGlobal(Math.Max(1, bytes.Length)) };
            Marshal.Copy(bytes, 0, b.Data, bytes.Length);
            return b;
        }

        public void Free()
        {
            if (Data == IntPtr.Zero) return;
            // efface la copie en mémoire non managée avant de la rendre
            for (var i = 0; i < Size; i++) Marshal.WriteByte(Data, i, 0);
            Marshal.FreeHGlobal(Data);
            Data = IntPtr.Zero;
        }
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool CryptProtectData(ref Blob pDataIn, string? szDataDescr, ref Blob pOptionalEntropy, IntPtr pvReserved,
        IntPtr pPromptStruct, int dwFlags, out Blob pDataOut);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool CryptUnprotectData(ref Blob pDataIn, IntPtr ppszDataDescr, ref Blob pOptionalEntropy, IntPtr pvReserved,
        IntPtr pPromptStruct, int dwFlags, out Blob pDataOut);

    [DllImport("kernel32.dll")]
    static extern IntPtr LocalFree(IntPtr hMem);
}

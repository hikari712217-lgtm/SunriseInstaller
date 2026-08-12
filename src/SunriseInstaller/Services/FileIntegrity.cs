using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Sunrise.Installer.Services;

public static class FileIntegrity
{
    private const ushort Amd64Machine = 0x8664;
    private const ushort DllCharacteristic = 0x2000;

    public static async Task<string> Sha256Async(string path, CancellationToken cancellationToken)
    {
        await using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1024 * 128,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static void ValidateAmd64Dll(string path)
    {
        using FileStream stream = File.OpenRead(path);
        Span<byte> dosHeader = stackalloc byte[64];
        ReadExactly(stream, dosHeader);
        if (dosHeader[0] != (byte)'M' || dosHeader[1] != (byte)'Z')
        {
            throw new InstallerException("The Sunrise release does not contain a valid Windows DLL.");
        }

        int peOffset = BinaryPrimitives.ReadInt32LittleEndian(dosHeader[0x3C..0x40]);
        if (peOffset < 64 || peOffset > stream.Length - 24)
        {
            throw new InstallerException("The Sunrise DLL has an invalid PE header.");
        }

        stream.Position = peOffset;
        Span<byte> peHeader = stackalloc byte[24];
        ReadExactly(stream, peHeader);
        if (!peHeader[..4].SequenceEqual("PE\0\0"u8))
        {
            throw new InstallerException("The Sunrise DLL has an invalid PE signature.");
        }

        ushort machine = BinaryPrimitives.ReadUInt16LittleEndian(peHeader[4..6]);
        ushort characteristics = BinaryPrimitives.ReadUInt16LittleEndian(peHeader[22..24]);
        if (machine != Amd64Machine || (characteristics & DllCharacteristic) == 0)
        {
            throw new InstallerException("The Sunrise release must contain a 64-bit Windows DLL.");
        }
    }

    private static void ReadExactly(Stream stream, Span<byte> buffer)
    {
        int read = 0;
        while (read < buffer.Length)
        {
            int count = stream.Read(buffer[read..]);
            if (count == 0)
            {
                throw new InstallerException("The Sunrise DLL is truncated.");
            }

            read += count;
        }
    }
}

using System.Security.Cryptography;
using System.Text;

namespace Nirengi.Modules.Curriculum.Domain;

/// <summary>Name-based UUID v5 (RFC 9562): the same namespace and name always give the same id.</summary>
public static class Uuid5
{
    public static Guid Create(Guid ns, string name)
    {
        Span<byte> input = stackalloc byte[16 + Encoding.UTF8.GetByteCount(name)];
        ns.TryWriteBytes(input[..16], bigEndian: true, out _);
        Encoding.UTF8.GetBytes(name, input[16..]);

        Span<byte> hash = stackalloc byte[SHA1.HashSizeInBytes];
#pragma warning disable CA5350 // SHA-1 is what RFC 9562 prescribes for v5 identifiers, not a security use.
        SHA1.HashData(input, hash);
#pragma warning restore CA5350

        hash[6] = (byte)((hash[6] & 0x0F) | 0x50);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        return new Guid(hash[..16], bigEndian: true);
    }
}

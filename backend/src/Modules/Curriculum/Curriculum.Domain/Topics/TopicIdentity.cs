using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Nirengi.Modules.Curriculum.Domain.Topics;

/// <summary>
/// Topic ids are UUID v5 (RFC 9562) of the topic code, so a re-seed keeps every id and the mobile app can
/// derive the same id offline. Never change the namespace: every stored topic reference depends on it.
/// </summary>
public static class TopicIdentity
{
    public static readonly Guid Namespace = new("6f1c2a4e-9b7d-4e3a-8c51-2d0f7a9e4b13");

    public static Guid For(string topicCode)
    {
        Span<byte> name = stackalloc byte[16 + Encoding.UTF8.GetByteCount(topicCode)];
        Namespace.TryWriteBytes(name[..16], bigEndian: true, out _);
        Encoding.UTF8.GetBytes(topicCode, name[16..]);

        Span<byte> hash = stackalloc byte[SHA1.HashSizeInBytes];
#pragma warning disable CA5350 // SHA-1 is what RFC 9562 prescribes for v5 identifiers, not a security use.
        SHA1.HashData(name, hash);
#pragma warning restore CA5350

        hash[6] = (byte)((hash[6] & 0x0F) | 0x50);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        return new Guid(hash[..16], bigEndian: true);
    }
}

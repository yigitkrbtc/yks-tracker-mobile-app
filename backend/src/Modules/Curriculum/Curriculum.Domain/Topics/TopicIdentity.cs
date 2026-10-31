namespace Nirengi.Modules.Curriculum.Domain.Topics;

/// <summary>
/// Topic ids are UUID v5 (RFC 9562) of the topic code, so a re-seed keeps every id and the mobile app can
/// derive the same id offline. Never change the namespace: every stored topic reference depends on it.
/// </summary>
public static class TopicIdentity
{
    public static readonly Guid Namespace = new("6f1c2a4e-9b7d-4e3a-8c51-2d0f7a9e4b13");

    public static Guid For(string topicCode) => Uuid5.Create(Namespace, topicCode);
}

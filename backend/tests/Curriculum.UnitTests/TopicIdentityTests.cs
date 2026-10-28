using Nirengi.Modules.Curriculum.Domain.Topics;
using Shouldly;

namespace Nirengi.Curriculum.UnitTests;

public sealed class TopicIdentityTests
{
    // Reference values from Python's uuid.uuid5 with the same namespace; the mobile app must produce the same.
    [Theory]
    [InlineData("AYT.AMAT.26", "b3807997-97be-533c-a033-70a07ad6142c")]
    [InlineData("TYT.TUR.01", "180d2e84-a20b-5739-9f60-1502f64fa38a")]
    public void Id_is_the_rfc_9562_v5_uuid_of_the_code(string code, string expected) =>
        TopicIdentity.For(code).ShouldBe(Guid.Parse(expected));

    [Fact]
    public void Different_codes_get_different_ids() =>
        TopicIdentity.For("TYT.TUR.01").ShouldNotBe(TopicIdentity.For("TYT.TUR.02"));
}

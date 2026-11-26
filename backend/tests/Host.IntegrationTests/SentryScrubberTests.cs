using System.Text;
using System.Text.Json;
using Nirengi.Host.Telemetry;
using Sentry;
using Sentry.Protocol;
using Shouldly;

namespace Nirengi.Host.IntegrationTests;

/// <summary>
/// ADR-0022 §5 layer 3: an event filled with personal data goes through the filter; none of it is left in the
/// serialized JSON, and the allow-listed fields stay. No database needed.
/// </summary>
public sealed class SentryScrubberTests
{
    private const string Email = "ali.veli@example.com";
    private const string Ipv4 = "203.0.113.42";
    private const string Ipv6 = "2001:db8::8a2e:370:7334";
    private const string Jwt = "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjMifQ.c2lnbmF0dXJlLXZhbHVl";
    private const string Digits = "12345678";
    private const string HostName = "vps-istanbul-01";

    private static readonly string[] Secrets =
    [
        Email, Ipv4, Ipv6, Jwt, Digits, HostName,
        "Bearer secret-token", "gizli-not-govdesi", "q=arama", "user-42", "cerez=1", "ekstra-deger", "ozel-baglam",
    ];

    [Fact]
    public void No_personal_data_is_left_in_the_serialized_event()
    {
        var json = Serialize(SentryScrubber.ScrubEvent(Polluted())!);

        foreach (var secret in Secrets)
        {
            json.ShouldNotContain(secret, customMessage: secret);
        }
    }

    [Fact]
    public void Allow_listed_fields_stay_and_messages_are_masked()
    {
        var original = Polluted();
        var id = original.EventId;

        var e = SentryScrubber.ScrubEvent(original)!;

        e.EventId.ShouldBe(id);
        e.Release.ShouldBe("nirengi-api@1.2.3");
        e.Environment.ShouldBe("production");
        e.Logger.ShouldBe("Nirengi.Modules.Tracking");
        e.Level.ShouldBe(SentryLevel.Error);
        e.ServerName.ShouldBe("api");
        e.Tags.ShouldBe(new Dictionary<string, string> { ["route"] = "/v1/topic-tests/{id}", ["method"] = "PUT", ["status"] = "500" });
        e.Message!.Formatted.ShouldBe("Kullanıcı [redacted] [redacted] için hata");
        e.Message.Message.ShouldBe("Kullanıcı {Email} {Ip} için hata");
        SentryScrubber.ScrubEvent(new SentryEvent { Message = new SentryMessage { Message = $"interpolated {Email}" } })!
            .Message!.Message.ShouldBe("interpolated [redacted]");
        e.Extra.ShouldBeEmpty();
        e.Modules.ShouldBeEmpty();

        var ex = e.SentryExceptions!.ShouldHaveSingleItem();
        ex.Type.ShouldBe("System.InvalidOperationException");
        ex.Value.ShouldBe("Bozuk [redacted] kodu [redacted] [redacted]");
        var frame = ex.Stacktrace!.Frames.ShouldHaveSingleItem();
        (frame.Module, frame.Function, frame.FileName, frame.LineNumber).ShouldBe(("Nirengi.Tracking", "Save", "SaveTopicTest.cs", 42));
        frame.Vars.ShouldBeEmpty();
        ex.Mechanism!.Data.ShouldBeEmpty();

        e.Contexts.Runtime.Name.ShouldBe(".NET");
        e.Contexts.OperatingSystem.Name.ShouldBe("Linux");
        e.Contexts.App.Version.ShouldBe("1.2.3");
        e.Contexts.Device.Name.ShouldBeNull();
        e.Contexts.ContainsKey("custom").ShouldBeFalse();

        var crumb = e.Breadcrumbs.ShouldHaveSingleItem();
        crumb.Message.ShouldBe("gönderim başarısız [redacted]");
        crumb.Data.ShouldBeNull();
    }

    [Theory]
    [InlineData(BreadcrumbLevel.Debug)]
    [InlineData(BreadcrumbLevel.Info)]
    public void Breadcrumbs_below_warning_are_dropped(BreadcrumbLevel level) =>
        SentryScrubber.ScrubBreadcrumb(new Breadcrumb("x", "default", level: level)).ShouldBeNull();

    [Fact]
    public void Warning_breadcrumbs_are_masked_and_lose_their_data()
    {
        var crumb = SentryScrubber.ScrubBreadcrumb(new Breadcrumb(
            $"giriş {Email}", "default", new Dictionary<string, string> { ["email"] = Email }, "Nirengi.Identity", BreadcrumbLevel.Warning))!;

        crumb.Message.ShouldBe("giriş [redacted]");
        crumb.Category.ShouldBe("Nirengi.Identity");
        crumb.Level.ShouldBe(BreadcrumbLevel.Warning);
        crumb.Data.ShouldBeNull();
    }

    [Theory]
    [InlineData("a çağrı@example.com b", "a [redacted] b")]
    [InlineData("a ayşe.yılmaz@gmail.com b", "a [redacted] b")]
    [InlineData("a user%40example.com b", "a [redacted] b")]
    [InlineData("a fe80::1 b", "a [redacted] b")]
    [InlineData("12:30:45 SaveTopicTest.cs:42:7", "12:30:45 SaveTopicTest.cs:42:7")]
    [InlineData("soru 1234567 ve 3.14", "soru 1234567 ve 3.14")]
    [InlineData("HTTP PUT /v1/topic-tests/0198c2a4-7d1e-7b3a-9f00-123456789abc responded 500", "HTTP PUT /v1/topic-tests/[redacted] responded 500")]
    [InlineData("Could not load Npgsql, Version=10.0.0.0, Culture=neutral", "Could not load Npgsql, Version=10.0.0.0, Culture=neutral")]
    public void Mask_hides_addresses_and_keeps_ordinary_text(string input, string expected) =>
        SentryScrubber.Mask(input).ShouldBe(expected);

    private static SentryEvent Polluted()
    {
        var e = new SentryEvent(new InvalidOperationException("x"))
        {
            Release = "nirengi-api@1.2.3",
            Environment = "production",
            Logger = "Nirengi.Modules.Tracking",
            Level = SentryLevel.Error,
            ServerName = HostName,
            Message = new SentryMessage
            {
                Message = "Kullanıcı {Email} {Ip} için hata",
                Formatted = $"Kullanıcı {Email} {Ipv4} için hata",
                Params = [Email, Ipv4],
            },
            SentryExceptions =
            [
                new SentryException
                {
                    Type = "System.InvalidOperationException",
                    Value = $"Bozuk {Jwt} kodu {Digits} {Ipv6}",
                    Stacktrace = new SentryStackTrace
                    {
                        Frames =
                        [
                            Frame(),
                        ],
                    },
                    Mechanism = new Mechanism { Type = "generic", Handled = false },
                },
            ],
            User = new SentryUser { Id = "user-42", Email = Email, IpAddress = Ipv4 },
            Request = new SentryRequest
            {
                Url = "https://api.example.com/v1/x?q=arama",
                Method = "PUT",
                QueryString = "q=arama",
                Cookies = "cerez=1",
                Data = new { note = "gizli-not-govdesi" },
            },
        };
        e.SentryExceptions.Single().Mechanism!.Data["email"] = Email;
        e.Request.Headers["Authorization"] = "Bearer secret-token";
        e.SetTag("route", "/v1/topic-tests/{id}");
        e.SetTag("method", "PUT");
        e.SetTag("status", "500");
        e.SetTag("email", Email);
        e.SetExtra("not", "ekstra-deger");
        e.Modules["Secret.Module"] = HostName;
        e.Contexts.Device.Name = HostName;
        e.Contexts.OperatingSystem.Name = "Linux";
        e.Contexts.OperatingSystem.RawDescription = $"Linux {HostName}";
        e.Contexts.Runtime.Name = ".NET";
        e.Contexts.App.Version = "1.2.3";
        e.Contexts.App.Identifier = HostName;
        e.Contexts["custom"] = new { value = "ozel-baglam" };
        e.AddBreadcrumb(new Breadcrumb($"gönderim başarısız {Email}", "default", new Dictionary<string, string> { ["ip"] = Ipv4 }, "Nirengi.Sync", BreadcrumbLevel.Warning));
        return e;
    }

    private static SentryStackFrame Frame()
    {
        var frame = new SentryStackFrame
        {
            Module = "Nirengi.Tracking",
            Function = "Save",
            FileName = "SaveTopicTest.cs",
            LineNumber = 42,
            ContextLine = $"var token = \"{Jwt}\";",
        };
        frame.Vars["password"] = $"hunter2-{Email}";
        frame.PreContext.Add(Email);
        return frame;
    }

    private static string Serialize(SentryEvent e)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            e.WriteTo(writer, null);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}

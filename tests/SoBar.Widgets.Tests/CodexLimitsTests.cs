using System.IO;
using System.Text.Json;
using SoBar.Widgets;
using Xunit;

namespace SoBar.Widgets.Tests;

public class CodexLimitsTests
{
    [Fact]
    public async Task HandshakeQueriesOnlyCodexLimitsAndChoosesItsBucket()
    {
        string[] messages = [
            """{"id":1,"result":{}}""",
            """{"method":"account/updated","params":{}}""",
            """{"id":2,"result":{"rateLimits":{"limitId":"other"},"rateLimitsByLimitId":{"codex":{"primary":{"usedPercent":41,"windowDurationMins":300,"resetsAt":1800000000},"secondary":{"usedPercent":64,"windowDurationMins":10080}}}}}"""
        ];
        using var incoming = new StringReader(string.Join("\n",messages));
        using var outgoing = new StringWriter();
        var reading = await CodexLimits.ExchangeAsync(outgoing,incoming,default);
        var methods = outgoing.ToString().Split(Environment.NewLine,StringSplitOptions.RemoveEmptyEntries)
            .Select(line => {
                using var packet = JsonDocument.Parse(line);
                return packet.RootElement.GetProperty("method").GetString()!;
            }).ToArray();
        Assert.Equal(["initialize","initialized","account/rateLimits/read"],methods);
        Assert.Equal(new CardLines("41%","64%","5 h","Semana",41,64),reading.Card);
        Assert.Contains("Renovação",reading.Detail);
    }
    [Fact]
    public void OlderServerReportsOneNonstandardWindow()
    {
        using var body = JsonDocument.Parse("""{"rateLimits":{"primary":{"usedPercent":0,"windowDurationMins":15},"secondary":null}}""");
        var reading = CodexLimits.Parse(body.RootElement);
        Assert.Equal(new CardLines("0%","—","15 min","Semana",0,null),reading.Card);
    }
    [Theory]
    [InlineData("""{"rateLimits":{"limitId":"other","primary":{"usedPercent":24}}}""")]
    [InlineData("""{"rateLimits":{"primary":{"usedPercent":120},"secondary":null}}""")]
    [InlineData("""{"rateLimits":null}""")]
    public void UnavailableOrUnrelatedQuotaDoesNotBecomeUsage(string json)
    {
        using var body = JsonDocument.Parse(json);
        Assert.Throws<InvalidDataException>(() => CodexLimits.Parse(body.RootElement));
    }
}

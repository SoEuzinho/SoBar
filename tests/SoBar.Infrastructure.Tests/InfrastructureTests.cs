using System.Net;
using System.Security.Cryptography;
using System.Text;
using SoBar.Core;
using SoBar.Infrastructure;
using Xunit;

namespace SoBar.Infrastructure.Tests;

public sealed class InfrastructureTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("SoBarTest-").FullName;
    public void Dispose() => Directory.Delete(_dir,true);
    [Fact]
    public void SettingsSurviveRestartAndProfilesKeepLayout()
    {
        using (var app = new AppServices(_dir))
        {
            var s = app.Settings;
            Assert.False(s.ReserveSpace);
            Assert.False(s.StartWithWindows);
            s.Theme = "Midnight"; s.Size = "Grande"; s.WidgetScales["clock"] = 1.5;
            app.Save(s);
            s.WidgetGroups["cpu"] = s.WidgetGroups["ram"] = "Sistema";
            app.Save(s); app.SaveProfile("Trabalho");
            s.Theme = "Claro"; app.Save(s);
        }
        using var restored = new AppServices(_dir);
        Assert.Equal("Claro",restored.Settings.Theme);
        restored.LoadProfile("Trabalho");
        Assert.Equal("Midnight",restored.Settings.Theme);
        Assert.Equal(1.5,restored.Settings.WidgetScales["clock"]);
        Assert.Equal("Sistema", restored.Settings.WidgetGroups["ram"]);
        Assert.Equal(["clock","cpu","ram"], restored.Settings.Widgets);
        var file = Path.Combine(_dir,"layout.json");
        restored.Export(file); restored.Save(new AppSettings()); restored.Import(file);
        Assert.Equal("Sistema", restored.Settings.WidgetGroups["cpu"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a\nb")]
    [InlineData("012345678901234567890123456789012")]
    public void WidgetGroupNamesAreBoundedAndSingleLine(string value)
    {
        var settings = new AppSettings { WidgetGroups = { ["clock"] = value } };
        Assert.Throws<InvalidDataException>(settings.Validate);
    }

    [Fact]
    public void ExportOmitsSecretsAndImportKeepsStartupLocal()
    {
        using var app = new AppServices(_dir);
        var key = "unit-test-credential-" + Guid.NewGuid();
        app.SetApiKey(key);
        var file = Path.Combine(_dir,"export.json");
        app.Export(file);
        Assert.DoesNotContain(key,File.ReadAllText(file));
        using (var db = File.Open(Path.Combine(_dir,"settings.db"),FileMode.Open,FileAccess.Read,FileShare.ReadWrite))
        using (var reader = new StreamReader(db,Encoding.UTF8))
            Assert.DoesNotContain(key,reader.ReadToEnd());
        File.WriteAllText(file,"{\"Theme\":\"Claro\",\"StartWithWindows\":true}");
        app.Import(file);
        Assert.False(app.Settings.StartWithWindows);
        Assert.True(app.HasApiKey);
        File.WriteAllText(file,"{\"Opacity\":0}");
        Assert.Throws<InvalidDataException>(() => app.Import(file));
        Assert.Equal("Claro",app.Settings.Theme);
        app.SetApiKey("");
        Assert.False(app.HasApiKey);
    }

    [Fact]
    public void CorruptDatabaseIsKeptAndRecovered()
    {
        File.WriteAllText(Path.Combine(_dir,"settings.db"),new string('x',4000));
        using var recovered = new AppServices(_dir);
        Assert.Single(Directory.GetFiles(_dir,"settings.db.corrupt-*"));
        Assert.False(recovered.Settings.ReserveSpace);
    }
    [Fact]
    public void HistoryIsOptInBoundedAndErasedOnDisable()
    {
        using var app = new AppServices(_dir);
        app.Remember("privado");
        Assert.Empty(app.History());
        var s = app.Settings; s.HistoryEnabled = true; app.Save(s);
        for (int i = 0; i < 50; i++) app.Remember(i.ToString());
        Assert.Equal(40,app.History().Length);
        Assert.Equal("49",app.History()[0]);
        s.HistoryEnabled = false; app.Save(s);
        Assert.Empty(app.History());
    }
    [Fact]
    public void PluginApprovalPinsReviewedContent()
    {
        using var app = new AppServices(_dir);
        var path = Path.Combine(_dir,"plugin.json");
        File.WriteAllText(path,"""{"apiVersion":1,"id":"utc","name":"UTC","source":"utc","permissions":["clock.read"]}""");
        Assert.Throws<InvalidOperationException>(() => app.ImportPlugin(path));
        Assert.Contains("clock.read",app.PreviewPlugin(path));
        File.WriteAllText(path,"{}");
        app.ImportPlugin(path);
        Assert.Contains("UTC",Assert.Single(app.PluginTiles()).Value);
        app.RemovePlugin("utc"); Assert.Empty(app.PluginTiles());
        Assert.Throws<InvalidDataException>(() => app.PreviewPlugin(path));
    }
    [Theory]
    [InlineData("clock.read","shell")]
    [InlineData("filesystem.read","utc")]
    public void UnknownPluginOperationsAreRejected(string permission,string source)
    {
        var manifest = new SoBar.Plugins.ExtensionManifest(1,"hello","Oi",source,[permission]);
        Assert.Throws<InvalidDataException>(manifest.Validate);
    }
    [Fact]
    public void UpdateRequiresNewVersionAndMatchingSignedPayload()
    {
        using var key = RSA.Create(2048);
        var msi = "test package"u8.ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(msi));
        var message = Encoding.UTF8.GetBytes($"SoBar-MSI-v1\n1.2.0\n{hash}\n");
        var sig = Convert.ToBase64String(key.SignData(message,HashAlgorithmName.SHA256,RSASignaturePadding.Pss));
        var release = new ReleaseSignature("1.2.0",hash,sig);
        UpdateInstaller.Verify(new MemoryStream(msi),release,key,new Version(1,1));
        Assert.Throws<InvalidDataException>(() => UpdateInstaller.Verify(new MemoryStream(msi),release,key,new Version(1,2,0,0)));
        Assert.Throws<CryptographicException>(() => UpdateInstaller.Verify(new MemoryStream([4,1]),release,key,new Version(1,0)));
        using var different = RSA.Create(2048);
        Assert.Throws<CryptographicException>(() => UpdateInstaller.Verify(new MemoryStream(msi),release,different,new Version(1,0)));
    }

    private sealed class FakeResponse(string action) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            Assert.Equal("api.openai.com",request.RequestUri?.Host);
            Assert.Equal("Bearer",request.Headers.Authorization?.Scheme);
            var data = new { output = new object[] {
                new { type="message",content = new[] { new { type="output_text",text="Sugestão" } } },
                new { type="function_call",name="propor_acao",arguments=System.Text.Json.JsonSerializer.Serialize(new { action }) } } };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(data))});
        }
    }
    [Theory]
    [InlineData("exit","exit")]
    [InlineData("settings","settings")]
    [InlineData("unrecognized",null)]
    public async Task AssistantCanOnlyProposeAllowedActions(string candidate,string? expected)
    {
        using var assistant = new Assistant(new FakeResponse(candidate));
        var reply = await assistant.AskAsync("test-token","Pergunta",default);
        Assert.Equal("Sugestão",reply.Text);
        Assert.Equal(expected,reply.SuggestedAction);
        for (int i=1;i<5;i++) await assistant.AskAsync("test-token","Pergunta",default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => assistant.AskAsync("test-token","Pergunta",default));
    }
}

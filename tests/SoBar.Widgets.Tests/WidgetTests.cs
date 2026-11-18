using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Xunit;

namespace SoBar.Widgets.Tests;

public sealed class WidgetTests
{
    [Fact]
    public void CpuUsesBusyDeltaAndRejectsInvalidIntervals()
    {
        Assert.Equal(75, SystemReadings.CpuPercent(10, 100, 20, 140));
        Assert.Null(SystemReadings.CpuPercent(20, 140, 21, 140));
        Assert.Null(SystemReadings.CpuPercent(20, 140, 15, 200));
        Assert.Null(SystemReadings.CpuPercent(20, 140, 100, 141));
    }

    [Fact]
    public void MemoryAndSystemVolumeReportNativeValues()
    {
        Assert.EndsWith("%", SystemReadings.Memory().Short);
        Assert.EndsWith("GiB", SystemReadings.Disk().Short);
    }

    [Theory]
    [InlineData("-23,5", "-46.6", true)]
    [InlineData("90", "-180", true)]
    [InlineData("-90.1", "0", false)]
    [InlineData("nan", "1", false)]
    [InlineData("0", "181", false)]
    public void CoordinatesAcceptDecimalCommaAndCheckRange(string latitude, string longitude, bool valid) =>
        Assert.Equal(valid, Weather.TryCoordinates(latitude, longitude, out _, out _));

    [Fact]
    public async Task WeatherReadsTemperatureIncludingZero()
    {
        using var client = new HttpClient(new Handler(request =>
        {
            Assert.Equal("api.open-meteo.com", request.RequestUri!.Host);
            Assert.Null(request.Headers.Authorization);
            return Json("""{"current":{"time":"2026-01-02T10:00","temperature_2m":0,"weather_code":71}}""");
        }));
        var result = await Weather.ReadAsync(client, 0, 0, CancellationToken.None);
        Assert.Equal("0°", result.Short);
        Assert.Contains("Neve", result.Detail);
    }

    [Fact]
    public void FocusTimerPausesAndTransitionsFromFocusToBreak()
    {
        var time = new TestTime();
        var timer = new FocusTimer(time);
        timer.Toggle();
        time.Advance(10);
        timer.Toggle();
        time.Advance(20);
        Assert.Equal(TimeSpan.FromMinutes(15), timer.Remaining);
        timer.Toggle();
        time.Advance(16);
        Assert.Equal(TimeSpan.Zero, timer.Remaining);
        Assert.True(timer.Finished);
        timer.Toggle();
        Assert.True(timer.IsBreak);
        Assert.Equal(TimeSpan.FromMinutes(5), timer.Remaining);
    }

    [Fact]
    public async Task UsageProtectsDedicatedCredentialAndReadsOfficialCounts()
    {
        var directory = Directory.CreateTempSubdirectory("sobar-usage-");
        try
        {
            const string key = "test-only-usage-token";
            var usage = new OrganizationUsage(directory.FullName);
            Assert.False(usage.Connected);
            usage.Connect(key);
            var storedBytes = File.ReadAllBytes(Assert.Single(directory.GetFiles()).FullName);
            Assert.DoesNotContain(key, Encoding.UTF8.GetString(storedBytes));
            using var client = new HttpClient(new Handler(request =>
            {
                Assert.Equal("api.openai.com", request.RequestUri!.Host);
                Assert.Equal("/v1/organization/usage/completions", request.RequestUri.AbsolutePath);
                Assert.Contains("start_time=1735689600", request.RequestUri.Query);
                Assert.Contains("end_time=1735693200", request.RequestUri.Query);
                Assert.Equal(key, request.Headers.Authorization!.Parameter);
                return Json("""{"has_more":false,"data":[{"results":[{"input_tokens":50,"output_tokens":25,"num_model_requests":2}]}]}""");
            }));
            var restored = new OrganizationUsage(directory.FullName);
            Assert.True(restored.Connected);
            var result = await restored.ReadAsync(client, DateTimeOffset.Parse("2025-01-01T01:00:00Z"), CancellationToken.None);
            Assert.Equal("75 tok", result.Short);
            restored.Disconnect();
            Assert.Empty(directory.GetFiles());
        }
        finally { directory.Delete(true); }
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task UsageDenialsDoNotShowZeroConsumption(HttpStatusCode response)
    {
        var directory = Directory.CreateTempSubdirectory("sobar-usage-");
        try
        {
            var usage = new OrganizationUsage(directory.FullName);
            usage.Connect("test-only-key");
            using var client = new HttpClient(new Handler(_ => new(response)));
            var result = await usage.ReadAsync(client, DateTimeOffset.UnixEpoch.AddHours(5), CancellationToken.None);
            Assert.Equal("—", result.Short);
            Assert.DoesNotContain("test-only-key", result.Detail);
        }
        finally { directory.Delete(true); }
    }

    [Fact]
    public void CreatesAllTilesAndKeepsMultipleClockViewsIndependent()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var service = new WidgetService(Path.GetTempPath());
                Assert.Contains("system-card", WidgetService.Catalog.Keys);
                Assert.Contains("agenda", WidgetService.Catalog.Keys);
                foreach (var id in WidgetService.Catalog.Keys) Assert.IsType<Button>(service.CreateTile(id));
                var first = (Button)service.CreateTile("clock");
                var second = (Button)service.CreateTile("clock");
                first.RaiseEvent(new(FrameworkElement.LoadedEvent));
                second.RaiseEvent(new(FrameworkElement.LoadedEvent));
                Assert.Matches(@"^\d{2}:\d{2}$", ((TextBlock)first.Content).Text);
                Assert.Equal(((TextBlock)first.Content).Text, ((TextBlock)second.Content).Text);
                var clockCard = (Button)service.CreateTile("clock-card");
                clockCard.RaiseEvent(new(FrameworkElement.LoadedEvent));
                Assert.Equal(WidgetService.ReadClock(first), WidgetService.ReadClock(clockCard));
                Assert.DoesNotContain("°", (string)clockCard.ToolTip);
                clockCard.RaiseEvent(new(FrameworkElement.UnloadedEvent));
                var agenda = (Button)service.CreateTile("agenda");
                agenda.RaiseEvent(new(FrameworkElement.LoadedEvent));
                Assert.False(string.IsNullOrEmpty(System.Windows.Automation.AutomationProperties.GetItemStatus(agenda)));
                agenda.RaiseEvent(new(FrameworkElement.UnloadedEvent));
                first.RaiseEvent(new(FrameworkElement.UnloadedEvent));
                second.RaiseEvent(new(FrameworkElement.UnloadedEvent));
                Assert.Throws<ArgumentOutOfRangeException>(() => service.CreateTile("missing"));
                service.Dispose();
                Assert.Throws<ObjectDisposedException>(() => service.CreateTile("cpu"));
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)));
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private sealed class TestTime : TimeProvider
    {
        private long ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => ticks;
        internal void Advance(int minutes) => ticks += TimeSpan.FromMinutes(minutes).Ticks;
    }

    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}

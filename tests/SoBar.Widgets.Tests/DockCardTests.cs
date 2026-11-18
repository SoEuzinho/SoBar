using System.IO;
using System.Windows;
using System.Windows.Controls;
using Xunit;

namespace SoBar.Widgets.Tests;

public sealed class DockCardTests
{
    [Fact]
    public void AgendaReopensEditsRemovesAndPicksNextAppointment()
    {
        var directory = Directory.CreateTempSubdirectory("sobar-agenda-");
        try
        {
            var now = new DateTimeOffset(2026,3,8,11,0,0,TimeSpan.Zero);
            var agenda = new Agenda(directory.FullName);
            agenda.Save(null,"Past",now.AddHours(-1));
            agenda.Save(null,"Segundo",now.AddHours(2));
            agenda.Save(null,"Primeiro",now.AddHours(1));
            var first = new Agenda(directory.FullName).Next(now)!;
            Assert.Equal("Primeiro",first.Title);
            agenda.Save(first.Id,"Adiado",now.AddHours(3));
            Assert.Equal("Segundo",agenda.Next(now)!.Title);
            agenda.Remove(agenda.Next(now)!.Id);
            Assert.Equal("Adiado",new Agenda(directory.FullName).Next(now)!.Title);
            Assert.Throws<ArgumentException>(() => agenda.Save(null,"",now));
            Assert.Throws<ArgumentException>(() => Agenda.LocalStart(DateTime.Today,"25:65"));
        }
        finally {directory.Delete(true);}
    }

    [Theory]
    [InlineData(1,128,255,"Tomada")]
    [InlineData(1,8,63,"63%")]
    [InlineData(0,1,84,"84%")]
    [InlineData(255,255,255,"—")]
    public void PowerNeverInventsBatteryValues(byte mains,byte flag,byte pct,string expected) =>
        Assert.Equal(expected,SystemReadings.FormatPower(mains,flag,pct).Short);

    [Fact]
    public void ScreenTimeConsentPausePersistenceAndErase()
    {
        var directory = Directory.CreateTempSubdirectory("sobar-usage-ledger-");
        try
        {
            var usage = new ScreenTimeStore(directory.FullName);
            var now = DateTimeOffset.UtcNow;
            usage.Observe(now,"Editor"); usage.Observe(now.AddSeconds(2),"Editor");
            Assert.Equal(0,usage.Summary(now,1).Seconds);
            Assert.Empty(directory.GetFiles());
            usage.Consent(true);
            usage.Observe(now,"Editor"); usage.Observe(now.AddSeconds(3),"Editor");
            usage.Observe(now.AddSeconds(6),null);
            usage.Observe(now.AddSeconds(9),"Editor"); usage.Observe(now.AddSeconds(11),"Editor");
            usage.Consent(false);
            usage.Observe(now.AddSeconds(13),"Editor");
            var reopened = new ScreenTimeStore(directory.FullName);
            Assert.False(reopened.Consented);
            var report = reopened.Summary(now.AddSeconds(14),7);
            Assert.Equal(5,report.Seconds);
            Assert.Equal("Editor",Assert.Single(report.Apps).Name);
            reopened.Clear();
            Assert.Empty(directory.GetFiles());
        }
        finally { directory.Delete(true); }
    }

    [Fact]
    public void ScreenTimeSplitsHoursRejectsSleepAndExpiresOldDays()
    {
        var directory = Directory.CreateTempSubdirectory("sobar-hour-");
        try
        {
            var usage = new ScreenTimeStore(directory.FullName);
            usage.Consent(true);
            var hour = new DateTimeOffset(new DateTime(2026,3,8,11,0,0,DateTimeKind.Local));
            usage.Observe(hour.AddSeconds(-2),"App");
            usage.Observe(hour.AddSeconds(2),"App");
            usage.Observe(hour.AddHours(2),"App");
            usage.Observe(hour.AddHours(2).AddSeconds(2),"App");
            usage.Pause();
            var summary = usage.Summary(hour.AddHours(3),7);
            Assert.Equal(6,summary.Seconds);
            Assert.Equal(2, summary.Hours[hour.AddHours(-1).LocalDateTime.Hour]);
            Assert.Equal(2, summary.Hours[hour.LocalDateTime.Hour]);
            Assert.Equal(0, usage.Summary(hour.AddDays(8),7).Seconds);
            usage.Flush();
            Assert.Equal(0,new ScreenTimeStore(directory.FullName).Summary(hour.AddDays(8),7).Seconds);
        }
        finally { directory.Delete(true); }
    }
}

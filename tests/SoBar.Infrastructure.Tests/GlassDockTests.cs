using SoBar.Core;
using SoBar.Infrastructure;
using Xunit;

namespace SoBar.Infrastructure.Tests;

public class GlassDockTests
{
    [Fact]
    public void NewDockSeedsOnceAndLeavesRemovedPinsRemoved()
    {
        var directory = Directory.CreateTempSubdirectory("SoBarGlass-");
        try
        {
            using (var initial = new AppServices(directory.FullName, initialPins: () => [@"C:\Apps\Editor.exe"]))
            {
                var settings = initial.Settings;
                Assert.Equal("Liquid Glass", settings.Theme);
                Assert.Equal(["codex-limits","agenda","power-card","clock-card"], settings.Widgets);
                Assert.Single(settings.PinnedApps);
                settings.Theme = "Escuro";
                settings.PinnedApps.Clear();
                settings.Widgets = ["clock"];
                initial.Save(settings);
            }
            using var reopened = new AppServices(directory.FullName, initialPins: () => throw new InvalidOperationException());
            Assert.Equal("Escuro", reopened.Settings.Theme);
            Assert.Equal(["clock"], reopened.Settings.Widgets);
            Assert.Empty(reopened.Settings.PinnedApps);
        }
        finally { directory.Delete(true); }
    }

    [Fact]
    public void ApplyingPresetOnlyChangesPresentation()
    {
        var settings = new AppSettings { Theme = "Claro", Widgets = ["notes"], Edge = "Left",
            AllMonitors = true, HistoryEnabled = true, PinnedApps = [@"C:\Apps\Editor.exe"] };
        settings.WidgetScales["codex-limits"] = 1.5;
        settings.WidgetGroups["clock-card"] = "Pessoal";
        settings.ApplyGlassDock();
        settings.Validate();
        Assert.Equal("Bottom", settings.Edge);
        Assert.Equal(new AppSettings().Widgets, settings.Widgets);
        Assert.Empty(settings.WidgetScales);
        Assert.Empty(settings.WidgetGroups);
        Assert.Single(settings.PinnedApps);
        Assert.True(settings.AllMonitors);
        Assert.True(settings.HistoryEnabled);
    }
}

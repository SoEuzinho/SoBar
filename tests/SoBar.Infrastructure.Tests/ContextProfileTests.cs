using SoBar.Core;
using SoBar.Infrastructure;
using Xunit;

namespace SoBar.Infrastructure.Tests;

public sealed class ContextProfileTests
{
    private const string Editor = @"C:\Tools\editor.exe";

    [Fact]
    public void ContextualSelectionHandlesPriorityAndNeverAppliesGlobalPermissions()
    {
        var directory = Directory.CreateTempSubdirectory("sobar-context-");
        try
        {
            using var service = new AppServices(directory.FullName);
            void Profile(string name, string theme)
            {
                var s = service.Settings;
                s.Theme = theme;
                s.HistoryEnabled = true;
                s.StartWithWindows = true;
                s.PinnedApps = [Editor];
                s.Widgets = ["clock", "weather"];
                s.WidgetGroups["clock"] = s.WidgetGroups["weather"] = "Agenda";
                service.Save(s);
                service.SaveProfile(name);
            }
            Profile("Display", "Claro");
            Profile("Editor", "Midnight");
            Profile("Both", "iOS Glass");
            service.Save(new AppSettings());
            service.SetContextRule("Display1", "", "Display");
            service.SetContextRule("", Editor, "Editor");
            service.SetContextRule("Display1", Editor, "Both");
            var plan = service.ReadContextProfiles();
            var match = plan.Resolve("DISPLAY1", "Display1", Editor.ToUpperInvariant());
            Assert.Equal("iOS Glass", match.Theme);
            Assert.Equal(["clock", "weather"], match.Widgets);
            Assert.Equal("Agenda",match.WidgetGroups["clock"]);
            Assert.False(match.HistoryEnabled);
            Assert.False(match.StartWithWindows);
            Assert.Empty(match.PinnedApps);
            Assert.Equal("Claro", plan.Resolve("Display1", "Display2", Editor).Theme);
            Assert.Equal("Midnight", plan.Resolve("Display2", "Display2", Editor).Theme);
            Assert.Same(plan.Default, plan.Resolve("Display2", "Display1", Editor));
            Assert.Equal("Escuro", service.Settings.Theme);
            Assert.False(service.Settings.StartWithWindows);
        }
        finally { directory.Delete(true); }
    }

    [Fact]
    public void RulesPersistReplaceCaseInsensitiveConditionsAndCanBeRemoved()
    {
        var directory = Directory.CreateTempSubdirectory("sobar-context-");
        try
        {
            using (var original = new AppServices(directory.FullName))
            {
                original.SaveProfile("A");
                original.SaveProfile("B");
                original.SetContextRule("Display1", Editor, "A");
                original.SetContextRule("display1", Editor.ToUpperInvariant(), "B");
                Assert.Throws<ArgumentException>(() => original.SetContextRule("", "", "B"));
                Assert.Throws<InvalidDataException>(() => original.SetContextRule("display1", "", "Missing"));
                Assert.Throws<ArgumentException>(() => original.SetContextRule("", "editor --arg", "B"));
            }
            using (var reopened = new AppServices(directory.FullName))
            {
                Assert.Equal("B", Assert.Single(reopened.ContextRules()).Profile);
                reopened.RemoveContextRule("DISPLAY1", Editor);
            }
            using var final = new AppServices(directory.FullName);
            Assert.Empty(final.ContextRules());
        }
        finally { directory.Delete(true); }
    }
}

using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Microsoft.Win32;
using SoBar.Windows;

namespace SoBar.App;

public partial class SettingsWindow
{
    private void ContextRules()
    {
        SectionHeading("Perfis por monitor e aplicativo");
        Hint("Aplique a aparência e os widgets de um perfil. A regra de aplicativo afeta o monitor em que ele está em primeiro plano.");
        var service = _shell.Services;
        foreach (var rule in service.ContextRules())
        {
            var row = Row();
            var monitor = rule.Monitor.Length == 0 ? "Qualquer monitor" : rule.Monitor.Replace(@"\\.\", "");
            var app = rule.Executable.Length == 0 ? "Qualquer aplicativo" : Path.GetFileName(rule.Executable);
            row.Children.Add(new TextBlock { Text = $"{monitor} · {app} → {rule.Profile}", TextWrapping = TextWrapping.Wrap,
                ToolTip = rule.Executable, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 7, 0) });
            Add(row, "Remover regra", () => _shell.Execute(() =>
            {
                service.RemoveContextRule(rule.Monitor, rule.Executable);
                _shell.Reconfigure();
                ShowPage(_section);
            }));
        }
        var profiles = service.Profiles();
        if (profiles.Length == 0) { Hint("Salve um perfil acima para criar a primeira regra."); return; }

        var displays = new[] { new DisplayChoice("", "Qualquer monitor") }.Concat(
            DesktopBarHost.Monitors().Select(m => new DisplayChoice(m.Id, m.Name))).ToArray();
        var display = new ComboBox { ItemsSource = displays, DisplayMemberPath = nameof(DisplayChoice.Name),
            SelectedIndex = 0, MinWidth = 140, Margin = new Thickness(0, 0, 5, 3) };
        AutomationProperties.SetName(display, "Monitor da regra");
        var profile = new ComboBox { ItemsSource = profiles, SelectedIndex = 0, MinWidth = 100, Margin = new Thickness(0, 0, 5, 3) };
        AutomationProperties.SetName(profile, "Perfil visual da regra");
        var selectors = Row();
        selectors.Children.Add(display);
        selectors.Children.Add(profile);
        var application = Row();
        var appPath = Input(application, "", "Caminho completo do aplicativo", 180);
        Add(application, "Selecionar .exe…", () =>
        {
            var file = new OpenFileDialog { Filter = "Aplicativo (*.exe)|*.exe" };
            if (file.ShowDialog(this) == true) appPath.Text = file.FileName;
        });
        Add(Row(), "Salvar regra", () => _shell.Execute(() =>
        {
            service.SetContextRule(((DisplayChoice)display.SelectedItem).Id, appPath.Text, (string)profile.SelectedItem);
            _shell.Reconfigure();
            ShowPage(_section);
            Status.Text = "Regra contextual salva.";
        }));
    }

    private sealed record DisplayChoice(string Id, string Name);
}

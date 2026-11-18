using System.IO;
using System.Windows.Media;

namespace SoBar.App;

internal sealed partial class Shell
{
    // The smoke entry point creates a fresh, isolated data directory.
    private void VerifyContextProfile()
    {
        var bar = _bars.First();
        var before = Services.Settings;
        var visual = Services.Settings;
        visual.Accent = "#DB316B";
        Services.Save(visual);
        Services.SaveProfile("Contexto de teste");
        Services.Save(before);
        Services.SetContextRule(bar.MonitorId, "", "Contexto de teste");
        Reconfigure();
        var contextualAccent = (SolidColorBrush)bar.Resources["Accent"];
        if (contextualAccent.Color != Color.FromRgb(219, 49, 107) || Services.Settings.Accent != before.Accent)
            throw new InvalidDataException("Aplicação do perfil contextual falhou.");
        Services.RemoveContextRule(bar.MonitorId, "");
        Reconfigure();
        if (((SolidColorBrush)bar.Resources["Accent"]).Color != (Color)ColorConverter.ConvertFromString(before.Accent))
            throw new InvalidDataException("Aparência global não restaurada.");
    }
}

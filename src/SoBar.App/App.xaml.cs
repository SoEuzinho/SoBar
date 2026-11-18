using System.IO;
using System.Windows;
using System.Windows.Threading;
using SoBar.Infrastructure;
using SoBar.Widgets;
using SoBar.Windows;

namespace SoBar.App;

public partial class App : Application
{
    private AppServices? _services;
    private WidgetService? _widgets;
    private Shell? _shell;
    private Mutex? _instance;
    private bool _ownsMutex;
    private bool _smoke;
    private string? _temporaryDirectory;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Contains("--inspect-plugin")) { Shutdown(PluginBroker.RunWorker()); return; }
        var diagnostics = e.Args.Contains("--diagnostics-sample");
        _smoke = diagnostics || e.Args.Contains("--smoke-test", StringComparer.OrdinalIgnoreCase);
        if (!_smoke)
        {
            _instance = new Mutex(true, @"Local\SoBar.Desktop.1", out _ownsMutex);
            if (!_ownsMutex) { Shutdown(); return; }
        }
        try
        {
            if (_smoke) _temporaryDirectory = Path.Combine(Path.GetTempPath(), "SoBar-Smoke-" + Guid.NewGuid().ToString("N"));
            _services = new AppServices(_temporaryDirectory, isolatePlugins: true,
                initialPins: _smoke ? null : PinnedShortcuts.Discover);
            if (_temporaryDirectory != null && e.Args.Contains("--capture-settings"))
            {
                var item = new { Id = Guid.NewGuid(), Start = DateTimeOffset.Now.AddHours(1), Title = "Revisão de design" };
                File.WriteAllText(Path.Combine(_temporaryDirectory,"agenda.json"),System.Text.Json.JsonSerializer.Serialize(new[] { item }));
            }
            _widgets = new WidgetService(_services.DataDirectory);
            _shell = new Shell(_services, _widgets, new DesktopWindowService(), () => Shutdown());
            DispatcherUnhandledException += OnUnexpectedError;
            _shell.Start();
            if (_smoke) Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(async () => {
                try
                {
                    await Task.Delay(diagnostics ? 20_000 : 1250);
                    if (!diagnostics) await _shell.VerifySmokeAsync(e.Args.SkipWhile(a => a != "--capture-settings").Skip(1).FirstOrDefault());
                    Shutdown(0);
                }
                catch (Exception error) { Fail(error); }
            }));
        }
        catch (Exception error) { Fail(error); }
    }
    private void OnUnexpectedError(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        Fail(e.Exception);
    }
    private void Fail(Exception error)
    {
        _services?.Log(error.ToString());
        if (!_smoke) MessageBox.Show("O SoBar precisou fechar. " + error.Message, "SoBar", MessageBoxButton.OK, MessageBoxImage.Error);
        else Console.Error.WriteLine(error);
        Shutdown(1);
    }
    protected override void OnExit(ExitEventArgs e)
    {
        try { _shell?.Dispose(); }
        finally
        {
            _widgets?.Dispose();
            _services?.Dispose();
            if (_ownsMutex) _instance?.ReleaseMutex();
            _instance?.Dispose();
            if (_temporaryDirectory != null)
            {
                try { Directory.Delete(_temporaryDirectory, true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            base.OnExit(e);
        }
    }
}

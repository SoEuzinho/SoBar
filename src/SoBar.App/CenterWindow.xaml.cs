using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SoBar.Core;
using SoBar.Infrastructure;

namespace SoBar.App;

internal sealed record CenterResult(string Name, string Detail, string Glyph, string Action, SearchHit? Hit = null);
internal sealed class CenterModel : Observable, IDisposable
{
    private readonly AppServices _services;
    private CancellationTokenSource? _pending;
    private string _query = "", _answer = "", _status = "";
    private IReadOnlyList<CenterResult> _results = [];
    private CenterResult? _suggested;
    public CenterResult? Suggested { get => _suggested; private set => Set(ref _suggested,value); }
    public bool Assistant { get; private set; }
    public string Query { get => _query; set { if (Set(ref _query, value) && !Assistant) _ = Search(); } }
    public string Answer { get => _answer; private set => Set(ref _answer, value); }
    public string Status { get => _status; set => Set(ref _status, value); }
    public IReadOnlyList<CenterResult> Results { get => _results; private set => Set(ref _results, value); }
    private static readonly CenterResult[] Commands = [
        new("Abrir configurações", "Personalização da barra", "\uE713", "settings"),
        new("Encerrar o SoBar", "Fechar todas as barras nesta sessão", "\uE8BB", "exit")
    ];
    public CenterModel(AppServices services) { _services = services; }
    private CancellationTokenSource Begin()
    {
        _pending?.Cancel();
        return _pending = new();
    }
    public void Mode(bool assistant)
    {
        _pending?.Cancel(); Assistant = assistant; Suggested = null;
        Query = "";
        if (assistant) Status = _services.HasApiKey ? "Sua pergunta será enviada à OpenAI ao clicar em Enviar." : "Adicione uma chave da OpenAI nas configurações para enviar uma pergunta.";
        else _ = Search();
    }
    public async Task Search()
    {
        var source = Begin();
        var text = Query.Trim();
        try
        {
            var items = Commands.Where(c => c.Name.Contains(text, StringComparison.CurrentCultureIgnoreCase)).ToList();
            if (text.Length == 0)
            {
                items.InsertRange(0, _services.History().Select(h => new CenterResult(h, "Busca recente", "\uE81C", "history")));
                Results = items; Status = "Pesquise aplicativos e arquivos ou selecione um comando."; return;
            }
            Status = "Pesquisando…";
            await Task.Delay(180, source.Token);
            var hits = await _services.SearchAsync(text, source.Token);
            source.Token.ThrowIfCancellationRequested();
            items.AddRange(hits.Select(h => new CenterResult(h.Name, h.Path, "\uE8A5", "open", h)));
            Results = items;
            Status = items.Count == 0 ? "Nenhum resultado." : $"{items.Count} resultado(s) · Enter para abrir";
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { if (!source.IsCancellationRequested) { Status = error.Message; Results = []; } }
        finally { if (_pending == source) _pending = null; source.Dispose(); }
    }
    public async Task Ask()
    {
        if (string.IsNullOrWhiteSpace(Query) || !Assistant) return;
        var source = Begin();
        try
        {
            Status = "Consultando a OpenAI…";
            Suggested = null; Answer = "";
            var answer = await _services.AskWithActionsAsync(Query.Trim(), source.Token);
            source.Token.ThrowIfCancellationRequested(); Answer = answer.Text; Suggested = Commands.FirstOrDefault(c => c.Action == answer.SuggestedAction); Status = "Resposta recebida.";
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { if (!source.IsCancellationRequested) Status = error.Message; }
        finally { if (_pending == source) _pending = null; source.Dispose(); }
    }
    public void Dispose() => _pending?.Cancel();
}

public partial class CenterWindow : Window
{
    private readonly CenterModel _model;
    private readonly Shell _shell;
    internal CenterWindow(Shell shell)
    {
        _shell = shell;
        _model = new(shell.Services);
        InitializeComponent(); DataContext = _model;
        _model.PropertyChanged += (_,e) => { if (e.PropertyName == nameof(CenterModel.Suggested)) UpdateSuggestion(); };
        SelectMode(false);
        Closed += (_, _) => _model.Dispose();
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }
    internal void FocusQuery() { Query.Focus(); Query.SelectAll(); }
    private void SelectMode(bool assistant)
    {
        _model.Mode(assistant);
        Results.Visibility = assistant ? Visibility.Collapsed : Visibility.Visible;
        Send.Visibility = ReplyPanel.Visibility = assistant ? Visibility.Visible : Visibility.Collapsed;
        SearchTab.SetResourceReference(BorderBrushProperty, assistant ? "Surface" : "Accent");
        AssistantTab.SetResourceReference(BorderBrushProperty, assistant ? "Accent" : "Surface");
        Query.ToolTip = assistant ? "Escreva uma pergunta para a OpenAI" : "Pesquise aplicativos, arquivos e comandos";
        UpdateSuggestion();
        FocusQuery();
    }
    private void UpdateSuggestion() => Suggested.Visibility = _model.Suggested == null ? Visibility.Collapsed : Visibility.Visible;
    private void SuggestionClick(object sender, RoutedEventArgs e) => Open(_model.Suggested);
    private void SettingsClick(object sender, RoutedEventArgs e) => _shell.OpenSettings();
    private void SearchClick(object sender, RoutedEventArgs e) => SelectMode(false);
    private void AssistantClick(object sender, RoutedEventArgs e) => SelectMode(true);
    private async void SendClick(object sender, RoutedEventArgs e) => await _model.Ask();
    private async void QueryKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { e.Handled = true; if (_model.Assistant) await _model.Ask(); else Open(Results.SelectedItem as CenterResult ?? _model.Results.FirstOrDefault()); }
        else if (e.Key == Key.Down && Results.Items.Count > 0) { Results.Focus(); Results.SelectedIndex = 0; e.Handled = true; }
    }
    private void ResultDoubleClick(object sender, MouseButtonEventArgs e) => Open(Results.SelectedItem as CenterResult);
    private void ResultKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { Open(Results.SelectedItem as CenterResult); e.Handled = true; } }
    private void Open(CenterResult? result)
    {
        if (result == null) return;
        if (result.Action == "history") { _model.Query = result.Name; FocusQuery(); return; }
        if (result.Hit is { } hit)
        {
            if (_shell.Execute(() => { _shell.Services.Launch(hit); _shell.Services.Remember(_model.Query); })) Close();
            return;
        }
        if (MessageBox.Show(this, result.Name + "?", "Confirmar comando local", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        if (result.Action == "settings") _shell.OpenSettings();
        else if (result.Action == "exit") _shell.Exit();
    }
}

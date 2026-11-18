using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace SoBar.Widgets;

public sealed partial class WidgetService
{
    private string? codexExecutable;
    private bool codexConsentLoaded;
    private CancellationTokenSource? codexRequest;
    private long? codexLastQuery;
    private string CodexConsentFile => Path.Combine(directory,"codex-limits.opt-in");
    private void LoadCodexConsent()
    {
        if (codexConsentLoaded) return;
        codexConsentLoaded = true;
        try
        {
            if (File.Exists(CodexConsentFile) && new FileInfo(CodexConsentFile).Length <= 65536)
                codexExecutable = File.ReadAllText(CodexConsentFile).Trim();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
    partial void RefreshCodexLimits()
    {
        LoadCodexConsent();
        if (codexExecutable == null)
        {
            Publish("codex-limits", new("Conectar Codex","Clique para autorizar a leitura dos limites da sua conta no Codex.",
                Card:new("—","—","Codex","Conectar")));
            return;
        }
        if (codexRequest != null || (codexLastQuery.HasValue && Stopwatch.GetElapsedTime(codexLastQuery.Value).TotalMinutes < 15)) return;
        codexLastQuery = Stopwatch.GetTimestamp();
        codexRequest = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        _ = ReadCodexAsync(codexExecutable,codexRequest);
    }
    private async Task ReadCodexAsync(string path,CancellationTokenSource request)
    {
        try
        {
            var value = await Task.Run(() => CodexLimits.QueryAsync(path,request.Token),request.Token);
            if (!request.IsCancellationRequested) Publish("codex-limits",value);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            if (!request.IsCancellationRequested)
                Publish("codex-limits",new("Indisponível","Abra o Codex, confira o login e o caminho do executável. A próxima leitura ocorre em 15 minutos.",
                    Card:new("—","—","Sessão","Semana")));
        }
        finally
        {
            if (ReferenceEquals(codexRequest,request)) codexRequest = null;
            request.Dispose();
        }
    }
    partial void BuildCodexPanel(StackPanel body)
    {
        LoadCodexConsent();
        body.Children.Add(Paragraph("O SoBar consulta os percentuais usados e as renovações pelo Codex instalado. A consulta usa a sessão já conectada, a cada 15 minutos enquanto o cartão estiver ativo."));
        var status = Paragraph(codexExecutable ?? "Desconectado");
        body.Children.Add(status);
        void Connect(string path)
        {
            if (!CodexLimits.IsExecutable(path)) { status.Text = "Selecione o codex.exe instalado."; return; }
            if (MessageBox.Show(Window.GetWindow(body), "Autorizar este executável a consultar os limites da sua conta?\n\n" + path,
                "Conectar Codex",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No) != MessageBoxResult.Yes) return;
            try
            {
                Directory.CreateDirectory(directory);
                File.WriteAllText(CodexConsentFile,path);
                DisposeCodexLimits();
                codexExecutable = path;
                codexLastQuery = null;
                status.Text = path;
                RefreshCodexLimits();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { status.Text = "Falha ao salvar a conexão."; }
        }
        AddAction(body,"Conectar Codex instalado", () => {
            var path = CodexLimits.Locate();
            if (path != null) { Connect(path); return; }
            var picker = new OpenFileDialog { Filter="Codex|codex.exe",Title="Localizar Codex" };
            if (picker.ShowDialog(Window.GetWindow(body)) == true) Connect(picker.FileName);
        });
        AddAction(body,"Desconectar",() => {
            try { File.Delete(CodexConsentFile); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { status.Text = "Falha ao remover a autorização."; return; }
            DisposeCodexLimits();
            codexExecutable = null;
            codexLastQuery = null;
            status.Text = "Desconectado";
            RefreshCodexLimits();
        });
    }
    partial void DisposeCodexLimits()
    {
        codexRequest?.Cancel();
        codexRequest = null;
    }
}

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SoBar.Core;

namespace SoBar.Infrastructure;

internal sealed class Assistant : IDisposable
{
    private readonly HttpClient _client;
    private readonly Queue<DateTimeOffset> _requests = new();
    internal Assistant(HttpMessageHandler? handler = null)
    {
        _client = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false }) {
            Timeout = TimeSpan.FromSeconds(45), MaxResponseContentBufferSize = 512 * 1024
        };
    }
    public async Task<AssistantReply> AskAsync(string key,string prompt,CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(prompt) || prompt.Length > 4000) throw new ArgumentException("Digite até 4.000 caracteres.");
        lock (_requests)
        {
            var now = DateTimeOffset.UtcNow;
            while (_requests.TryPeek(out var first) && now - first >= TimeSpan.FromMinutes(1)) _requests.Dequeue();
            if (_requests.Count >= 5) throw new InvalidOperationException("Limite de cinco consultas por minuto.");
            _requests.Enqueue(now);
        }
        using var request = new HttpRequestMessage(HttpMethod.Post,"https://api.openai.com/v1/responses");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",key);
        request.Content = JsonContent.Create(new { model = "gpt-4.1-mini", input = prompt, store = false, max_output_tokens = 512,
            instructions = "Responda em português. Se solicitado, proponha abrir as configurações ou fechar o SoBar. O usuário precisa confirmar a ação.",
            tools = new[] { new { type = "function", name = "propor_acao", description = "Propõe uma ação no SoBar para aprovação do usuário.",
                strict = true, parameters = new { type = "object", properties = new { action = new { type = "string", @enum = new[] { "settings", "exit" } } },
                required = new[] { "action" }, additionalProperties = false } } } });
        using var response = await _client.SendAsync(request,token).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException($"A OpenAI retornou HTTP {(int)response.StatusCode}. Confira a chave, a cota e o acesso ao modelo.");
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false));
        var text = new List<string>();
        string? action = null;
        if (payload.RootElement.TryGetProperty("output",out var items))
            foreach (var item in items.EnumerateArray())
            {
                if (item.TryGetProperty("type",out var type) && type.GetString() == "function_call" &&
                    item.TryGetProperty("name",out var name) && name.GetString() == "propor_acao" &&
                    item.TryGetProperty("arguments",out var args))
                {
                    using var parsed = JsonDocument.Parse(args.GetString() ?? "{}");
                    if (parsed.RootElement.TryGetProperty("action",out var value) && value.GetString() is "settings" or "exit")
                        action = value.GetString();
                }
                if (item.TryGetProperty("content",out var parts))
                    foreach (var part in parts.EnumerateArray())
                        if (part.TryGetProperty("type",out var kind) && kind.GetString() == "output_text" &&
                            part.TryGetProperty("text",out var content)) text.Add(content.GetString() ?? "");
            }
        if (text.Count == 0 && action == null) throw new InvalidDataException("Resposta sem conteúdo de texto.");
        return new AssistantReply(text.Count == 0 ? "Revise a ação sugerida abaixo." : string.Join("\n",text), action);
    }
    public void Dispose() => _client.Dispose();
}

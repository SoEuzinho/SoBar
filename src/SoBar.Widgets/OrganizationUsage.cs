using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SoBar.Widgets;

internal sealed class OrganizationUsage(string directory)
{
    private string CredentialPath => Path.Combine(directory, "usage-admin.dpapi");
    public bool Connected { get; private set; } = File.Exists(Path.Combine(directory, "usage-admin.dpapi"));

    public void Connect(string key)
    {
        if (key.Length is < 8 or > 4096 || key.Any(char.IsWhiteSpace) || key.Any(char.IsControl))
            throw new ArgumentException("Informe uma chave de administração válida.");
        Directory.CreateDirectory(directory);
        var bytes = Encoding.UTF8.GetBytes(key);
        var temporary = Path.Combine(directory, $".usage-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllBytes(temporary, ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser));
            File.Move(temporary, CredentialPath, true);
            Connected = true;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
            File.Delete(temporary);
        }
    }

    public void Disconnect()
    {
        if (Directory.Exists(directory)) File.Delete(CredentialPath);
        Connected = false;
    }

    internal async Task<Reading> ReadAsync(HttpClient client, DateTimeOffset now, CancellationToken cancellation)
    {
        if (!Connected) return Reading.Missing("Conecte uma chave administrativa da organização.");
        var bytes = ProtectedData.Unprotect(File.ReadAllBytes(CredentialPath), null, DataProtectionScope.CurrentUser);
        try
        {
            long start = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero).ToUnixTimeSeconds();
            long end = now.ToUnixTimeSeconds();
            if (start == end) return Reading.Missing("O período de hoje (UTC) está começando.");
            var url = $"https://api.openai.com/v1/organization/usage/completions?start_time={start}&end_time={end}&bucket_width=1d&limit=1";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Encoding.UTF8.GetString(bytes));
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return Reading.Missing(response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "Acesso recusado. Verifique a chave Admin e a permissão para consultar o uso.",
                    HttpStatusCode.TooManyRequests => "O provedor limitou as consultas. Nova tentativa em 15 minutos.",
                    _ => $"Consulta indisponível · HTTP {(int)response.StatusCode}"
                });
            using var stream = await response.Content.ReadAsStreamAsync(cancellation).ConfigureAwait(false);
            using var data = await JsonDocument.ParseAsync(stream, cancellationToken: cancellation).ConfigureAwait(false);
            if (data.RootElement.GetProperty("has_more").GetBoolean()) return Reading.Missing("O provedor retornou um período incompleto.");
            long input = 0, output = 0, requests = 0;
            checked
            {
                foreach (var bucket in data.RootElement.GetProperty("data").EnumerateArray())
                    foreach (var item in bucket.GetProperty("results").EnumerateArray())
                    {
                        input += item.GetProperty("input_tokens").GetInt64();
                        output += item.GetProperty("output_tokens").GetInt64();
                        requests += item.GetProperty("num_model_requests").GetInt64();
                    }
            }
            return new($"{checked(input + output):N0} tok", $"Uso de Completions da organização · hoje (UTC)\nEntrada: {input:N0} · Saída: {output:N0}\n{requests:N0} solicitações · Consulta às {now.UtcDateTime:HH:mm} UTC");
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
}

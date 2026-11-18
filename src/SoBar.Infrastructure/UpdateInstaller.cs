using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SoBar.Infrastructure;

internal sealed record ReleaseSignature(string Version, string Sha256, string Signature);
internal static class UpdateInstaller
{
    internal static void Verify(Stream package, ReleaseSignature release, RSA publisher, Version current)
    {
        if (!Version.TryParse(release.Version,out var next) || next <= current)
            throw new InvalidDataException("Selecione uma versão mais recente do SoBar.");
        var digest = Convert.ToHexString(SHA256.HashData(package));
        var message = Encoding.UTF8.GetBytes($"SoBar-MSI-v1\n{release.Version}\n{digest}\n");
        if (!string.Equals(digest,release.Sha256,StringComparison.Ordinal) ||
            !publisher.VerifyData(message,Convert.FromBase64String(release.Signature),HashAlgorithmName.SHA256,RSASignaturePadding.Pss))
            throw new CryptographicException("O instalador ou a assinatura não corresponde à publicação do SoBar.");
    }
    public static void Start(string path)
    {
        if (!Path.GetExtension(path).Equals(".msi",StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Selecione o MSI do SoBar.");
        using var package = File.OpenRead(path);
        if (package.Length > 600L * 1024 * 1024) throw new InvalidDataException("Instalador muito grande.");
        using var signatureFile = File.OpenRead(path + ".sig");
        if (signatureFile.Length > 8192) throw new InvalidDataException("Assinatura inválida.");
        var release = JsonSerializer.Deserialize<ReleaseSignature>(signatureFile) ?? throw new InvalidDataException("Assinatura ausente.");
        using var publicKey = RSA.Create();
        using var reader = new StreamReader(typeof(UpdateInstaller).Assembly.GetManifestResourceStream("SoBar.Publisher")!);
        publicKey.FromXmlString(reader.ReadToEnd());
        Verify(package, release, publicKey, typeof(UpdateInstaller).Assembly.GetName().Version!);
        package.Position = 0;
        var cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SoBar","updates");
        Directory.CreateDirectory(cache);
        var staged = Path.Combine(cache,Guid.NewGuid().ToString("N") + ".msi");
        using (var dest = new FileStream(staged,FileMode.CreateNew,FileAccess.Write,FileShare.None)) package.CopyTo(dest);
        var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory,"msiexec.exe")) { UseShellExecute = false };
        start.ArgumentList.Add("/i");
        start.ArgumentList.Add(staged);
        using var process = Process.Start(start) ?? throw new IOException("O Windows Installer não iniciou.");
    }
}

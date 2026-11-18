using System.Diagnostics;
using SoBar.Core;

namespace SoBar.Infrastructure;

internal sealed class LocalSearch
{
    private static readonly HashSet<string> Documents = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt",".md",".pdf",".docx",".xlsx",".pptx",".csv",".jpg",".jpeg",".png",".webp",".mp3",".wav",".mp4"
    };
    private readonly SemaphoreSlim _indexLock = new(1);
    private List<SearchHit> _items = [];
    private DateTime _refreshed;
    private static readonly string[] AppRoots =
    [
        Environment.GetFolderPath(Environment.SpecialFolder.Programs),
        Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms)
    ];
    public async Task<IReadOnlyList<SearchHit>> FindAsync(string query, CancellationToken token)
    {
        query = query.Trim();
        if (query.Length is < 1 or > 256) return [];
        await _indexLock.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (DateTime.UtcNow - _refreshed > TimeSpan.FromMinutes(2))
            {
                _items = await Task.Run(() => Index(token),token).ConfigureAwait(false);
                _refreshed = DateTime.UtcNow;
            }
            return _items.Where(x => x.Name.Contains(query,StringComparison.CurrentCultureIgnoreCase))
                .OrderByDescending(x => x.Name.StartsWith(query,StringComparison.CurrentCultureIgnoreCase))
                .ThenBy(x => x.Name.Length).ThenBy(x => x.Name).Take(35).ToArray();
        }
        finally { _indexLock.Release(); }
    }
    private static List<SearchHit> Index(CancellationToken token)
    {
        var candidates = new Dictionary<string,SearchHit>(StringComparer.OrdinalIgnoreCase);
        void Scan(string root,bool apps,int limit)
        {
            if (!Directory.Exists(root)) return;
            var options = new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true, MaxRecursionDepth = 6,
                AttributesToSkip = FileAttributes.System | FileAttributes.Hidden | FileAttributes.ReparsePoint };
            try
            {
                foreach (var path in Directory.EnumerateFiles(root,"*",options).Take(limit))
                {
                    token.ThrowIfCancellationRequested();
                    var extension = Path.GetExtension(path);
                    if (apps ? extension.Equals(".lnk",StringComparison.OrdinalIgnoreCase) : Documents.Contains(extension))
                        candidates.TryAdd(path,new SearchHit(Path.GetFileNameWithoutExtension(path),path,apps ? "Aplicativo" : "Arquivo"));
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        foreach (var root in AppRoots) Scan(root,true,4000);
        Scan(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),false,4000);
        Scan(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),false,10000);
        return candidates.Values.ToList();
    }
    public void Launch(SearchHit hit)
    {
        var path = Path.GetFullPath(hit.Path);
        var extension = Path.GetExtension(path);
        bool menuLink = extension.Equals(".lnk",StringComparison.OrdinalIgnoreCase) && AppRoots.Any(root =>
            !string.IsNullOrEmpty(root) && path.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase));
        if (!File.Exists(path) || (!AppSettings.IsLocalApplication(path) && !menuLink && !Documents.Contains(extension)))
            throw new InvalidDataException("O item não pode ser aberto.");
        using var process = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }
}

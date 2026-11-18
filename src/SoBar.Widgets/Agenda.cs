using System.Globalization;
using System.IO;
using System.Text.Json;

namespace SoBar.Widgets;

internal sealed record Appointment(Guid Id, DateTimeOffset Start, string Title)
{
    public string Display => $"{Start.ToLocalTime():dd/MM/yyyy HH:mm}   {Title}";
}

internal sealed class Agenda(string directory)
{
    private readonly string path = Path.Combine(directory, "agenda.json");
    private List<Appointment>? entries;

    public IReadOnlyList<Appointment> All()
    {
        if (entries != null) return entries;
        if (!File.Exists(path)) return entries = [];
        using var stream = File.OpenRead(path);
        if (stream.Length > 256 * 1024) throw new InvalidDataException("Arquivo de agenda excede o tamanho permitido.");
        var loaded = JsonSerializer.Deserialize<List<Appointment>>(stream) ?? throw new InvalidDataException("Agenda vazia ou inválida.");
        if (loaded.Count > 256 || loaded.Any(a => a == null || a.Id == Guid.Empty || !ValidTitle(a.Title)) || loaded.Select(a => a.Id).Distinct().Count() != loaded.Count)
            throw new InvalidDataException("Agenda inválida.");
        return entries = loaded.OrderBy(a => a.Start).ToList();
    }

    public Appointment? Next(DateTimeOffset now) => All().FirstOrDefault(a => a.Start > now.AddMinutes(-1));

    public void Save(Guid? id, string title, DateTimeOffset start)
    {
        title = title.Trim();
        if (!ValidTitle(title)) throw new ArgumentException("Informe um título de até 100 caracteres em uma linha.");
        var list = All().ToList();
        var entry = new Appointment(id ?? Guid.NewGuid(), start, title);
        if (id.HasValue)
        {
            var index = list.FindIndex(a => a.Id == id.Value);
            if (index < 0) throw new InvalidOperationException("O compromisso já foi removido.");
            list[index] = entry;
        }
        else
        {
            if (list.Count >= 256) throw new InvalidOperationException("A agenda comporta até 256 compromissos.");
            list.Add(entry);
        }
        Write(list);
    }

    public void Remove(Guid id) => Write(All().Where(a => a.Id != id).ToList());

    private static bool ValidTitle(string? title) =>
        !string.IsNullOrWhiteSpace(title) && title.Length <= 100 && !title.Any(char.IsControl);

    private void Write(List<Appointment> list)
    {
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $".agenda-{Guid.NewGuid():N}.tmp");
        try
        {
            list.Sort((a, b) => a.Start.CompareTo(b.Start));
            File.WriteAllBytes(temporary, JsonSerializer.SerializeToUtf8Bytes(list));
            File.Move(temporary, path, true);
            entries = list;
        }
        finally { File.Delete(temporary); }
    }

    internal static DateTimeOffset LocalStart(DateTime date, string hour)
    {
        if (!TimeOnly.TryParseExact(hour.Trim(), ["H:mm", "HH:mm"], CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var time)) throw new ArgumentException("Informe a hora no formato HH:mm.");
        var local = DateTime.SpecifyKind(date.Date.Add(time.ToTimeSpan()), DateTimeKind.Unspecified);
        if (TimeZoneInfo.Local.IsInvalidTime(local)) throw new ArgumentException("Esse horário não existe no fuso local.");
        return new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local));
    }
}

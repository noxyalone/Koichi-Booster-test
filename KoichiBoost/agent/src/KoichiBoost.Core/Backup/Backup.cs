using System.Globalization;
using System.Text.Json;
using KoichiBoost.Core.Reporting;

namespace KoichiBoost.Core.Backup;

/// <summary>Uma alteração de valor no registro, com o estado ANTERIOR guardado para restauração.</summary>
public sealed record RegistryChange(
    string Hive, string Path, string Name,
    bool ExistedBefore, string? KindBefore, string? ValueBefore,
    string KindAfter, string ValueAfter);

public sealed record PowerPlanChange(string PreviousGuid, string NewGuid);

public sealed record ActionRecord(
    string ChangeId, string Title, DateTimeOffset AppliedAt,
    IReadOnlyList<RegistryChange> Registry, PowerPlanChange? PowerPlan, bool Reverted = false);

public sealed record BackupSession(string Id, DateTimeOffset CreatedAt, string AgentVersion, IReadOnlyList<ActionRecord> Actions);

/// <summary>Converte valores do registro para texto (e de volta). Kinds: DWord, QWord, String, ExpandString, MultiString, Binary.</summary>
public static class ValueCodec
{
    public static string Encode(string kind, object value) => kind switch
    {
        "DWord" => Convert.ToInt32(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
        "QWord" => Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
        "Binary" => Convert.ToBase64String((byte[])value),
        "MultiString" => string.Join("\0", (string[])value),
        _ => value.ToString() ?? ""
    };

    public static object Decode(string kind, string text) => kind switch
    {
        "DWord" => int.Parse(text, CultureInfo.InvariantCulture),
        "QWord" => long.Parse(text, CultureInfo.InvariantCulture),
        "Binary" => Convert.FromBase64String(text),
        "MultiString" => text.Split('\0'),
        _ => text
    };
}

/// <summary>Guarda sessões de backup em disco (um JSON por sessão). Escrita atômica.</summary>
public sealed class BackupStore
{
    public string Root { get; }

    public BackupStore(string root) => Root = root;

    public static string DefaultRoot =>
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KoichiBoost", "backups");

    private string PathFor(string id) => System.IO.Path.Combine(Root, id + ".json");

    public void Save(BackupSession session)
    {
        Directory.CreateDirectory(Root);
        var target = PathFor(session.Id);
        var tmp = target + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(session, JsonReportWriter.Options));
        File.Move(tmp, target, overwrite: true);
    }

    public BackupSession? Load(string id)
    {
        var p = PathFor(id);
        return File.Exists(p) ? JsonSerializer.Deserialize<BackupSession>(File.ReadAllText(p), JsonReportWriter.Options) : null;
    }

    /// <summary>Sessões da mais nova para a mais antiga.</summary>
    public IReadOnlyList<BackupSession> List()
    {
        if (!Directory.Exists(Root)) return [];
        return Directory.EnumerateFiles(Root, "*.json")
            .Select(f => { try { return JsonSerializer.Deserialize<BackupSession>(File.ReadAllText(f), JsonReportWriter.Options); } catch { return null; } })
            .Where(s => s is not null).Select(s => s!)
            .OrderByDescending(s => s.CreatedAt).ToList();
    }
}

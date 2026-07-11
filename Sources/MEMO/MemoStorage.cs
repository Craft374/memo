using System.Text;
using System.Text.Json;

namespace Memo;

public sealed class MemoStorage
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string baseDir;
    private readonly string tabsDir;
    private readonly string manifestPath;
    private readonly string settingsPath;

    public MemoStorage()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        baseDir = Path.Combine(appData, "MEMO");
        tabsDir = Path.Combine(baseDir, "tabs");
        manifestPath = Path.Combine(baseDir, "tabs.json");
        settingsPath = Path.Combine(baseDir, "settings.json");
    }

    public string BaseDirectory => baseDir;

    public MemoSession LoadSession()
    {
        try
        {
            if (File.Exists(manifestPath))
            {
                var session = JsonSerializer.Deserialize<MemoSession>(File.ReadAllText(manifestPath));
                if (session != null)
                {
                    var tabs = NormalizedTabs(session.Tabs);
                    if (tabs.Count > 0)
                    {
                        string? selectedId = tabs.Any(t => t.Id == session.SelectedTabId)
                            ? session.SelectedTabId
                            : tabs[0].Id;
                        return new MemoSession { Tabs = tabs, SelectedTabId = selectedId };
                    }
                }
            }
        }
        catch
        {
        }

        return new MemoSession { Tabs = new List<MemoTab> { MemoTab.Create(1) }, SelectedTabId = null };
    }

    public void SaveSession(MemoSession session)
    {
        Directory.CreateDirectory(baseDir);
        AtomicWrite(manifestPath, JsonSerializer.Serialize(session, JsonOptions));
    }

    public string? LoadTabRtf(string id) => TryRead(TabPath(id, ".rtf"));

    public string? LoadTabText(string id) => TryRead(TabPath(id, ".txt"));

    public void SaveTab(string id, string rtf, string plainText)
    {
        Directory.CreateDirectory(tabsDir);
        AtomicWrite(TabPath(id, ".rtf"), rtf);
        AtomicWrite(TabPath(id, ".txt"), plainText);
    }

    public void DeleteTab(string id)
    {
        try { File.Delete(TabPath(id, ".rtf")); } catch { }
        try { File.Delete(TabPath(id, ".txt")); } catch { }
    }

    public MemoSettings LoadSettings()
    {
        try
        {
            if (File.Exists(settingsPath))
            {
                var settings = JsonSerializer.Deserialize<MemoSettings>(File.ReadAllText(settingsPath));
                if (settings != null)
                {
                    return settings;
                }
            }
        }
        catch
        {
        }

        return new MemoSettings();
    }

    public void SaveSettings(MemoSettings settings)
    {
        Directory.CreateDirectory(baseDir);
        AtomicWrite(settingsPath, JsonSerializer.Serialize(settings, JsonOptions));
    }

    private static List<MemoTab> NormalizedTabs(List<MemoTab> tabs)
    {
        var seen = new HashSet<string>();
        var result = new List<MemoTab>();

        foreach (var tab in tabs)
        {
            string id = NormalizedId(tab.Id);
            if (id.Length == 0 || !seen.Add(id))
            {
                continue;
            }

            string title = (tab.Title ?? "").Trim();
            result.Add(new MemoTab
            {
                Id = id,
                Title = title.Length == 0 ? $"메모 {seen.Count}" : title,
                BodyIsSeparate = tab.BodyIsSeparate,
            });
        }

        return result;
    }

    private static string NormalizedId(string id)
    {
        var builder = new StringBuilder(id.Length);
        foreach (char c in id)
        {
            if (char.IsLetterOrDigit(c) || c == '-' || c == '_')
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    private string TabPath(string id, string extension) =>
        Path.Combine(tabsDir, NormalizedId(id) + extension);

    private static string? TryRead(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch
        {
            return null;
        }
    }

    private static void AtomicWrite(string path, string content)
    {
        string tmp = path + ".tmp";
        File.WriteAllText(tmp, content, new UTF8Encoding(false));
        File.Move(tmp, path, overwrite: true);
    }
}

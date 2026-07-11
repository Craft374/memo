using System.Text.Json.Serialization;

namespace Memo;

public sealed class MemoTab
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("bodyIsSeparate")]
    public bool BodyIsSeparate { get; set; }

    public static MemoTab Create(int index) => new()
    {
        Id = Guid.NewGuid().ToString("D").ToUpperInvariant(),
        Title = $"메모 {index}",
        BodyIsSeparate = true,
    };
}

public sealed class MemoSession
{
    [JsonPropertyName("tabs")]
    public List<MemoTab> Tabs { get; set; } = new();

    [JsonPropertyName("selectedTabID")]
    public string? SelectedTabId { get; set; }
}

public sealed class WindowPlacement
{
    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("maximized")]
    public bool Maximized { get; set; }
}

public sealed class MemoSettings
{
    [JsonPropertyName("fontFamily")]
    public string? FontFamily { get; set; }

    [JsonPropertyName("fontSize")]
    public float FontSize { get; set; } = Theme.BaseFontSize;

    [JsonPropertyName("lineWrapEnabled")]
    public bool LineWrapEnabled { get; set; } = true;

    [JsonPropertyName("window")]
    public WindowPlacement? Window { get; set; }
}

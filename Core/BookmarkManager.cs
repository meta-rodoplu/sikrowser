using System.Text.Json;

namespace Sikrowser.Core;

public class BookmarkItem
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string IconEmoji { get; set; } = "🌐";

    public BookmarkItem() { }

    public BookmarkItem(string title, string url, string iconEmoji = "🌐")
    {
        Title = title;
        Url = url;
        IconEmoji = iconEmoji;
    }
}

/// <summary>
/// Ultra-lightweight bookmark manager.
/// Loads and stores bookmarks with zero network overhead and sub-kilobyte memory footprint.
/// </summary>
public sealed class BookmarkManager
{
    private static readonly Lazy<BookmarkManager> _instance =
        new(() => new BookmarkManager());

    public static BookmarkManager Instance => _instance.Value;

    private readonly List<BookmarkItem> _bookmarks = new();
    private readonly string _filePath;

    public IReadOnlyList<BookmarkItem> Bookmarks => _bookmarks.AsReadOnly();
    public event Action? BookmarksChanged;

    private BookmarkManager()
    {
        _filePath = Path.Combine(BrowserConfig.UserDataFolder, "bookmarks.json");
        LoadBookmarks();
    }

    public void AddBookmark(string title, string url, string emoji = "⭐")
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        if (string.IsNullOrWhiteSpace(title)) title = url;

        // Pick intuitive emoji based on domain
        if (emoji == "⭐" || emoji == "🌐")
        {
            string lower = url.ToLowerInvariant();
            if (lower.Contains("google")) emoji = "🔍";
            else if (lower.Contains("youtube")) emoji = "▶️";
            else if (lower.Contains("github")) emoji = "🐙";
            else if (lower.Contains("wikipedia")) emoji = "📖";
            else if (lower.Contains("reddit")) emoji = "🤖";
            else if (lower.Contains("twitter") || lower.Contains("x.com")) emoji = "🐦";
            else if (lower.Contains("duckduckgo")) emoji = "🦆";
            else if (lower.Contains("chatgpt") || lower.Contains("openai")) emoji = "🧠";
            else emoji = "🌐";
        }

        // Avoid duplicate exact URLs
        _bookmarks.RemoveAll(b => b.Url.Equals(url, StringComparison.OrdinalIgnoreCase));
        _bookmarks.Add(new BookmarkItem(title, url, emoji));

        SaveBookmarks();
        BookmarksChanged?.Invoke();
    }

    public void RemoveBookmark(BookmarkItem item)
    {
        if (_bookmarks.Remove(item))
        {
            SaveBookmarks();
            BookmarksChanged?.Invoke();
        }
    }

    private void LoadBookmarks()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                string json = File.ReadAllText(_filePath);
                var items = JsonSerializer.Deserialize<List<BookmarkItem>>(json);
                if (items != null && items.Count > 0)
                {
                    _bookmarks.Clear();
                    _bookmarks.AddRange(items);
                    return;
                }
            }
        }
        catch { }

        // Default minimalist starter bookmarks
        _bookmarks.Clear();
        _bookmarks.Add(new BookmarkItem("Google", "https://www.google.com", "🔍"));
        _bookmarks.Add(new BookmarkItem("YouTube", "https://www.youtube.com", "▶️"));
        _bookmarks.Add(new BookmarkItem("GitHub", "https://github.com", "🐙"));
        _bookmarks.Add(new BookmarkItem("Wikipedia", "https://wikipedia.org", "📖"));
        _bookmarks.Add(new BookmarkItem("DuckDuckGo", "https://duckduckgo.com", "🦆"));
        SaveBookmarks();
    }

    private void SaveBookmarks()
    {
        try
        {
            string dir = Path.GetDirectoryName(_filePath)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string json = JsonSerializer.Serialize(_bookmarks, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }
        catch { }
    }
}

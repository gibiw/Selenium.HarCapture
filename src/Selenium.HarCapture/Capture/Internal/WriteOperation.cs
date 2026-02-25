using Selenium.HarCapture.Models;

namespace Selenium.HarCapture.Capture.Internal;

internal readonly struct WriteOperation
{
    public enum OpType { Entry, Page }
    public OpType Type { get; }
    public HarEntry? Entry { get; }
    public HarPage? Page { get; }

    private WriteOperation(OpType type, HarEntry? entry = null, HarPage? page = null)
    {
        Type = type;
        Entry = entry;
        Page = page;
    }

    public static WriteOperation CreateEntry(HarEntry entry) =>
        new WriteOperation(OpType.Entry, entry: entry);

    public static WriteOperation CreatePage(HarPage page) =>
        new WriteOperation(OpType.Page, page: page);
}

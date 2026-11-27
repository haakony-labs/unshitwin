using UnshitWindows.Clips;

namespace UnshitWindows.Tests;

public class ClipboardHistoryTests
{
    private static string[] Texts(ClipboardHistory history) =>
        history.Items.Select(e => ((TextEntry)e).Text).ToArray();

    [Fact]
    public void Add_PutsNewestFirst()
    {
        var history = new ClipboardHistory(10);
        history.Add(new TextEntry("a"));
        history.Add(new TextEntry("b"));
        history.Add(new TextEntry("c"));
        Assert.Equal(["c", "b", "a"], Texts(history));
    }

    [Fact]
    public void Add_DropsOldestBeyondCapacity()
    {
        var history = new ClipboardHistory(3);
        foreach (var t in new[] { "1", "2", "3", "4", "5" }) history.Add(new TextEntry(t));
        Assert.Equal(["5", "4", "3"], Texts(history));
    }

    [Fact]
    public void Add_DuplicateMovesExistingEntryToTop()
    {
        var history = new ClipboardHistory(10);
        var first = history.Add(new TextEntry("a"));
        history.Add(new TextEntry("b"));
        var kept = history.Add(new TextEntry("a"));

        Assert.Same(first, kept);
        Assert.Equal(["a", "b"], Texts(history));
    }

    [Fact]
    public void Add_TreatsFileListsCaseInsensitively()
    {
        var history = new ClipboardHistory(10);
        history.Add(new FilesEntry([@"C:\Temp\A.txt"]));
        history.Add(new FilesEntry([@"c:\temp\a.txt"]));
        Assert.Single(history.Items);
    }

    [Fact]
    public void Add_DifferentTypesAreNotDuplicates()
    {
        var history = new ClipboardHistory(10);
        history.Add(new TextEntry(@"C:\a.txt"));
        history.Add(new FilesEntry([@"C:\a.txt"]));
        Assert.Equal(2, history.Items.Count);
    }

    [Fact]
    public void MoveToTop_ReordersWithoutDuplicating()
    {
        var history = new ClipboardHistory(10);
        var a = history.Add(new TextEntry("a"));
        history.Add(new TextEntry("b"));
        history.Add(new TextEntry("c"));

        history.MoveToTop(a);

        Assert.Equal(["a", "c", "b"], Texts(history));
    }

    [Fact]
    public void Remove_AndClear()
    {
        var history = new ClipboardHistory(10);
        var a = history.Add(new TextEntry("a"));
        history.Add(new TextEntry("b"));

        Assert.True(history.Remove(a));
        Assert.Equal(["b"], Texts(history));

        history.Clear();
        Assert.Empty(history.Items);
    }

    [Fact]
    public void ShrinkingCapacity_TrimsOldest()
    {
        var history = new ClipboardHistory(10);
        foreach (var t in new[] { "1", "2", "3", "4", "5" }) history.Add(new TextEntry(t));

        history.Capacity = 2;

        Assert.Equal(["5", "4"], Texts(history));
    }

    [Fact]
    public void Changed_IsRaisedOnEveryMutation()
    {
        var history = new ClipboardHistory(10);
        int count = 0;
        history.Changed += (_, _) => count++;

        var a = history.Add(new TextEntry("a"));
        history.Add(new TextEntry("b"));
        history.MoveToTop(a);
        history.Remove(a);
        history.Clear();

        Assert.Equal(5, count);
    }
}

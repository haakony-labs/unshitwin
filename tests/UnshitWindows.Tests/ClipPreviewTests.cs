using UnshitWindows.Clips;

namespace UnshitWindows.Tests;

public class ClipPreviewTests
{
    [Fact]
    public void Text_CollapsesWhitespaceAndTrims()
    {
        Assert.Equal("hello world again", ClipPreview.ForText("  hello\r\n\tworld   again \n"));
    }

    [Fact]
    public void Text_ShortTextIsUnchanged()
    {
        Assert.Equal("abc", ClipPreview.ForText("abc", maxLength: 3));
    }

    [Fact]
    public void Text_LongTextIsTruncatedWithEllipsis()
    {
        string preview = ClipPreview.ForText(new string('x', 500), maxLength: 10);
        Assert.Equal(10, preview.Length);
        Assert.EndsWith("…", preview);
    }

    [Fact]
    public void Text_DoesNotSplitSurrogatePairs()
    {
        // 😀 is a surrogate pair; truncating right after its high half would produce invalid text.
        string preview = ClipPreview.ForText("abcd😀efghij", maxLength: 6);
        Assert.Equal("abcd…", preview);
    }

    [Fact]
    public void Text_OnlyWhitespace()
    {
        Assert.Equal("(only whitespace)", ClipPreview.ForText(" \r\n\t "));
    }

    [Fact]
    public void Files_SingleFile()
    {
        Assert.Equal("File: report.pdf", ClipPreview.ForFiles([@"C:\Users\me\report.pdf"]));
    }

    [Fact]
    public void Files_FolderWithTrailingSlash()
    {
        Assert.Equal("File: Docs", ClipPreview.ForFiles([@"C:\Users\me\Docs\"]));
    }

    [Fact]
    public void Files_ManyFilesAreSummarised()
    {
        string preview = ClipPreview.ForFiles([@"C:\a.txt", @"C:\b.txt", @"C:\c.txt", @"C:\d.txt"]);
        Assert.Equal("4 files: a.txt, b.txt, c.txt, …", preview);
    }

    [Fact]
    public void Files_DriveRootFallsBackToPath()
    {
        Assert.Equal(@"File: C:\", ClipPreview.ForFiles([@"C:\"]));
    }
}

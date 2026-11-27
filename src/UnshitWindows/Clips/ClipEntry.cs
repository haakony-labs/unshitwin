using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Text;

namespace UnshitWindows.Clips;

internal abstract class ClipEntry
{
    public abstract bool HasSameContent(ClipEntry other);

    /// <summary>Single-line description shown in the picker.</summary>
    public abstract string Preview { get; }
}

internal sealed class TextEntry(string text) : ClipEntry
{
    public string Text { get; } = text;

    public override bool HasSameContent(ClipEntry other) => other is TextEntry t && t.Text == Text;

    public override string Preview => ClipPreview.ForText(Text);
}

internal sealed class FilesEntry(IReadOnlyList<string> paths) : ClipEntry
{
    public IReadOnlyList<string> Paths { get; } = paths;

    public override bool HasSameContent(ClipEntry other) =>
        other is FilesEntry f && f.Paths.SequenceEqual(Paths, StringComparer.OrdinalIgnoreCase);

    public override string Preview => ClipPreview.ForFiles(Paths);
}

/// <summary>An image kept as PNG bytes plus a small thumbnail for the picker.</summary>
internal sealed class ImageEntry(byte[] png, Size size, Image thumbnail, string hash) : ClipEntry
{
    private const int ThumbnailWidth = 176;
    private const int ThumbnailHeight = 96;

    public byte[] Png { get; } = png;
    public Size Size { get; } = size;
    public Image Thumbnail { get; } = thumbnail;
    public string Hash { get; } = hash;

    public override bool HasSameContent(ClipEntry other) => other is ImageEntry i && i.Hash == Hash;

    public override string Preview => $"Image  {Size.Width} × {Size.Height}";

    public static ImageEntry FromImage(Image image)
    {
        using var bitmap = new Bitmap(image);
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        byte[] png = stream.ToArray();
        string hash = Convert.ToHexString(SHA256.HashData(png));
        return new ImageEntry(png, bitmap.Size, MakeThumbnail(bitmap), hash);
    }

    private static Bitmap MakeThumbnail(Bitmap source)
    {
        double scale = Math.Min(1.0, Math.Min((double)ThumbnailWidth / source.Width, (double)ThumbnailHeight / source.Height));
        int w = Math.Max(1, (int)Math.Round(source.Width * scale));
        int h = Math.Max(1, (int)Math.Round(source.Height * scale));
        var thumb = new Bitmap(w, h);
        using var g = Graphics.FromImage(thumb);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.DrawImage(source, 0, 0, w, h);
        return thumb;
    }
}

internal static class ClipPreview
{
    /// <summary>Collapses all whitespace runs to single spaces and truncates with an ellipsis.</summary>
    public static string ForText(string text, int maxLength = 120)
    {
        var sb = new StringBuilder(Math.Min(text.Length, maxLength + 1));
        bool pendingSpace = false;
        foreach (char c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = sb.Length > 0;
                continue;
            }
            if (pendingSpace)
            {
                sb.Append(' ');
                pendingSpace = false;
            }
            sb.Append(c);
            if (sb.Length > maxLength) break;
        }

        if (sb.Length == 0) return "(only whitespace)";
        if (sb.Length <= maxLength) return sb.ToString();

        sb.Length = maxLength - 1;
        if (char.IsHighSurrogate(sb[^1])) sb.Length--;
        return sb.Append('…').ToString();
    }

    public static string ForFiles(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0) return "(no files)";
        var names = paths.Select(DisplayName).ToList();
        if (names.Count == 1) return "File: " + names[0];
        string shown = string.Join(", ", names.Take(3));
        return $"{names.Count} files: {shown}{(names.Count > 3 ? ", …" : "")}";
    }

    private static string DisplayName(string path)
    {
        string name = Path.GetFileName(path.TrimEnd('\\', '/'));
        return name.Length > 0 ? name : path;
    }
}

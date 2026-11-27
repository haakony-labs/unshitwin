using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;

namespace UnshitWindows;

/// <summary>Small "About" dialog: name, version, author and source link.</summary>
internal sealed class AboutForm : Form
{
    private const string WebsiteUrl = "https://haakony.no";
    private const string RepositoryUrl = "https://github.com/haakony-labs/unshitwin";

    private readonly Bitmap _logo;

    public AboutForm(Icon icon)
    {
        Text = "About UnshitWindows";
        Icon = icon;
        Font = SystemFonts.MessageBoxFont;
        AutoScaleMode = AutoScaleMode.Font;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(16);

        using (var large = new Icon(icon, 48, 48)) _logo = large.ToBitmap();

        var layout = new TableLayoutPanel
        {
            ColumnCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Location = new Point(Padding.Left, Padding.Top),
        };

        var logo = new PictureBox
        {
            Image = _logo,
            SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size(48, 48),
            Margin = new Padding(0, 2, 16, 0),
        };
        layout.Controls.Add(logo, 0, 0);
        layout.SetRowSpan(logo, 6);

        var title = new Label
        {
            Text = "UnshitWindows",
            AutoSize = true,
            Font = new Font(Font.FontFamily, Font.SizeInPoints * 1.5f, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 2),
        };
        layout.Controls.Add(title, 1, 0);
        layout.Controls.Add(new Label { Text = VersionText(), AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(1, 0, 0, 10) }, 1, 1);
        layout.Controls.Add(new Label
        {
            Text = "Same-app window switching and clipboard history for Windows.",
            AutoSize = true,
            MaximumSize = new Size(300, 0),
            Margin = new Padding(1, 0, 0, 12),
        }, 1, 2);
        layout.Controls.Add(NewLink("Made by haakony.no", "haakony.no", WebsiteUrl), 1, 3);
        layout.Controls.Add(NewLink("Source code on GitHub", "GitHub", RepositoryUrl), 1, 4);

        var ok = new Button { Text = "OK", AutoSize = true, MinimumSize = new Size(80, 0), Anchor = AnchorStyles.Right, Margin = new Padding(0, 16, 0, 0) };
        ok.Click += (_, _) => Close();
        layout.Controls.Add(ok, 1, 5);
        AcceptButton = ok;
        CancelButton = ok;

        Controls.Add(layout);
        ActiveControl = ok;
    }

    private static LinkLabel NewLink(string text, string linkText, string url)
    {
        var link = new LinkLabel { Text = text, AutoSize = true, Margin = new Padding(1, 0, 0, 4) };
        link.LinkArea = new LinkArea(text.IndexOf(linkText, StringComparison.Ordinal), linkText.Length);
        link.LinkClicked += (_, _) => OpenUrl(url);
        return link;
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Win32Exception ex)
        {
            MessageBox.Show($"Couldn't open {url}: {ex.Message}", "UnshitWindows", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    /// <summary>"Version 0.1.0 (1c2a3d7)" from the informational version the SDK stamps (version+commit).</summary>
    private static string VersionText()
    {
        string info = typeof(AboutForm).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";
        string[] parts = info.Split('+', 2);
        if (parts[0].Length == 0) return "";
        return parts.Length == 2 && parts[1].Length >= 7 ? $"Version {parts[0]} ({parts[1][..7]})" : $"Version {parts[0]}";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _logo.Dispose();
        base.Dispose(disposing);
    }
}

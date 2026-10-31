using System.Text;
using System.Text.RegularExpressions;

namespace IgiQoL.Features.Notes;

// Tiny markdown subset to TMP rich text: #/## headings, - bullets, **bold**, *italic*, [text](url), bare http(s) links.
public static class NotesMarkdown
{
    private static readonly Regex Link = new(@"\[([^\]\r\n]+)\]\((https?://[^\s)""<]+)\)|(https?://[^\s<""]+)", RegexOptions.Compiled);
    private static readonly Regex Bold = new(@"\*\*(.+?)\*\*", RegexOptions.Compiled);
    private static readonly Regex Italic = new(@"(?<!\*)\*(?![\s*])(.+?)(?<![\s*])\*(?!\*)", RegexOptions.Compiled);

    public static string Render(string source)
    {
        if (string.IsNullOrEmpty(source)) return "";
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var sb = new StringBuilder(source.Length + 64);
        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0) sb.Append('\n');
            sb.Append(RenderLine(lines[i].Replace("<", "<noparse><</noparse>")));
        }
        return sb.ToString();
    }

    private static string RenderLine(string line)
    {
        if (line.StartsWith("## ")) return "<size=120%><b>" + Inline(line.Substring(3)) + "</b></size>";
        if (line.StartsWith("# ")) return "<size=140%><b>" + Inline(line.Substring(2)) + "</b></size>";
        if (line.StartsWith("- ")) return "  - " + Inline(line.Substring(2)); // the game's pixel font has no bullet glyph
        return Inline(line);
    }

    private static string Inline(string s)
    {
        s = Link.Replace(s, EvaluateLink);
        s = Bold.Replace(s, "<b>$1</b>");
        return Italic.Replace(s, "<i>$1</i>");
    }

    private static string EvaluateLink(Match m)
    {
        if (m.Groups[3].Success)
        {
            string url = m.Groups[3].Value;
            int end = url.Length;
            while (end > 0 && ".,;:!?)".IndexOf(url[end - 1]) >= 0) end--;
            return Wrap(url.Substring(0, end), url.Substring(0, end)) + url.Substring(end);
        }
        return Wrap(m.Groups[2].Value, m.Groups[1].Value);
    }

    private static string Wrap(string url, string text) =>
        "<link=\"" + url + "\"><color=#7FB2FF><u>" + text + "</u></color></link>";
}

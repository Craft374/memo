using System.Text;
using System.Text.RegularExpressions;

namespace Memo;

// Start/Length 범위를 Replacement로 바꾸고, SelectionStart가 있으면 그 선택으로 옮긴다 (없으면 삽입 끝에 커서).
internal sealed record TextEdit(int Start, int Length, string Replacement, int? SelectionStart = null, int SelectionLength = 0);

// 맥 버전 MemoTextLogic과 같은 규칙. WinForms에 의존하지 않아야 Tests 프로젝트에서 그대로 컴파일된다.
internal static class MemoTextLogic
{
    private static readonly Regex NumberedLine = new(@"^([\t ]*)([0-9]+)\.[\t ]*(.*)$");
    private static readonly Regex BulletLine = new(@"^([\t ]*)-(?:[\t ]+(.*))?$");

    public static TextEdit? ListEdit(string text, int caret)
    {
        if (caret < 0 || caret > text.Length)
        {
            return null;
        }

        var (lineStart, lineEnd) = LineAt(text, caret);
        string line = text[lineStart..lineEnd];

        var numbered = NumberedLine.Match(line);
        if (numbered.Success &&
            caret >= lineStart + numbered.Groups[3].Index &&
            long.TryParse(numbered.Groups[2].Value, out long number) &&
            number < long.MaxValue)
        {
            if (string.IsNullOrWhiteSpace(numbered.Groups[3].Value))
            {
                return new TextEdit(lineStart, lineEnd - lineStart, "");
            }

            return new TextEdit(caret, 0, $"\n{numbered.Groups[1].Value}{number + 1}. ");
        }

        var bullet = BulletLine.Match(line);
        if (!bullet.Success)
        {
            return null;
        }

        int bodyStart = bullet.Groups[2].Success ? bullet.Groups[2].Index : line.Length;
        if (caret < lineStart + bodyStart)
        {
            return null;
        }

        string indent = bullet.Groups[1].Value;
        if (string.IsNullOrWhiteSpace(bullet.Groups[2].Value))
        {
            // 번호 목록 아래 하위 글머리표를 비우고 Enter → 부모 번호 다음 번호로 돌아간다.
            return ParentNumber(text, lineStart, indent) is { } parent
                ? new TextEdit(lineStart, lineEnd - lineStart, $"{parent.Indent}{parent.Number + 1}. ")
                : new TextEdit(lineStart, lineEnd - lineStart, "");
        }

        return new TextEdit(caret, 0, $"\n{indent}- ");
    }

    public static TextEdit? IndentationEdit(string text, int selectionStart, int selectionLength, bool outdent)
    {
        int selectionEnd = selectionStart + selectionLength;
        if (selectionStart < 0 || selectionLength < 0 || selectionEnd > text.Length)
        {
            return null;
        }

        // 선택이 다음 줄 맨 앞에서 끝나면 그 줄은 건드리지 않는다.
        int lastIndex = selectionLength > 0 ? selectionEnd - 1 : selectionStart;
        int start = LineAt(text, selectionStart).Start;
        int lastNewline = text.IndexOf('\n', lastIndex);
        int end = lastNewline < 0 ? text.Length : lastNewline + 1;

        if (end == start)
        {
            return outdent ? null : new TextEdit(start, 0, "\t", selectionStart + 1);
        }

        var replacement = new StringBuilder(end - start + 16);
        var changes = new List<(int At, int Delta)>();
        for (int lineStart = start; lineStart < end;)
        {
            int newline = text.IndexOf('\n', lineStart, end - lineStart);
            int lineEnd = newline < 0 ? end : newline + 1;
            int copyFrom = lineStart;

            if (!outdent)
            {
                replacement.Append('\t');
                changes.Add((lineStart, 1));
            }
            else if (text[lineStart] == '\t')
            {
                copyFrom++;
                changes.Add((lineStart, -1));
            }

            replacement.Append(text, copyFrom, lineEnd - copyFrom);
            lineStart = lineEnd;
        }

        if (changes.Count == 0)
        {
            return null;
        }

        int newStart = selectionStart + changes
            .Where(c => c.At < selectionStart || (c.Delta > 0 && c.At == selectionStart))
            .Sum(c => c.Delta);
        int newEnd = selectionEnd + changes.Where(c => c.At < selectionEnd).Sum(c => c.Delta);

        return new TextEdit(start, end - start, replacement.ToString(), newStart, Math.Max(0, newEnd - newStart));
    }

    public static bool IsHorizontalRuleLine(ReadOnlySpan<char> line)
    {
        var trimmed = line.Trim();
        return trimmed.Length >= 3 && !trimmed.ContainsAnyExcept('_');
    }

    public static List<(int Start, int Length)> HorizontalRuleLineRanges(string text)
    {
        var ranges = new List<(int Start, int Length)>();
        for (int start = 0; start < text.Length;)
        {
            int end = text.IndexOf('\n', start);
            if (end < 0)
            {
                end = text.Length;
            }

            if (IsHorizontalRuleLine(text.AsSpan(start, end - start)))
            {
                ranges.Add((start, end - start));
            }

            start = end + 1;
        }

        return ranges;
    }

    // 정규식 폭주로 UI가 멈추면 저장 안 된 입력을 잃을 수 있어 제한 시간을 둔다.
    public static Regex SearchRegex(string pattern, bool useRegex) =>
        new(useRegex ? pattern : Regex.Escape(pattern), RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));

    // start부터 찾고, 없으면 문서 처음으로 돌아가 start 앞까지 찾는다.
    public static Match? NextMatch(Regex regex, string text, int start)
    {
        start = Math.Clamp(start, 0, text.Length);
        var match = regex.Match(text, start);
        if (!match.Success)
        {
            match = regex.Match(text, 0, start);
        }

        return match.Success ? match : null;
    }

    // index가 속한 줄의 시작과 끝(개행 제외)
    private static (int Start, int End) LineAt(string text, int index)
    {
        int start = index == 0 ? 0 : text.LastIndexOf('\n', index - 1) + 1;
        int end = text.IndexOf('\n', index);
        return (start, end < 0 ? text.Length : end);
    }

    private static (string Indent, long Number)? ParentNumber(string text, int lineStart, string bulletIndent)
    {
        for (int location = lineStart; location > 0;)
        {
            var (start, end) = LineAt(text, location - 1);
            string line = text[start..end];

            var numbered = NumberedLine.Match(line);
            if (numbered.Success)
            {
                string indent = numbered.Groups[1].Value;
                if (bulletIndent.Length > indent.Length &&
                    bulletIndent.StartsWith(indent, StringComparison.Ordinal) &&
                    long.TryParse(numbered.Groups[2].Value, out long number) &&
                    number < long.MaxValue)
                {
                    return (indent, number);
                }
            }
            else if (!BulletLine.IsMatch(line))
            {
                return null;
            }

            location = start;
        }

        return null;
    }
}

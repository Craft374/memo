namespace Memo;

public sealed class LineNumberGutter : Control
{
    private readonly MemoRichTextBox editor;
    private readonly List<int> lineStarts = new() { 0 };

    private Font editorBaseFont;
    private float zoomRatio = 1f;
    private float fontSizePt = Theme.BaseFontSize;
    private Font? numberFont;
    private float numberFontSize = -1f;
    private string? numberFontFamily;

    public LineNumberGutter(MemoRichTextBox editor, Font editorBaseFont)
    {
        this.editor = editor;
        this.editorBaseFont = editorBaseFont;

        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw,
            true);
        SetStyle(ControlStyles.Selectable, false);

        Dock = DockStyle.Left;
        BackColor = Theme.GutterBackground;
        UpdateWidth();
    }

    public void SetTypography(Font editorBaseFont, float zoomRatio, float fontSizePt)
    {
        this.editorBaseFont = editorBaseFont;
        this.zoomRatio = zoomRatio;
        this.fontSizePt = fontSizePt;
    }

    public void RebuildLineStarts(string text)
    {
        lineStarts.Clear();
        lineStarts.Add(0);
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                lineStarts.Add(i + 1);
            }
        }

        UpdateWidth();
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        editor.Focus();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Theme.GutterBackground);

        using (var pen = new Pen(Theme.GutterSeparator))
        {
            g.DrawLine(pen, Width - 1, 0, Width - 1, Height);
        }

        float scale = DeviceDpi / 96f;
        float lineHeight = MemoRichTextBox.GetLineHeightPixels(editorBaseFont, g.DpiY, zoomRatio);
        int rightPad = (int)(12 * scale);
        var font = GetNumberFont();
        float fontHeight = font.GetHeight(g);

        int textLength = editor.TextLength;
        if (textLength == 0)
        {
            var origin = editor.GetPositionFromCharIndex(0);
            int y = SaneY(origin.Y, (int)(10 * scale));
            DrawNumber(g, font, 1, y, lineHeight, fontHeight, rightPad);
            return;
        }

        int firstVisibleChar = Math.Max(0, editor.GetCharIndexFromPosition(new Point(2, 2)));
        int startLine = Math.Max(0, FindLineIndex(firstVisibleChar) - 1);

        int lastLineTopY = int.MinValue;
        for (int i = startLine; i < lineStarts.Count; i++)
        {
            int start = lineStarts[i];
            int y;
            if (start >= textLength)
            {
                // 마지막이 개행으로 끝나는 경우: 마지막 문자 표시줄 바로 아래가 빈 마지막 줄
                var last = editor.GetPositionFromCharIndex(textLength - 1);
                y = last.Y + (int)Math.Round(lineHeight);
            }
            else
            {
                y = editor.GetPositionFromCharIndex(start).Y;
            }

            if (y > Height)
            {
                break;
            }

            if (y + lineHeight >= -20 && y > lastLineTopY)
            {
                DrawNumber(g, font, i + 1, y, lineHeight, fontHeight, rightPad);
                lastLineTopY = y;
            }
        }
    }

    private Font GetNumberFont()
    {
        float size = Math.Max(7.5f, Math.Min(9.75f, fontSizePt - 1.5f));
        string family = editorBaseFont.FontFamily.Name;
        if (numberFont == null || Math.Abs(numberFontSize - size) > 0.01f ||
            !string.Equals(numberFontFamily, family, StringComparison.OrdinalIgnoreCase))
        {
            numberFont?.Dispose();
            numberFont = new Font(editorBaseFont.FontFamily, size, FontStyle.Regular, GraphicsUnit.Point);
            numberFontSize = size;
            numberFontFamily = family;
        }

        return numberFont;
    }

    private void DrawNumber(Graphics g, Font font, int number, int y, float lineHeight, float fontHeight, int rightPad)
    {
        int textY = y + (int)Math.Max(0f, (lineHeight - fontHeight) / 2f);
        var bounds = new Rectangle(0, textY, Width - rightPad, (int)Math.Ceiling(fontHeight) + 2);
        TextRenderer.DrawText(
            g,
            number.ToString(),
            font,
            bounds,
            Theme.LineNumber,
            TextFormatFlags.Right | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
    }

    private int FindLineIndex(int charIndex)
    {
        int index = lineStarts.BinarySearch(charIndex);
        return index >= 0 ? index : Math.Max(0, ~index - 1);
    }

    private void UpdateWidth()
    {
        float scale = DeviceDpi / 96f;
        int digits = Math.Max(2, lineStarts.Count.ToString().Length);
        int width = (int)(Math.Max(46, digits * 8 + 24) * scale);
        if (Width != width)
        {
            Width = width;
        }
    }

    private static int SaneY(int y, int fallback) =>
        y < -10000 || y > 100000 ? fallback : y;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            numberFont?.Dispose();
        }

        base.Dispose(disposing);
    }
}

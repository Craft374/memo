namespace Memo;

public sealed class TabBarControl : Control
{
    public event Action<string>? TabSelected;
    public event Action<string>? TabCloseRequested;
    public event Action? NewTabRequested;
    public event Action<IReadOnlyList<MemoTab>>? TabsReordered;

    private sealed record TabLayout(string Id, Rectangle Bounds, Rectangle CloseBounds);

    private readonly List<TabLayout> layouts = new();
    private List<MemoTab> tabs = new();
    private string? selectedId;
    private string? hoverTabId;
    private bool hoverClose;
    private bool hoverPlus;
    private Rectangle plusBounds;
    private string? draggingTabId;
    private int dragStartX;
    private bool dragMoved;

    public TabBarControl()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw,
            true);
        SetStyle(ControlStyles.Selectable, false);

        Dock = DockStyle.Top;
        Height = 0;
        BackColor = Theme.TabBarBackground;
    }

    public int PreferredHeight => (int)(32 * DeviceDpi / 96f);

    public void UpdateTabs(IReadOnlyList<MemoTab> tabs, string? selectedId)
    {
        this.tabs = tabs.ToList();
        this.selectedId = selectedId;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Theme.TabBarBackground);

        layouts.Clear();
        plusBounds = Rectangle.Empty;

        if (Height < 4 || tabs.Count == 0)
        {
            return;
        }

        float s = DeviceDpi / 96f;
        int height = PreferredHeight;
        using var titleFont = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
        using var borderPen = new Pen(Theme.Border);

        int x = 0;
        foreach (var tab in tabs)
        {
            if (x >= Width)
            {
                break;
            }

            int titleWidth = TextRenderer.MeasureText(g, tab.Title, titleFont, Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width;
            int tabWidth = (int)Math.Min(Math.Max(112 * s, titleWidth + 48 * s), 190 * s);
            var bounds = new Rectangle(x, 0, tabWidth, height);

            bool isSelected = tab.Id == selectedId;
            bool isHover = tab.Id == hoverTabId;
            var fill = isSelected ? Theme.TabSelected : isHover ? Theme.TabHover : Theme.TabNormal;
            using (var brush = new SolidBrush(fill))
            {
                g.FillRectangle(brush, bounds);
            }

            g.DrawLine(borderPen, bounds.Right - 1, 0, bounds.Right - 1, height);

            int closeSize = (int)(18 * s);
            var closeBounds = new Rectangle(
                bounds.Right - closeSize - (int)(8 * s),
                (height - closeSize) / 2,
                closeSize,
                closeSize);

            if (isHover && hoverClose)
            {
                using var closeBrush = new SolidBrush(Theme.CloseHoverBackground);
                var old = g.SmoothingMode;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.FillEllipse(closeBrush, closeBounds);
                g.SmoothingMode = old;
            }

            TextRenderer.DrawText(
                g, "×", titleFont, closeBounds,
                isHover && hoverClose ? Theme.TabText : Theme.CloseGlyph,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            var textBounds = new Rectangle(
                bounds.X + (int)(13 * s),
                0,
                Math.Max(20, closeBounds.Left - bounds.X - (int)(17 * s)),
                height);
            TextRenderer.DrawText(
                g, tab.Title, titleFont, textBounds, Theme.TabText,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);

            layouts.Add(new TabLayout(tab.Id, bounds, closeBounds));
            x += tabWidth;
        }

        // 새 탭 버튼
        int plusSize = (int)(28 * s);
        if (x + plusSize <= Width)
        {
            plusBounds = new Rectangle(x + (int)(4 * s), (height - plusSize) / 2, plusSize, plusSize);
            if (hoverPlus)
            {
                using var plusBrush = new SolidBrush(Theme.TabHover);
                g.FillRectangle(plusBrush, plusBounds);
            }

            using var plusFont = new Font("Segoe UI", 11f, FontStyle.Regular, GraphicsUnit.Point);
            TextRenderer.DrawText(
                g, "+", plusFont, plusBounds,
                hoverPlus ? Theme.TabText : Theme.CloseGlyph,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        g.DrawLine(borderPen, 0, Height - 1, Width, Height - 1);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (draggingTabId != null && e.Button == MouseButtons.Left)
        {
            DragTab(e.X);
        }

        string? newHover = null;
        bool newHoverClose = false;
        foreach (var layout in layouts)
        {
            if (layout.Bounds.Contains(e.Location))
            {
                newHover = layout.Id;
                newHoverClose = InflatedClose(layout.CloseBounds).Contains(e.Location);
                break;
            }
        }

        bool newHoverPlus = plusBounds != Rectangle.Empty && plusBounds.Contains(e.Location);

        if (newHover != hoverTabId || newHoverClose != hoverClose || newHoverPlus != hoverPlus)
        {
            hoverTabId = newHover;
            hoverClose = newHoverClose;
            hoverPlus = newHoverPlus;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (hoverTabId != null || hoverPlus)
        {
            hoverTabId = null;
            hoverClose = false;
            hoverPlus = false;
            Invalidate();
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button == MouseButtons.Left && plusBounds != Rectangle.Empty && plusBounds.Contains(e.Location))
        {
            NewTabRequested?.Invoke();
            return;
        }

        foreach (var layout in layouts)
        {
            if (!layout.Bounds.Contains(e.Location))
            {
                continue;
            }

            if (e.Button == MouseButtons.Middle)
            {
                TabCloseRequested?.Invoke(layout.Id);
            }
            else if (e.Button == MouseButtons.Left)
            {
                if (InflatedClose(layout.CloseBounds).Contains(e.Location))
                {
                    TabCloseRequested?.Invoke(layout.Id);
                }
                else
                {
                    draggingTabId = layout.Id;
                    dragStartX = e.X;
                    dragMoved = false;
                    TabSelected?.Invoke(layout.Id);
                }
            }

            return;
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        EndDrag();
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        EndDrag();
    }

    private void DragTab(int x)
    {
        if (!dragMoved && Math.Abs(x - dragStartX) <= 4 * DeviceDpi / 96f)
        {
            return;
        }

        dragMoved = true;
        int from = tabs.FindIndex(t => t.Id == draggingTabId);
        int to = DropIndex(x);
        if (from < 0 || from == to)
        {
            return;
        }

        var tab = tabs[from];
        tabs.RemoveAt(from);
        tabs.Insert(to, tab);
        Invalidate();
    }

    // 끌고 있는 탭을 뺀 나머지 중 가운데가 커서보다 왼쪽인 탭 수 = 놓일 자리.
    // 폭이 다른 탭끼리 자리가 왔다 갔다 하지 않도록 경계 대신 가운데를 기준으로 한다.
    private int DropIndex(int x)
    {
        int left = 0;
        int index = 0;
        foreach (var tab in tabs)
        {
            int width = layouts.Find(l => l.Id == tab.Id)?.Bounds.Width ?? 0;
            if (tab.Id != draggingTabId && left + width / 2 < x)
            {
                index++;
            }

            left += width;
        }

        return index;
    }

    private void EndDrag()
    {
        if (draggingTabId == null)
        {
            return;
        }

        draggingTabId = null;
        if (dragMoved)
        {
            TabsReordered?.Invoke(tabs.ToList());
        }
    }

    private static Rectangle InflatedClose(Rectangle bounds)
    {
        var r = bounds;
        r.Inflate(3, 3);
        return r;
    }
}

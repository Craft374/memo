using System.Drawing.Drawing2D;

namespace Memo;

internal sealed class DarkColorTable : ProfessionalColorTable
{
    public override Color MenuStripGradientBegin => Theme.MenuBackground;
    public override Color MenuStripGradientEnd => Theme.MenuBackground;
    public override Color MenuItemSelected => Theme.MenuHover;
    public override Color MenuItemSelectedGradientBegin => Theme.MenuHover;
    public override Color MenuItemSelectedGradientEnd => Theme.MenuHover;
    public override Color MenuItemPressedGradientBegin => Theme.MenuDropDownBackground;
    public override Color MenuItemPressedGradientMiddle => Theme.MenuDropDownBackground;
    public override Color MenuItemPressedGradientEnd => Theme.MenuDropDownBackground;
    public override Color MenuItemBorder => Theme.MenuHover;
    public override Color MenuBorder => Theme.MenuBorder;
    public override Color ToolStripDropDownBackground => Theme.MenuDropDownBackground;
    public override Color ImageMarginGradientBegin => Theme.MenuDropDownBackground;
    public override Color ImageMarginGradientMiddle => Theme.MenuDropDownBackground;
    public override Color ImageMarginGradientEnd => Theme.MenuDropDownBackground;
    public override Color SeparatorDark => Theme.MenuBorder;
    public override Color SeparatorLight => Theme.MenuBorder;
}

internal sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
{
    public DarkMenuRenderer() : base(new DarkColorTable())
    {
        RoundedEdges = false;
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? Theme.TabText : Theme.MenuDisabledText;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = Theme.TabText;
        base.OnRenderArrow(e);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        var g = e.Graphics;
        var r = e.ImageRectangle;

        var old = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(Theme.TabText, 1.7f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round,
        };

        float cx = r.X + r.Width / 2f;
        float cy = r.Y + r.Height / 2f;
        g.DrawLines(pen, new[]
        {
            new PointF(cx - 4f, cy),
            new PointF(cx - 1.2f, cy + 3f),
            new PointF(cx + 4.2f, cy - 3.4f),
        });
        g.SmoothingMode = old;
    }
}

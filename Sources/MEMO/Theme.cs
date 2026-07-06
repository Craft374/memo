using System.Drawing.Text;

namespace Memo;

internal static class Theme
{
    // 맥 버전과 동일한 색 (calibrated white/rgb 값을 0-255로 환산)
    public static readonly Color WindowBackground = Color.FromArgb(19, 20, 22);
    public static readonly Color EditorText = Color.FromArgb(235, 235, 235);

    public static readonly Color TabBarBackground = Color.FromArgb(14, 15, 16);
    public static readonly Color TabSelected = Color.FromArgb(26, 27, 29);
    public static readonly Color TabNormal = Color.FromArgb(19, 20, 22);
    public static readonly Color TabHover = Color.FromArgb(23, 24, 27);
    public static readonly Color Border = Color.FromArgb(46, 46, 46);
    public static readonly Color TabText = Color.FromArgb(219, 219, 219);
    public static readonly Color CloseGlyph = Color.FromArgb(153, 153, 153);
    public static readonly Color CloseHoverBackground = Color.FromArgb(52, 54, 58);

    public static readonly Color GutterBackground = Color.FromArgb(14, 15, 17);
    public static readonly Color GutterSeparator = Color.FromArgb(56, 56, 56);
    public static readonly Color LineNumber = Color.FromArgb(133, 133, 133);

    public static readonly Color MenuBackground = Color.FromArgb(14, 15, 16);
    public static readonly Color MenuDropDownBackground = Color.FromArgb(24, 25, 28);
    public static readonly Color MenuHover = Color.FromArgb(38, 40, 44);
    public static readonly Color MenuBorder = Color.FromArgb(58, 60, 64);
    public static readonly Color MenuDisabledText = Color.FromArgb(120, 120, 120);

    // 맥 Menlo 15px ≈ 윈도우 11.5pt (RTF의 half-point 단위와 정확히 맞는 값)
    public const float BaseFontSize = 11.5f;
    public const float MinFontSize = 7.5f;
    public const float MaxFontSize = 25.5f;

    public static readonly string EditorFontFamily = ResolveEditorFontFamily();

    private static string ResolveEditorFontFamily()
    {
        try
        {
            using var installed = new InstalledFontCollection();
            foreach (var family in installed.Families)
            {
                if (family.Name.Equals("Cascadia Mono", StringComparison.OrdinalIgnoreCase))
                {
                    return "Cascadia Mono";
                }
            }
        }
        catch
        {
        }

        return "Consolas";
    }
}

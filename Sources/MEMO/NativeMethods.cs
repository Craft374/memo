using System.Runtime.InteropServices;

namespace Memo;

internal static class NativeMethods
{
    public const int WM_SIZE = 0x0005;
    public const int WM_SETFONT = 0x0030;
    public const int WM_HSCROLL = 0x0114;
    public const int WM_VSCROLL = 0x0115;
    public const int WM_MOUSEWHEEL = 0x020A;
    public const int WM_PASTE = 0x0302;

    public const int EM_SETRECT = 0x00B3;
    public const int EM_GETCHARFORMAT = 0x0400 + 58;
    public const int EM_SETCHARFORMAT = 0x0400 + 68;
    public const int EM_GETZOOM = 0x0400 + 224;
    public const int EM_SETTARGETDEVICE = 0x0400 + 72;
    public const int EM_SETZOOM = 0x0400 + 225;

    public const int SCF_DEFAULT = 0;
    public const int SCF_ALL = 4;

    public const uint CFM_FACE = 0x20000000;
    public const uint CFM_COLOR = 0x40000000;
    public const uint CFM_SIZE = 0x80000000;

    public const int GWL_STYLE = -16;
    public const int WS_HSCROLL = 0x00100000;
    public const int SB_HORZ = 0;

    public const uint SWP_FRAMECHANGED = 0x0020;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_NOACTIVATE = 0x0010;

    public const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19;
    public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    public const int DWMWA_CAPTION_COLOR = 35;

    public static readonly IntPtr HWND_BROADCAST = new(0xFFFF);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct CHARFORMAT2W
    {
        public int cbSize;
        public uint dwMask;
        public uint dwEffects;
        public int yHeight;
        public int yOffset;
        public int crTextColor;
        public byte bCharSet;
        public byte bPitchAndFamily;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szFaceName;
        public ushort wWeight;
        public short sSpacing;
        public int crBackColor;
        public int lcid;
        public uint dwCookie;
        public short sStyle;
        public ushort wKerning;
        public byte bUnderlineType;
        public byte bAnimation;
        public byte bRevAuthor;
        public byte bUnderlineColor;
    }

    [DllImport("user32.dll")]
    public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref RECT lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref CHARFORMAT2W lParam);

    [DllImport("user32.dll")]
    public static extern IntPtr SendMessage(IntPtr hWnd, int msg, ref int wParam, ref int lParam);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int index, IntPtr value);

    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter, int x, int y, int w, int h, uint flags);

    [DllImport("user32.dll")]
    public static extern bool ShowScrollBar(IntPtr hWnd, int bar, bool show);

    [DllImport("user32.dll")]
    public static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int RegisterWindowMessage(string message);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    public static extern int SetWindowTheme(IntPtr hWnd, string? appName, string? idList);

    [DllImport("dwmapi.dll")]
    public static extern int DwmSetWindowAttribute(IntPtr hWnd, int attribute, ref int value, int size);
}

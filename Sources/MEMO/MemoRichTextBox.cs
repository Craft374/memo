namespace Memo;

public sealed class MemoRichTextBox : RichTextBox
{
    public event Action<int>? ZoomStepRequested;
    public event Action? ViewChanged;

    private int wheelRemainder;
    private bool wrapToWindow = true;

    public MemoRichTextBox()
    {
        BorderStyle = BorderStyle.None;
        ScrollBars = RichTextBoxScrollBars.Both;
        // 실제 줄바꿈은 EM_SETTARGETDEVICE로 제어 (핸들 재생성 없이 토글하기 위함)
        WordWrap = false;
        AcceptsTab = true;
        HideSelection = false;
        DetectUrls = false;
        RichTextShortcutsEnabled = false;
        AutoWordSelection = false;
        MaxLength = int.MaxValue;
        BackColor = Theme.WindowBackground;
        ForeColor = Theme.EditorText;
    }

    public bool WrapToWindow
    {
        get => wrapToWindow;
        set
        {
            wrapToWindow = value;
            if (IsHandleCreated)
            {
                ApplyWrap();
            }
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        NativeMethods.SetWindowTheme(Handle, "DarkMode_Explorer", null);
        ApplyDefaultCharFormat();
        ApplyWrap();
        ApplyInsets();
    }

    // 글자 크기를 트윕(twip) 단위로 직접 지정해 DPI 이중 스케일링을 피한다.
    // 문서 전체와 기본 서식을 항상 BaseFontSize로 고정 (표시 크기는 EM_SETZOOM 배율로만 변경)
    public void ApplyDefaultCharFormat()
    {
        var format = new NativeMethods.CHARFORMAT2W
        {
            dwMask = NativeMethods.CFM_FACE | NativeMethods.CFM_SIZE | NativeMethods.CFM_COLOR,
            yHeight = (int)(Theme.BaseFontSize * 20),
            crTextColor = (Theme.EditorText.B << 16) | (Theme.EditorText.G << 8) | Theme.EditorText.R,
            szFaceName = Theme.EditorFontFamily,
        };
        format.cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.CHARFORMAT2W>();

        NativeMethods.SendMessage(Handle, NativeMethods.EM_SETCHARFORMAT, (IntPtr)NativeMethods.SCF_DEFAULT, ref format);
        NativeMethods.SendMessage(Handle, NativeMethods.EM_SETCHARFORMAT, (IntPtr)NativeMethods.SCF_ALL, ref format);
    }

    protected override void OnClientSizeChanged(EventArgs e)
    {
        base.OnClientSizeChanged(e);
        if (IsHandleCreated)
        {
            ApplyInsets();
        }

        ViewChanged?.Invoke();
    }

    public void ApplyWrap()
    {
        // 줄바꿈 켤 때 WS_HSCROLL을 제거하지 않으면 가로 스크롤바가 남는다
        long style = (long)NativeMethods.GetWindowLongPtr(Handle, NativeMethods.GWL_STYLE);
        long newStyle = wrapToWindow ? style & ~(long)NativeMethods.WS_HSCROLL : style | NativeMethods.WS_HSCROLL;
        if (newStyle != style)
        {
            NativeMethods.SetWindowLongPtr(Handle, NativeMethods.GWL_STYLE, (IntPtr)newStyle);
            NativeMethods.SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0,
                NativeMethods.SWP_FRAMECHANGED | NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE |
                NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
        }

        if (wrapToWindow)
        {
            NativeMethods.ShowScrollBar(Handle, NativeMethods.SB_HORZ, false);
        }

        // lineWidth 0 = 창 폭에 맞춰 줄바꿈, 1 = 줄바꿈 없음
        NativeMethods.SendMessage(Handle, NativeMethods.EM_SETTARGETDEVICE, IntPtr.Zero,
            wrapToWindow ? IntPtr.Zero : (IntPtr)1);
    }

    public void ApplyInsets()
    {
        float s = DeviceDpi / 96f;
        int side = (int)(12 * s);
        int top = (int)(10 * s);
        int bottom = (int)(6 * s);

        var rect = new NativeMethods.RECT
        {
            Left = side,
            Top = top,
            Right = Math.Max(side + 1, ClientSize.Width - side),
            Bottom = Math.Max(top + 1, ClientSize.Height - bottom),
        };
        NativeMethods.SendMessage(Handle, NativeMethods.EM_SETRECT, IntPtr.Zero, ref rect);
    }

    public void SetZoomRatio(float ratio)
    {
        int numerator = Math.Max(16, (int)Math.Round(ratio * 1000f));
        NativeMethods.SendMessage(Handle, NativeMethods.EM_SETZOOM, (IntPtr)numerator, (IntPtr)1000);
        ViewChanged?.Invoke();
    }

    public string GetDiagnostics()
    {
        var format = new NativeMethods.CHARFORMAT2W
        {
            dwMask = NativeMethods.CFM_FACE | NativeMethods.CFM_SIZE | NativeMethods.CFM_COLOR,
        };
        format.cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.CHARFORMAT2W>();
        NativeMethods.SendMessage(Handle, NativeMethods.EM_GETCHARFORMAT, (IntPtr)NativeMethods.SCF_DEFAULT, ref format);

        var selection = new NativeMethods.CHARFORMAT2W
        {
            dwMask = NativeMethods.CFM_FACE | NativeMethods.CFM_SIZE | NativeMethods.CFM_COLOR,
        };
        selection.cbSize = format.cbSize;
        NativeMethods.SendMessage(Handle, NativeMethods.EM_GETCHARFORMAT, (IntPtr)1, ref selection);

        int numerator = 0, denominator = 0;
        NativeMethods.SendMessage(Handle, NativeMethods.EM_GETZOOM, ref numerator, ref denominator);

        return $"dpi={DeviceDpi} zoom={numerator}/{denominator} " +
            $"default: yHeight={format.yHeight}tw ({format.yHeight / 20f}pt) face={format.szFaceName} | " +
            $"selection: yHeight={selection.yHeight}tw ({selection.yHeight / 20f}pt) face={selection.szFaceName} | " +
            $"font={Font.FontFamily.Name} {Font.Size}{Font.Unit} zoomFactorProp={ZoomFactor}";
    }

    public void PastePlainText()
    {
        string? text = null;
        try
        {
            if (Clipboard.ContainsText())
            {
                text = Clipboard.GetText();
            }
        }
        catch
        {
        }

        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        SelectedText = TextSanitizer.Sanitize(text);
    }

    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case NativeMethods.WM_PASTE:
                PastePlainText();
                return;

            case NativeMethods.WM_MOUSEWHEEL when (ModifierKeys & Keys.Control) == Keys.Control:
                short delta = unchecked((short)(((long)m.WParam >> 16) & 0xFFFF));
                HandleZoomWheel(delta);
                m.Result = IntPtr.Zero;
                return;
        }

        base.WndProc(ref m);

        switch (m.Msg)
        {
            case NativeMethods.WM_VSCROLL:
            case NativeMethods.WM_HSCROLL:
            case NativeMethods.WM_MOUSEWHEEL:
            case NativeMethods.WM_SIZE:
                ViewChanged?.Invoke();
                break;

            case NativeMethods.WM_SETFONT:
                // WM_SETFONT가 기본 서식을 덮어쓰므로 다시 고정
                ApplyDefaultCharFormat();
                break;
        }
    }

    private void HandleZoomWheel(int delta)
    {
        wheelRemainder += delta;
        while (Math.Abs(wheelRemainder) >= 120)
        {
            int step = wheelRemainder > 0 ? 1 : -1;
            ZoomStepRequested?.Invoke(step);
            wheelRemainder -= step * 120;
        }
    }
}

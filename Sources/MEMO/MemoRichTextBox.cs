namespace Memo;

public sealed class MemoRichTextBox : RichTextBox
{
    private const int TabStopTwips = 360;

    public event Action<int>? ZoomStepRequested;
    public event Action? ViewChanged;
    public event Action? ImeCompositionEnded;

    private int wheelRemainder;
    private bool wrapToWindow = true;
    private bool viewChangePending;
    private int? characterSelectionAnchor;
    private int characterSelectionEnd;
    private List<(int Start, int Length)> ruleRanges = new();

    public bool IsImeComposing { get; private set; }

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
        ApplyDefaultParagraphFormat();
        ApplyWrap();
        ApplyInsets();
    }

    // 글자 크기를 트윕(twip) 단위로 직접 지정해 DPI 이중 스케일링을 피한다.
    // 문서 전체와 기본 서식을 기본 글꼴 크기로 고정하고, 표시 크기는 EM_SETZOOM 배율로만 변경한다.
    public void ApplyDefaultCharFormat()
    {
        var format = new NativeMethods.CHARFORMAT2W
        {
            dwMask = NativeMethods.CFM_FACE | NativeMethods.CFM_SIZE | NativeMethods.CFM_COLOR,
            yHeight = (int)Math.Round(Font.SizeInPoints * 20f),
            crTextColor = (Theme.EditorText.B << 16) | (Theme.EditorText.G << 8) | Theme.EditorText.R,
            szFaceName = Font.FontFamily.Name,
        };
        format.cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.CHARFORMAT2W>();

        NativeMethods.SendMessage(Handle, NativeMethods.EM_SETCHARFORMAT, (IntPtr)NativeMethods.SCF_DEFAULT, ref format);
        NativeMethods.SendMessage(Handle, NativeMethods.EM_SETCHARFORMAT, (IntPtr)NativeMethods.SCF_ALL, ref format);
    }

    public void ApplyDefaultParagraphFormat()
    {
        int selectionStart = SelectionStart;
        int selectionLength = SelectionLength;
        var tabStops = new int[32];
        for (int i = 0; i < tabStops.Length; i++)
        {
            tabStops[i] = (i + 1) * TabStopTwips;
        }

        SelectAll();
        var format = new NativeMethods.PARAFORMAT2
        {
            cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.PARAFORMAT2>(),
            dwMask = NativeMethods.PFM_LINESPACING | NativeMethods.PFM_TABSTOPS,
            cTabCount = (short)tabStops.Length,
            rgxTabs = tabStops,
            dyLineSpacing = GetLineSpacingTwips(Font),
            bLineSpacingRule = 4,
        };
        NativeMethods.SendMessage(Handle, NativeMethods.EM_SETPARAFORMAT, IntPtr.Zero, ref format);
        Select(selectionStart, selectionLength);
    }

    internal static int GetLineSpacingTwips(Font font) =>
        Math.Max(1, (int)Math.Ceiling(font.GetHeight(96f) * 15f));

    internal static float GetLineHeightPixels(Font font, float dpi, float zoomRatio) =>
        GetLineSpacingTwips(font) * dpi * zoomRatio / 1440f;

    protected override void OnClientSizeChanged(EventArgs e)
    {
        base.OnClientSizeChanged(e);
        if (IsHandleCreated)
        {
            ApplyInsets();
        }

        QueueViewChanged();
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
        QueueViewChanged();
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

    // 다른 앱에는 서식 없는 텍스트만 넘긴다 (맥 버전과 동일)
    public bool CopyPlainText()
    {
        string text = SelectedText;
        if (text.Length == 0)
        {
            return false;
        }

        try
        {
            Clipboard.SetText(text.ReplaceLineEndings("\r\n"));
            return true;
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            return false;
        }
    }

    public void CutPlainText()
    {
        // 클립보드에 못 넣었으면 지우지 않는다
        if (CopyPlainText())
        {
            SelectedText = "";
        }
    }

    internal void ApplyEdit(TextEdit? edit)
    {
        if (edit == null)
        {
            return;
        }

        Select(edit.Start, edit.Length);
        SelectedText = edit.Replacement;
        if (edit.SelectionStart is int start)
        {
            Select(start, edit.SelectionLength);
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (IsImeComposing)
        {
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // RichEdit는 Ctrl+C/X/V를 WM_COPY 등을 거치지 않고 직접 처리하므로 여기서 가로챈다.
        if (keyData is (Keys.Control | Keys.C) or (Keys.Control | Keys.Insert))
        {
            CopyPlainText();
            return true;
        }

        if (keyData is (Keys.Control | Keys.X) or (Keys.Shift | Keys.Delete) && SelectionLength > 0)
        {
            CutPlainText();
            return true;
        }

        if (keyData is (Keys.Control | Keys.V) or (Keys.Shift | Keys.Insert))
        {
            PastePlainText();
            return true;
        }

        if (keyData is Keys.Tab or (Keys.Shift | Keys.Tab))
        {
            ApplyEdit(MemoTextLogic.IndentationEdit(Text, SelectionStart, SelectionLength, outdent: keyData != Keys.Tab));
            return true;
        }

        if (keyData == Keys.Enter && SelectionLength == 0 &&
            MemoTextLogic.ListEdit(Text, SelectionStart) is { } edit)
        {
            ApplyEdit(edit);
            return true;
        }

        if (keyData == Keys.Down && SelectionLength == 0 && IsCaretOnLastPhysicalLine())
        {
            // 마지막 실제 줄에는 다음 줄이 없으므로 커서만 문서 끝으로 옮긴다.
            Select(TextLength, 0);
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case NativeMethods.WM_LBUTTONDOWN
                when ((long)m.WParam & NativeMethods.MK_SHIFT) != 0:
                HandleShiftMouseSelection(ref m);
                return;

            case NativeMethods.WM_LBUTTONDOWN:
                characterSelectionAnchor = null;
                break;

            case NativeMethods.WM_MOUSEMOVE
                when characterSelectionAnchor is not null &&
                     ((long)m.WParam & (NativeMethods.MK_LBUTTON | NativeMethods.MK_SHIFT)) ==
                     (NativeMethods.MK_LBUTTON | NativeMethods.MK_SHIFT):
                HandleShiftMouseSelection(ref m);
                return;

            case NativeMethods.WM_PASTE:
                PastePlainText();
                return;

            case NativeMethods.WM_COPY:
                CopyPlainText();
                return;

            case NativeMethods.WM_CUT:
                CutPlainText();
                return;

            case NativeMethods.WM_PAINT:
                base.WndProc(ref m);
                DrawHorizontalRules();
                return;

            case NativeMethods.WM_IME_STARTCOMPOSITION:
                IsImeComposing = true;
                break;

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
                QueueViewChanged();
                break;

            case NativeMethods.WM_IME_ENDCOMPOSITION:
                CompleteImeComposition();
                break;
        }
    }

    protected override void OnTextChanged(EventArgs e)
    {
        var ranges = MemoTextLogic.HorizontalRuleLineRanges(Text);
        // 줄이 구분선이 되거나 풀릴 때 RichEdit는 바뀐 글자 쪽만 다시 그릴 수 있어 전체를 다시 그린다
        if (ranges.Count != ruleRanges.Count)
        {
            Invalidate();
        }

        ruleRanges = ranges;
        base.OnTextChanged(e);
    }

    // ___ 처럼 밑줄 3개 이상으로만 된 줄은 RichEdit가 그린 밑줄을 배경색으로 덮고 가운데에 가로선을 긋는다.
    // 텍스트는 그대로라 저장/복사는 영향 없음. 선택 영역이 걸친 줄은 실제 글자를 보여준다.
    private void DrawHorizontalRules()
    {
        if (ruleRanges.Count == 0)
        {
            return;
        }

        var formatRect = new NativeMethods.RECT();
        NativeMethods.SendMessage(Handle, NativeMethods.EM_GETRECT, IntPtr.Zero, ref formatRect);
        int lineHeight = (int)Math.Round(GetLineHeightPixels(Font, DeviceDpi, ZoomFactor));
        int selectionStart = SelectionStart;
        int selectionEnd = selectionStart + SelectionLength;
        var client = ClientRectangle;

        // 깜빡이는 캐럿(XOR) 위에 덧그리면 잔상이 남으므로 잠시 숨긴다
        NativeMethods.HideCaret(Handle);
        try
        {
            using var g = Graphics.FromHwnd(Handle);
            using var background = new SolidBrush(BackColor);
            using var pen = new Pen(Theme.HorizontalRule, Math.Max(1, DeviceDpi / 96));
            foreach (var (start, length) in ruleRanges)
            {
                if (selectionEnd > selectionStart && selectionStart < start + length && selectionEnd > start)
                {
                    continue;
                }

                int top = GetPositionFromCharIndex(start).Y;
                int bottom = GetPositionFromCharIndex(start + length - 1).Y + lineHeight;
                // 위치 값이 엉뚱하면 본문 전체를 배경색으로 덮을 수 있으므로 건너뛴다
                if (bottom <= top || bottom - top > client.Height)
                {
                    continue;
                }

                var fill = Rectangle.Intersect(client, new Rectangle(0, top, client.Width, bottom - top));
                if (fill.IsEmpty)
                {
                    continue;
                }

                g.FillRectangle(background, fill);
                int y = top + lineHeight / 2;
                g.DrawLine(pen, formatRect.Left, y, formatRect.Right, y);
            }
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            // 장식용 덧그리기라 GDI+ 실패는 무시하고 본문 표시는 유지
        }
        finally
        {
            NativeMethods.ShowCaret(Handle);
        }
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        CompleteImeComposition();
    }

    protected override void OnVScroll(EventArgs e)
    {
        base.OnVScroll(e);
        QueueViewChanged();
    }

    private void HandleZoomWheel(int delta)
    {
        wheelRemainder += delta;
        int steps = wheelRemainder / 120;
        if (steps == 0)
        {
            return;
        }

        wheelRemainder -= steps * 120;
        ZoomStepRequested?.Invoke(steps);
    }

    private void QueueViewChanged()
    {
        if (viewChangePending || !IsHandleCreated || IsDisposed || Disposing)
        {
            return;
        }

        viewChangePending = true;
        try
        {
            BeginInvoke((Action)RaisePendingViewChanged);
        }
        catch (InvalidOperationException)
        {
            viewChangePending = false;
        }
    }

    private void RaisePendingViewChanged()
    {
        viewChangePending = false;
        if (!IsDisposed && !Disposing)
        {
            ViewChanged?.Invoke();
        }
    }

    private void CompleteImeComposition()
    {
        if (!IsImeComposing)
        {
            return;
        }

        IsImeComposing = false;
        ImeCompositionEnded?.Invoke();
    }

    private void HandleShiftMouseSelection(ref Message message)
    {
        int selectionEnd = GetCharacterIndexFromMousePosition(message.LParam);
        int selectionAnchor = ResolveCharacterSelectionAnchor(selectionEnd);

        base.WndProc(ref message);

        characterSelectionAnchor = selectionAnchor;
        characterSelectionEnd = selectionEnd;
        NativeMethods.SendMessage(Handle, NativeMethods.EM_SETSEL,
            (IntPtr)selectionAnchor, (IntPtr)selectionEnd);
    }

    private int ResolveCharacterSelectionAnchor(int target)
    {
        int selectionStart = SelectionStart;
        int selectionEnd = selectionStart + SelectionLength;

        if (characterSelectionAnchor is int anchor &&
            Math.Min(anchor, characterSelectionEnd) == selectionStart &&
            Math.Max(anchor, characterSelectionEnd) == selectionEnd)
        {
            return anchor;
        }

        if (SelectionLength == 0 || target >= selectionEnd)
        {
            return selectionStart;
        }

        if (target <= selectionStart)
        {
            return selectionEnd;
        }

        return target - selectionStart <= selectionEnd - target
            ? selectionEnd
            : selectionStart;
    }

    private int GetCharacterIndexFromMousePosition(IntPtr lParam)
    {
        long packedPosition = (long)lParam;
        var point = new Point(
            unchecked((short)(packedPosition & 0xFFFF)),
            unchecked((short)((packedPosition >> 16) & 0xFFFF)));

        int index = GetCharIndexFromPosition(point);
        if (TextLength == 0 || index != TextLength - 1)
        {
            return index;
        }

        Point lastCharacterPosition = GetPositionFromCharIndex(index);
        Point documentEndPosition = GetPositionFromCharIndex(TextLength);
        if (lastCharacterPosition.Y != documentEndPosition.Y &&
            point.Y >= documentEndPosition.Y)
        {
            return TextLength;
        }

        if (lastCharacterPosition.Y == documentEndPosition.Y)
        {
            int midpoint = lastCharacterPosition.X +
                (documentEndPosition.X - lastCharacterPosition.X) / 2;
            bool isPastMidpoint = documentEndPosition.X >= lastCharacterPosition.X
                ? point.X >= midpoint
                : point.X <= midpoint;
            if (isPastMidpoint)
            {
                return TextLength;
            }
        }

        float lineHeight = GetLineHeightPixels(Font, DeviceDpi, ZoomFactor);
        return point.Y >= documentEndPosition.Y + lineHeight ? TextLength : index;
    }

    private bool IsCaretOnLastPhysicalLine() =>
        GetLineFromCharIndex(SelectionStart) >= GetLineFromCharIndex(TextLength);
}

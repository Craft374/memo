namespace Memo;

// 맥 버전 검색/바꾸기 창과 같은 구성.
// 결과: OK = 다음 찾기(검색) / 하나 바꾸기(바꾸기), Yes = 모두 바꾸기, Cancel = 취소
internal sealed class FindDialog : Form
{
    private readonly TextBox patternBox = NewTextBox("찾을 내용");
    private readonly TextBox replacementBox = NewTextBox("바꿀 내용");
    private readonly CheckBox regexBox = new()
    {
        Text = "정규식",
        AutoSize = true,
        FlatStyle = FlatStyle.Flat,
        Margin = new Padding(0, 2, 0, 14),
    };

    public string Pattern => patternBox.Text;
    public string Replacement => replacementBox.Text;
    public bool UseRegex => regexBox.Checked;

    public FindDialog(bool replace, string pattern, bool useRegex)
    {
        Text = replace ? "바꾸기" : "검색";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = Theme.WindowBackground;
        ForeColor = Theme.EditorText;

        var layout = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(20, 16, 20, 16),
        };
        layout.Controls.Add(new Label
        {
            Text = replace ? "정규식에서는 $1 형식으로 그룹을 사용할 수 있습니다." : "일반 검색과 정규식을 사용할 수 있습니다.",
            AutoSize = true,
            ForeColor = Theme.CloseGlyph,
            Margin = new Padding(0, 0, 0, 10),
        });

        patternBox.Width = replacementBox.Width = LogicalToDeviceUnits(340);
        patternBox.Text = pattern;
        layout.Controls.Add(patternBox);
        if (replace)
        {
            layout.Controls.Add(replacementBox);
        }

        regexBox.Checked = useRegex;
        layout.Controls.Add(regexBox);

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Anchor = AnchorStyles.Right,
            Margin = Padding.Empty,
        };
        var cancelButton = FontSettingsDialog.NewButton("취소", DialogResult.Cancel);
        var acceptButton = FontSettingsDialog.NewButton(replace ? "하나 바꾸기" : "다음 찾기", DialogResult.OK);
        buttons.Controls.Add(cancelButton);
        if (replace)
        {
            buttons.Controls.Add(FontSettingsDialog.NewButton("모두 바꾸기", DialogResult.Yes));
        }

        buttons.Controls.Add(acceptButton);
        layout.Controls.Add(buttons);
        Controls.Add(layout);

        AcceptButton = acceptButton;
        CancelButton = cancelButton;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        NativeMethods.UseDarkTitleBar(Handle);
    }

    private static TextBox NewTextBox(string placeholder) => new()
    {
        PlaceholderText = placeholder,
        BorderStyle = BorderStyle.FixedSingle,
        BackColor = Theme.MenuDropDownBackground,
        ForeColor = Theme.EditorText,
        Margin = new Padding(0, 0, 0, 8),
    };
}

namespace Memo;

internal sealed class FontSettingsDialog : Form
{
    private readonly ComboBox fontFamilyBox = new();
    private readonly Label previewLabel = new();
    private Font? previewFont;

    public string SelectedFontFamily => fontFamilyBox.SelectedItem as string ?? "";

    public FontSettingsDialog(string currentFontFamily)
    {
        Text = "글꼴 설정";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        AutoScaleMode = AutoScaleMode.Dpi;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(620, 380);
        ClientSize = new Size(620, 380);
        BackColor = Theme.WindowBackground;
        ForeColor = Theme.EditorText;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize,
            Padding = new Padding(28, 24, 28, 20),
            BackColor = Theme.WindowBackground,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var title = new Label
        {
            Text = "편집기 글꼴",
            AutoSize = true,
            Font = new Font(SystemFonts.MessageBoxFont!, FontStyle.Bold),
            ForeColor = Theme.TabText,
            Margin = new Padding(0, 0, 0, 10),
        };

        fontFamilyBox.DropDownStyle = ComboBoxStyle.DropDownList;
        fontFamilyBox.FlatStyle = FlatStyle.Flat;
        fontFamilyBox.BackColor = Theme.MenuDropDownBackground;
        fontFamilyBox.ForeColor = Theme.EditorText;
        fontFamilyBox.AutoSize = false;
        fontFamilyBox.Dock = DockStyle.Fill;
        fontFamilyBox.MinimumSize = new Size(0, 38);
        fontFamilyBox.Font = new Font("Segoe UI", 12f, FontStyle.Regular, GraphicsUnit.Point);
        fontFamilyBox.Margin = new Padding(0, 0, 0, 10);

        foreach (string family in Theme.GetInstalledFontFamilies())
        {
            fontFamilyBox.Items.Add(family);
        }

        int selectedIndex = fontFamilyBox.FindStringExact(currentFontFamily);
        if (selectedIndex >= 0)
        {
            fontFamilyBox.SelectedIndex = selectedIndex;
        }
        else if (fontFamilyBox.Items.Count > 0)
        {
            fontFamilyBox.SelectedIndex = 0;
        }

        var previewPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.MenuDropDownBackground,
            Margin = new Padding(0, 0, 0, 14),
            Padding = new Padding(14, 8, 14, 8),
        };
        previewLabel.Dock = DockStyle.Fill;
        previewLabel.AutoEllipsis = true;
        previewLabel.Text = "가나다라마바사  ABC 123";
        previewLabel.TextAlign = ContentAlignment.MiddleCenter;
        previewLabel.ForeColor = Theme.EditorText;
        previewPanel.Controls.Add(previewLabel);
        previewLabel.Text = "가나다 ABC 123";

        var hint = new Label
        {
            Text = "글꼴은 모든 탭에 적용됩니다.  Ctrl+휠은 빠르게, Ctrl++ / Ctrl+-는 0.25pt 단위로 조절합니다.",
            AutoSize = true,
            ForeColor = Theme.CloseGlyph,
            Margin = new Padding(0, 0, 0, 14),
        };

        hint.AutoSize = false;
        hint.Dock = DockStyle.Fill;
        hint.Text = "글꼴은 모든 탭에 적용됩니다.\r\nCtrl+휠은 빠르게, Ctrl++ / Ctrl+-는 0.25pt 단위입니다.";
        hint.TextAlign = ContentAlignment.MiddleLeft;

        var buttonPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Margin = Padding.Empty,
            Padding = new Padding(0, 6, 0, 0),
        };
        var applyButton = NewButton("적용", DialogResult.OK);
        var cancelButton = NewButton("취소", DialogResult.Cancel);
        buttonPanel.Controls.Add(cancelButton);
        buttonPanel.Controls.Add(applyButton);

        layout.Controls.Add(title, 0, 0);
        layout.Controls.Add(fontFamilyBox, 0, 1);
        layout.Controls.Add(previewPanel, 0, 2);
        layout.Controls.Add(hint, 0, 3);
        layout.Controls.Add(buttonPanel, 0, 4);
        Controls.Add(layout);

        fontFamilyBox.SelectedIndexChanged += (_, _) => UpdatePreview();
        UpdatePreview();
        AcceptButton = applyButton;
        CancelButton = cancelButton;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        int enabled = 1;
        if (NativeMethods.DwmSetWindowAttribute(Handle, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref enabled, sizeof(int)) != 0)
        {
            NativeMethods.DwmSetWindowAttribute(Handle, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref enabled, sizeof(int));
        }

        int caption = (Theme.WindowBackground.B << 16) | (Theme.WindowBackground.G << 8) | Theme.WindowBackground.R;
        NativeMethods.DwmSetWindowAttribute(Handle, NativeMethods.DWMWA_CAPTION_COLOR, ref caption, sizeof(int));
    }

    private void UpdatePreview()
    {
        previewFont?.Dispose();
        previewFont = null;

        try
        {
            previewFont = new Font(SelectedFontFamily, 18f, FontStyle.Regular, GraphicsUnit.Point);
            previewLabel.Font = previewFont;
        }
        catch
        {
            previewLabel.Font = Font;
        }
    }

    private static Button NewButton(string text, DialogResult result) => new()
    {
        Text = text,
        DialogResult = result,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        MinimumSize = new Size(88, 34),
        Margin = new Padding(8, 0, 0, 0),
        FlatStyle = FlatStyle.Flat,
        BackColor = Theme.MenuDropDownBackground,
        ForeColor = Theme.EditorText,
        FlatAppearance = { BorderColor = Theme.MenuBorder },
    };

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            previewFont?.Dispose();
        }

        base.Dispose(disposing);
    }
}

using System.Diagnostics;
using System.Reflection;

namespace Memo;

public sealed class MainForm : Form
{
    private readonly MemoStorage storage = new();
    private readonly TextBox titleEditor = new();
    private readonly MemoRichTextBox editor = new();
    private readonly LineNumberGutter gutter;
    private readonly TabBarControl tabBar = new();
    private readonly MenuStrip menu = new();
    private readonly System.Windows.Forms.Timer autosaveTimer = new() { Interval = 350 };
    private readonly System.Windows.Forms.Timer tabBarAnimTimer = new() { Interval = 10 };
    private readonly Stopwatch tabBarAnimClock = new();
    private Font editorBaseFont;

    private const float KeyboardFontSizeStep = 0.25f;
    private const float MouseWheelFontSizeStep = 1f;

    private readonly List<MemoTab> tabs = new();
    private string? selectedTabId;
    private MemoSettings settings;
    private float fontSize;
    private bool isLoadingTab;
    private bool editorTextChangePending;
    private bool autosaveAfterIme;
    private int tabBarAnimFrom;
    private int tabBarAnimTo;

    private ToolStripMenuItem wrapMenuItem = null!;
    private ToolStripMenuItem undoMenuItem = null!;
    private ToolStripMenuItem redoMenuItem = null!;
    private ToolStripMenuItem cutMenuItem = null!;
    private ToolStripMenuItem copyMenuItem = null!;
    private ToolStripMenuItem pasteMenuItem = null!;

    public MainForm()
    {
        settings = storage.LoadSettings();
        fontSize = ClampFontSize(settings.FontSize > 0 ? settings.FontSize : Theme.BaseFontSize);
        editorBaseFont = new Font(Theme.ResolveEditorFontFamily(settings.FontFamily), Theme.BaseFontSize,
            FontStyle.Regular, GraphicsUnit.Point);

        Text = "MEMO";
        BackColor = Theme.WindowBackground;
        ForeColor = Theme.EditorText;
        DoubleBuffered = true;
        Icon = LoadAppIcon();

        ConfigureTitleEditor();
        ConfigureEditor();
        gutter = new LineNumberGutter(editor, editorBaseFont);
        BuildMenu();
        BuildLayout();

        autosaveTimer.Tick += (_, _) =>
        {
            autosaveTimer.Stop();
            if (editor.IsImeComposing)
            {
                autosaveAfterIme = true;
                return;
            }

            SaveNow(showError: false);
        };
        tabBarAnimTimer.Tick += (_, _) => OnTabBarAnimTick();

        ApplyWindowPlacement();
        LoadSession();
    }

    private float ZoomRatio => fontSize / Theme.BaseFontSize;

    private MemoTab? CurrentTab => selectedTabId == null ? null : tabs.FirstOrDefault(t => t.Id == selectedTabId);

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        int enabled = 1;
        if (NativeMethods.DwmSetWindowAttribute(Handle, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref enabled, sizeof(int)) != 0)
        {
            NativeMethods.DwmSetWindowAttribute(Handle, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref enabled, sizeof(int));
        }

        // 타이틀바를 본문과 같은 색으로 (Windows 11)
        int caption = (Theme.WindowBackground.B << 16) | (Theme.WindowBackground.G << 8) | Theme.WindowBackground.R;
        NativeMethods.DwmSetWindowAttribute(Handle, NativeMethods.DWMWA_CAPTION_COLOR, ref caption, sizeof(int));
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        editor.Focus();

        if (Environment.GetEnvironmentVariable("MEMO_DIAG") == "1")
        {
            try
            {
                File.WriteAllText(Path.Combine(Path.GetTempPath(), "memo_diag.txt"), editor.GetDiagnostics());
            }
            catch
            {
            }
        }
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        if (!isLoadingTab && IsHandleCreated)
        {
            SaveNow(showError: false);
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        autosaveTimer.Stop();
        SaveNow(showError: false);
        base.OnFormClosing(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Program.ActivateMessage)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                WindowState = FormWindowState.Normal;
            }

            Activate();
            NativeMethods.SetForegroundWindow(Handle);
            return;
        }

        base.WndProc(ref m);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Control | Keys.Tab:
            case Keys.Control | Keys.PageDown:
            case Keys.Control | Keys.OemCloseBrackets:
                SelectAdjacentTab(1);
                return true;

            case Keys.Control | Keys.Shift | Keys.Tab:
            case Keys.Control | Keys.PageUp:
            case Keys.Control | Keys.OemOpenBrackets:
                SelectAdjacentTab(-1);
                return true;

            case Keys.Control | Keys.D:
                CreateNewTab();
                return true;

            case Keys.Control | Keys.Add:
                ChangeFontSize(KeyboardFontSizeStep);
                return true;

            case Keys.Control | Keys.Subtract:
                ChangeFontSize(-KeyboardFontSizeStep);
                return true;

            case Keys.Control | Keys.NumPad0:
                ResetFontSize();
                return true;

            case Keys.Control | Keys.Shift | Keys.Z:
                if (editor.CanRedo)
                {
                    editor.Redo();
                }

                return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void ConfigureEditor()
    {
        editor.Dock = DockStyle.Fill;
        editor.Font = editorBaseFont;
        editor.WrapToWindow = settings.LineWrapEnabled;

        editor.TextChanged += (_, _) => OnEditorTextChanged();
        editor.ImeCompositionEnded += OnImeCompositionEnded;
        editor.ViewChanged += () => gutter?.Invalidate();
        editor.ZoomStepRequested += steps => ChangeFontSize(steps * MouseWheelFontSizeStep);

        var context = new ContextMenuStrip
        {
            Renderer = new DarkMenuRenderer(),
            BackColor = Theme.MenuDropDownBackground,
            ForeColor = Theme.TabText,
            ShowImageMargin = true,
        };
        context.Items.Add(NewItem("실행 취소", null, (_, _) => { if (editor.CanUndo) editor.Undo(); }));
        context.Items.Add(new ToolStripSeparator());
        context.Items.Add(NewItem("오려두기", null, (_, _) => editor.Cut()));
        context.Items.Add(NewItem("복사", null, (_, _) => editor.Copy()));
        context.Items.Add(NewItem("붙여넣기", null, (_, _) => editor.PastePlainText()));
        context.Items.Add(new ToolStripSeparator());
        context.Items.Add(NewItem("전체 선택", null, (_, _) => editor.SelectAll()));
        editor.ContextMenuStrip = context;
    }

    private void ConfigureTitleEditor()
    {
        titleEditor.BorderStyle = BorderStyle.None;
        titleEditor.AutoSize = false;
        titleEditor.Dock = DockStyle.Fill;
        titleEditor.BackColor = Theme.WindowBackground;
        titleEditor.ForeColor = Theme.EditorText;
        titleEditor.Font = new Font("Segoe UI", 18f, FontStyle.Bold, GraphicsUnit.Point);
        titleEditor.TextChanged += (_, _) => OnTitleTextChanged();
    }

    private void BuildLayout()
    {
        var content = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.WindowBackground,
        };
        float scale = DeviceDpi / 96f;
        var titleArea = new Panel
        {
            Dock = DockStyle.Top,
            Height = (int)Math.Round(74 * scale),
            BackColor = Theme.WindowBackground,
            Padding = new Padding((int)Math.Round(58 * scale), (int)Math.Round(16 * scale),
                (int)Math.Round(24 * scale), (int)Math.Round(12 * scale)),
        };
        titleArea.Controls.Add(titleEditor);

        var bodyArea = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.WindowBackground,
        };
        bodyArea.Controls.Add(editor);
        bodyArea.Controls.Add(gutter);

        content.Controls.Add(bodyArea);
        content.Controls.Add(titleArea);

        tabBar.TabSelected += SelectTab;
        tabBar.TabCloseRequested += CloseTab;
        tabBar.NewTabRequested += CreateNewTab;

        Controls.Add(content);
        Controls.Add(tabBar);
        Controls.Add(menu);
        MainMenuStrip = menu;
    }

    private void BuildMenu()
    {
        menu.Renderer = new DarkMenuRenderer();
        menu.BackColor = Theme.MenuBackground;
        menu.ForeColor = Theme.TabText;
        menu.Padding = new Padding(6, 3, 0, 3);

        var fileMenu = new ToolStripMenuItem("파일(&F)");
        fileMenu.DropDownItems.Add(NewItem("지금 저장", Keys.Control | Keys.S, (_, _) => SaveNow(showError: true)));
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        var exitItem = NewItem("종료", null, (_, _) => Close());
        exitItem.ShortcutKeyDisplayString = "Alt+F4";
        fileMenu.DropDownItems.Add(exitItem);

        var editMenu = new ToolStripMenuItem("편집(&E)");
        undoMenuItem = NewItem("실행 취소", Keys.Control | Keys.Z, (_, _) => { if (editor.CanUndo) editor.Undo(); });
        redoMenuItem = NewItem("다시 실행", Keys.Control | Keys.Y, (_, _) => { if (editor.CanRedo) editor.Redo(); });
        cutMenuItem = NewItem("오려두기", null, (_, _) => editor.Cut());
        cutMenuItem.ShortcutKeyDisplayString = "Ctrl+X";
        copyMenuItem = NewItem("복사", null, (_, _) => editor.Copy());
        copyMenuItem.ShortcutKeyDisplayString = "Ctrl+C";
        pasteMenuItem = NewItem("붙여넣기", null, (_, _) => editor.PastePlainText());
        pasteMenuItem.ShortcutKeyDisplayString = "Ctrl+V";
        var selectAllItem = NewItem("전체 선택", null, (_, _) => editor.SelectAll());
        selectAllItem.ShortcutKeyDisplayString = "Ctrl+A";

        editMenu.DropDownItems.Add(undoMenuItem);
        editMenu.DropDownItems.Add(redoMenuItem);
        editMenu.DropDownItems.Add(new ToolStripSeparator());
        editMenu.DropDownItems.Add(cutMenuItem);
        editMenu.DropDownItems.Add(copyMenuItem);
        editMenu.DropDownItems.Add(pasteMenuItem);
        editMenu.DropDownItems.Add(selectAllItem);
        editMenu.DropDownItems.Add(new ToolStripSeparator());
        editMenu.DropDownItems.Add(NewItem("왼쪽 정렬", Keys.Control | Keys.L, (_, _) => SetAlignment(HorizontalAlignment.Left)));
        editMenu.DropDownItems.Add(NewItem("가운데 정렬", Keys.Control | Keys.E, (_, _) => SetAlignment(HorizontalAlignment.Center)));
        editMenu.DropDownItems.Add(NewItem("오른쪽 정렬", Keys.Control | Keys.R, (_, _) => SetAlignment(HorizontalAlignment.Right)));
        editMenu.DropDownOpening += (_, _) =>
        {
            undoMenuItem.Enabled = editor.CanUndo;
            redoMenuItem.Enabled = editor.CanRedo;
            bool hasSelection = editor.SelectionLength > 0;
            cutMenuItem.Enabled = hasSelection;
            copyMenuItem.Enabled = hasSelection;
            try { pasteMenuItem.Enabled = Clipboard.ContainsText(); } catch { pasteMenuItem.Enabled = true; }
        };

        var viewMenu = new ToolStripMenuItem("보기(&V)");
        wrapMenuItem = NewItem("자동 줄바꿈", Keys.Control | Keys.Alt | Keys.W, (_, _) => ToggleLineWrap());
        wrapMenuItem.Checked = settings.LineWrapEnabled;
        viewMenu.DropDownItems.Add(wrapMenuItem);
        viewMenu.DropDownItems.Add(new ToolStripSeparator());
        var zoomInItem = NewItem("글자 크게", Keys.Control | Keys.Oemplus, (_, _) => ChangeFontSize(KeyboardFontSizeStep));
        zoomInItem.ShortcutKeyDisplayString = "Ctrl++";
        var zoomOutItem = NewItem("글자 작게", Keys.Control | Keys.OemMinus, (_, _) => ChangeFontSize(-KeyboardFontSizeStep));
        zoomOutItem.ShortcutKeyDisplayString = "Ctrl+-";
        var zoomResetItem = NewItem("글자 크기 초기화", Keys.Control | Keys.D0, (_, _) => ResetFontSize());
        zoomResetItem.ShortcutKeyDisplayString = "Ctrl+0";
        viewMenu.DropDownItems.Add(zoomInItem);
        viewMenu.DropDownItems.Add(zoomOutItem);
        viewMenu.DropDownItems.Add(zoomResetItem);
        viewMenu.DropDownItems.Add(new ToolStripSeparator());
        viewMenu.DropDownItems.Add(NewItem("글꼴 설정...", null, (_, _) => OpenFontSettings()));

        var tabMenu = new ToolStripMenuItem("탭(&T)");
        tabMenu.DropDownItems.Add(NewItem("새 탭", Keys.Control | Keys.T, (_, _) => CreateNewTab()));
        tabMenu.DropDownItems.Add(NewItem("현재 탭 닫기", Keys.Control | Keys.W, (_, _) => CloseCurrentTab()));
        tabMenu.DropDownItems.Add(new ToolStripSeparator());
        var prevTabItem = NewItem("이전 탭", null, (_, _) => SelectAdjacentTab(-1));
        prevTabItem.ShortcutKeyDisplayString = "Ctrl+Shift+Tab";
        var nextTabItem = NewItem("다음 탭", null, (_, _) => SelectAdjacentTab(1));
        nextTabItem.ShortcutKeyDisplayString = "Ctrl+Tab";
        tabMenu.DropDownItems.Add(prevTabItem);
        tabMenu.DropDownItems.Add(nextTabItem);

        menu.Items.Add(fileMenu);
        menu.Items.Add(editMenu);
        menu.Items.Add(viewMenu);
        menu.Items.Add(tabMenu);
    }

    private static ToolStripMenuItem NewItem(string text, Keys? shortcut, EventHandler onClick)
    {
        var item = new ToolStripMenuItem(text);
        if (shortcut.HasValue)
        {
            item.ShortcutKeys = shortcut.Value;
        }

        item.Click += onClick;
        return item;
    }

    private void ApplyWindowPlacement()
    {
        float s = DeviceDpi / 96f;
        MinimumSize = new Size((int)(420 * s), (int)(280 * s));

        var placement = settings.Window;
        if (placement != null && placement.Width >= 200 && placement.Height >= 150)
        {
            var target = new Rectangle(placement.X, placement.Y, placement.Width, placement.Height);
            bool visible = Screen.AllScreens.Any(screen => screen.WorkingArea.IntersectsWith(target));
            if (visible)
            {
                StartPosition = FormStartPosition.Manual;
                Bounds = target;
                if (placement.Maximized)
                {
                    WindowState = FormWindowState.Maximized;
                }

                return;
            }
        }

        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size((int)(900 * s), (int)(680 * s));
    }

    private void LoadSession()
    {
        var session = storage.LoadSession();
        tabs.Clear();
        tabs.AddRange(session.Tabs.Count > 0 ? session.Tabs : new List<MemoTab> { MemoTab.Create(1) });
        selectedTabId = session.SelectedTabId ?? tabs[0].Id;

        LoadSelectedTab();
        SaveSessionSafe();
    }

    private void LoadSelectedTab()
    {
        var tab = CurrentTab;
        if (tab == null)
        {
            return;
        }

        bool migratedLegacyTitle = false;
        isLoadingTab = true;
        try
        {
            bool loaded = false;
            string? rtf = storage.LoadTabRtf(tab.Id);
            if (rtf != null)
            {
                try
                {
                    editor.Rtf = rtf;
                    loaded = true;
                }
                catch
                {
                }
            }

            if (!loaded)
            {
                editor.Text = storage.LoadTabText(tab.Id) ?? "";
            }

            titleEditor.Text = tab.Title;
            migratedLegacyTitle = SeparateLegacyTitleFromBody(tab);
            editor.Select(0, 0);
            editor.ApplyDefaultCharFormat();
            editor.ApplyDefaultParagraphFormat();
            editor.ApplyWrap();
            editor.ApplyInsets();
            ApplyZoom();
            editor.ClearUndo();
            editor.Modified = false;

            gutter.RebuildLineStarts(editor.Text);
            gutter.SetTypography(editorBaseFont, ZoomRatio, fontSize);
            RefreshTabBar();
        }
        finally
        {
            isLoadingTab = false;
        }

        if (migratedLegacyTitle)
        {
            SaveNow(showError: false);
        }
    }

    private void OnEditorTextChanged()
    {
        if (isLoadingTab)
        {
            return;
        }

        if (editor.IsImeComposing)
        {
            editorTextChangePending = true;
            return;
        }

        string text = editor.Text;
        gutter.RebuildLineStarts(text);
        ScheduleAutosave();
    }

    private void OnTitleTextChanged()
    {
        if (isLoadingTab)
        {
            return;
        }

        UpdateCurrentTabTitle();
        ScheduleAutosave();
    }

    private void OnImeCompositionEnded()
    {
        if (editorTextChangePending)
        {
            editorTextChangePending = false;
            OnEditorTextChanged();
        }

        if (autosaveAfterIme)
        {
            autosaveAfterIme = false;
            ScheduleAutosave();
        }
    }

    private void ScheduleAutosave()
    {
        autosaveTimer.Stop();
        autosaveTimer.Start();
    }

    private void SaveNow(bool showError)
    {
        var tab = CurrentTab;
        if (tab == null)
        {
            return;
        }

        try
        {
            storage.SaveTab(tab.Id, editor.Rtf ?? "", editor.Text);
            storage.SaveSession(new MemoSession { Tabs = tabs.ToList(), SelectedTabId = selectedTabId });
            storage.SaveSettings(SnapshotSettings());
        }
        catch (Exception error)
        {
            if (showError)
            {
                MessageBox.Show(this, error.Message, "저장 실패", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

    private void SaveSessionSafe()
    {
        try
        {
            storage.SaveSession(new MemoSession { Tabs = tabs.ToList(), SelectedTabId = selectedTabId });
        }
        catch
        {
        }
    }

    private MemoSettings SnapshotSettings()
    {
        var bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
        return new MemoSettings
        {
            FontFamily = editorBaseFont.FontFamily.Name,
            FontSize = fontSize,
            LineWrapEnabled = settings.LineWrapEnabled,
            Window = new WindowPlacement
            {
                X = bounds.X,
                Y = bounds.Y,
                Width = bounds.Width,
                Height = bounds.Height,
                Maximized = WindowState == FormWindowState.Maximized,
            },
        };
    }

    private void CreateNewTab()
    {
        SaveNow(showError: false);

        var tab = MemoTab.Create(NextTabNumber());
        tabs.Add(tab);
        selectedTabId = tab.Id;
        LoadSelectedTab();
        SaveSessionSafe();
        editor.Focus();
    }

    private void CloseCurrentTab()
    {
        if (selectedTabId != null)
        {
            CloseTab(selectedTabId);
        }
    }

    private void CloseTab(string id)
    {
        int index = tabs.FindIndex(t => t.Id == id);
        if (index < 0)
        {
            return;
        }

        if (!ConfirmTabClose(tabs[index]))
        {
            return;
        }

        bool closingSelected = id == selectedTabId;
        if (closingSelected)
        {
            SaveNow(showError: false);
        }

        var removed = tabs[index];
        tabs.RemoveAt(index);
        storage.DeleteTab(removed.Id);

        if (tabs.Count == 0)
        {
            tabs.Add(MemoTab.Create(1));
        }

        if (closingSelected)
        {
            selectedTabId = tabs[Math.Min(index, tabs.Count - 1)].Id;
            LoadSelectedTab();
        }
        else
        {
            RefreshTabBar();
        }

        SaveSessionSafe();
        editor.Focus();
    }

    private void SelectTab(string id)
    {
        if (id == selectedTabId || !tabs.Any(t => t.Id == id))
        {
            return;
        }

        SaveNow(showError: false);
        selectedTabId = id;
        LoadSelectedTab();
        SaveSessionSafe();
        editor.Focus();
    }

    private void SelectAdjacentTab(int offset)
    {
        if (tabs.Count <= 1 || selectedTabId == null)
        {
            return;
        }

        int current = tabs.FindIndex(t => t.Id == selectedTabId);
        if (current < 0)
        {
            return;
        }

        int next = (current + offset + tabs.Count) % tabs.Count;
        SelectTab(tabs[next].Id);
    }

    private bool ConfirmTabClose(MemoTab tab)
    {
        string message = $"‘{tab.Title}’ 탭을 닫으시겠습니까?\n본문 내용은 삭제됩니다.";
        return MessageBox.Show(this, message, "탭 닫기", MessageBoxButtons.OKCancel,
            MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1) == DialogResult.OK;
    }

    private void UpdateCurrentTabTitle()
    {
        if (selectedTabId == null)
        {
            return;
        }

        int index = tabs.FindIndex(t => t.Id == selectedTabId);
        if (index < 0)
        {
            return;
        }

        string title = titleEditor.Text.Trim();
        if (title.Length == 0)
        {
            title = tabs[index].Title.StartsWith("메모 ", StringComparison.Ordinal)
                ? tabs[index].Title
                : $"메모 {NextTabNumber(tabs[index].Id)}";
        }

        if (tabs[index].Title == title)
        {
            return;
        }

        tabs[index].Title = title;
        RefreshTabBar();
        SaveSessionSafe();
    }

    private bool SeparateLegacyTitleFromBody(MemoTab tab)
    {
        if (tab.BodyIsSeparate)
        {
            return false;
        }

        tab.BodyIsSeparate = true;

        string text = editor.Text;
        int lineStart = 0;
        while (lineStart < text.Length)
        {
            int newline = text.IndexOf('\n', lineStart);
            int lineEnd = newline < 0 ? text.Length : newline;
            if (text[lineStart..lineEnd].Trim().Length > 0)
            {
                int removeEnd = newline < 0 ? text.Length : newline + 1;
                editor.Select(0, removeEnd);
                editor.SelectedText = "";
                return true;
            }

            if (newline < 0)
            {
                break;
            }

            lineStart = newline + 1;
        }

        return true;
    }

    private int NextTabNumber(string? excludedTabId = null)
    {
        var titles = tabs
            .Where(t => t.Id != excludedTabId)
            .Select(t => t.Title)
            .ToHashSet();
        int number = 1;
        while (titles.Contains($"메모 {number}"))
        {
            number++;
        }

        return number;
    }

    private void RefreshTabBar()
    {
        bool show = tabs.Count > 1;
        tabBar.UpdateTabs(show ? tabs.ToList() : Array.Empty<MemoTab>(), selectedTabId);
        AnimateTabBarTo(show ? tabBar.PreferredHeight : 0);
    }

    private void AnimateTabBarTo(int target)
    {
        if (tabBar.Height == target && !tabBarAnimTimer.Enabled)
        {
            return;
        }

        if (tabBarAnimTimer.Enabled && tabBarAnimTo == target)
        {
            return;
        }

        tabBarAnimFrom = tabBar.Height;
        tabBarAnimTo = target;
        tabBarAnimClock.Restart();
        tabBarAnimTimer.Start();
    }

    private void OnTabBarAnimTick()
    {
        const float durationMs = 140f;
        float t = Math.Min(1f, tabBarAnimClock.ElapsedMilliseconds / durationMs);
        float eased = 1f - MathF.Pow(1f - t, 3f);
        tabBar.Height = (int)Math.Round(tabBarAnimFrom + (tabBarAnimTo - tabBarAnimFrom) * eased);

        if (t >= 1f)
        {
            tabBarAnimTimer.Stop();
            tabBar.Height = tabBarAnimTo;
        }
    }

    private void ChangeFontSize(float delta) => SetFontSize(fontSize + delta);

    private void ResetFontSize() => SetFontSize(Theme.BaseFontSize);

    private void SetFontSize(float value)
    {
        float clamped = ClampFontSize(value);
        if (Math.Abs(clamped - fontSize) < 0.001f)
        {
            return;
        }

        fontSize = clamped;
        ApplyZoom();
        gutter.SetTypography(editorBaseFont, ZoomRatio, fontSize);
        ScheduleAutosave();
    }

    private static float ClampFontSize(float value) =>
        Math.Clamp(value, Theme.MinFontSize, Theme.MaxFontSize);

    private void ApplyZoom() => editor.SetZoomRatio(ZoomRatio);

    private void OpenFontSettings()
    {
        using var dialog = new FontSettingsDialog(editorBaseFont.FontFamily.Name);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            SetEditorFontFamily(dialog.SelectedFontFamily);
        }

        editor.Focus();
    }

    private void SetEditorFontFamily(string fontFamily)
    {
        string resolved = Theme.ResolveEditorFontFamily(fontFamily);
        if (resolved.Equals(editorBaseFont.FontFamily.Name, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Font previous = editorBaseFont;
        editorBaseFont = new Font(resolved, Theme.BaseFontSize, FontStyle.Regular, GraphicsUnit.Point);
        editor.Font = editorBaseFont;
        editor.ApplyDefaultCharFormat();
        editor.ApplyDefaultParagraphFormat();
        gutter.SetTypography(editorBaseFont, ZoomRatio, fontSize);
        ApplyZoom();
        settings.FontFamily = editorBaseFont.FontFamily.Name;
        ScheduleAutosave();
        previous.Dispose();
    }

    private void SetAlignment(HorizontalAlignment alignment)
    {
        editor.SelectionAlignment = alignment;
        ScheduleAutosave();
    }

    private void ToggleLineWrap()
    {
        settings.LineWrapEnabled = !settings.LineWrapEnabled;
        editor.WrapToWindow = settings.LineWrapEnabled;
        wrapMenuItem.Checked = settings.LineWrapEnabled;
        gutter.Invalidate();
        ScheduleAutosave();
    }

    private static Icon? LoadAppIcon()
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("MEMO.AppIcon.ico");
            return stream == null ? null : new Icon(stream);
        }
        catch
        {
            return null;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace MEMO
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MemoForm());
        }
    }

    internal sealed class MemoForm : Form
    {
        private readonly MemoStore store = new MemoStore();
        private readonly List<MemoTab> tabs = new List<MemoTab>();
        private readonly FlowLayoutPanel tabBar = new FlowLayoutPanel();
        private readonly Panel linePanel = new Panel();
        private readonly MemoRichTextBox editor = new MemoRichTextBox();
        private readonly Timer saveTimer = new Timer();
        private string selectedTabId;
        private bool loading;
        private float fontSize = 15f;

        public MemoForm()
        {
            Text = "MEMO";
            Width = 1000;
            Height = 720;
            MinimumSize = new Size(480, 320);
            BackColor = Colors.Background;
            ForeColor = Colors.Text;
            KeyPreview = true;

            BuildMenu();
            BuildLayout();
            LoadSession();

            saveTimer.Interval = 350;
            saveTimer.Tick += delegate
            {
                saveTimer.Stop();
                SaveCurrentTab(false);
            };

            FormClosing += delegate { SaveCurrentTab(true); SaveSession(); };
        }

        private void BuildMenu()
        {
            var menu = new MenuStrip();
            menu.BackColor = Colors.Menu;
            menu.ForeColor = Colors.Text;
            menu.Renderer = new DarkMenuRenderer();

            var file = new ToolStripMenuItem("파일");
            file.DropDownItems.Add(MenuItem("지금 저장", Keys.Control | Keys.S, delegate { SaveCurrentTab(false); }));
            file.DropDownItems.Add(MenuItem("종료", Keys.Control | Keys.Q, delegate { Close(); }));

            var edit = new ToolStripMenuItem("편집");
            edit.DropDownItems.Add(MenuItem("실행 취소", Keys.Control | Keys.Z, delegate { if (editor.CanUndo) editor.Undo(); }));
            edit.DropDownItems.Add(MenuItem("다시 실행", Keys.Control | Keys.Shift | Keys.Z, delegate { if (editor.CanRedo) editor.Redo(); }));
            edit.DropDownItems.Add(new ToolStripSeparator());
            edit.DropDownItems.Add(MenuItem("오려두기", Keys.Control | Keys.X, delegate { editor.Cut(); }));
            edit.DropDownItems.Add(MenuItem("복사", Keys.Control | Keys.C, delegate { editor.Copy(); }));
            edit.DropDownItems.Add(MenuItem("붙여넣기", Keys.Control | Keys.V, delegate { editor.PastePlainText(); }));
            edit.DropDownItems.Add(MenuItem("전체 선택", Keys.Control | Keys.A, delegate { editor.SelectAll(); }));
            edit.DropDownItems.Add(new ToolStripSeparator());
            edit.DropDownItems.Add(MenuItem("왼쪽 정렬", Keys.Control | Keys.L, delegate { SetAlignment(HorizontalAlignment.Left); }));
            edit.DropDownItems.Add(MenuItem("가운데 정렬", Keys.Control | Keys.E, delegate { SetAlignment(HorizontalAlignment.Center); }));
            edit.DropDownItems.Add(MenuItem("오른쪽 정렬", Keys.Control | Keys.R, delegate { SetAlignment(HorizontalAlignment.Right); }));

            var view = new ToolStripMenuItem("보기");
            view.DropDownItems.Add(MenuItem("글자 크게", Keys.Control | Keys.Oemplus, delegate { ChangeFontSize(1f); }));
            view.DropDownItems.Add(MenuItem("글자 작게", Keys.Control | Keys.OemMinus, delegate { ChangeFontSize(-1f); }));
            view.DropDownItems.Add(MenuItem("글자 크기 초기화", Keys.Control | Keys.D0, delegate { SetFontSize(15f); }));

            var tab = new ToolStripMenuItem("탭");
            tab.DropDownItems.Add(MenuItem("새 탭", Keys.Control | Keys.D, delegate { CreateTab(); }));
            tab.DropDownItems.Add(MenuItem("현재 탭 닫기", Keys.Control | Keys.W, delegate { CloseCurrentTab(); }));
            tab.DropDownItems.Add(MenuItem("이전 탭", Keys.Control | Keys.OemOpenBrackets, delegate { SelectTabOffset(-1); }));
            tab.DropDownItems.Add(MenuItem("다음 탭", Keys.Control | Keys.OemCloseBrackets, delegate { SelectTabOffset(1); }));

            menu.Items.Add(file);
            menu.Items.Add(edit);
            menu.Items.Add(view);
            menu.Items.Add(tab);
            MainMenuStrip = menu;
            Controls.Add(menu);
        }

        private ToolStripMenuItem MenuItem(string title, Keys shortcut, EventHandler handler)
        {
            var item = new ToolStripMenuItem(title);
            item.ShortcutKeys = shortcut;
            item.Click += handler;
            return item;
        }

        private void BuildLayout()
        {
            tabBar.Dock = DockStyle.Top;
            tabBar.Height = 32;
            tabBar.BackColor = Colors.TabBar;
            tabBar.ForeColor = Colors.Text;
            tabBar.WrapContents = false;
            tabBar.Visible = false;
            Controls.Add(tabBar);
            tabBar.BringToFront();

            var editorPanel = new Panel();
            editorPanel.Dock = DockStyle.Fill;
            editorPanel.BackColor = Colors.Background;
            Controls.Add(editorPanel);

            linePanel.Dock = DockStyle.Left;
            linePanel.Width = 54;
            linePanel.BackColor = Colors.Gutter;
            linePanel.Paint += PaintLineNumbers;
            editorPanel.Controls.Add(linePanel);

            editor.Dock = DockStyle.Fill;
            editor.BorderStyle = BorderStyle.None;
            editor.BackColor = Colors.Background;
            editor.ForeColor = Colors.Text;
            editor.Font = EditorFont(fontSize);
            editor.WordWrap = true;
            editor.AcceptsTab = true;
            editor.DetectUrls = false;
            editor.ScrollBars = RichTextBoxScrollBars.Vertical;
            editor.ZoomRequested += delegate(float delta) { ChangeFontSize(delta); };
            editor.TextChanged += delegate
            {
                if (loading) return;
                RefreshLineNumbers();
                UpdateCurrentTabTitle();
                ScheduleSave();
            };
            editor.SelectionChanged += delegate { RefreshLineNumbers(); };
            editor.VScroll += delegate { RefreshLineNumbers(); };
            editorPanel.Controls.Add(editor);
        }

        private void LoadSession()
        {
            var session = store.LoadSession();
            tabs.Clear();
            tabs.AddRange(session.Tabs);

            if (tabs.Count == 0)
            {
                tabs.Add(MemoTab.Create(1));
            }

            selectedTabId = session.SelectedTabId;
            if (FindTab(selectedTabId) == null)
            {
                selectedTabId = tabs[0].Id;
            }

            LoadSelectedTab();
            SaveSession();
        }

        private void LoadSelectedTab()
        {
            var tab = FindTab(selectedTabId);
            if (tab == null) return;

            loading = true;
            editor.Clear();
            store.LoadTab(tab, editor);
            SanitizeEditorStyle();
            loading = false;

            RefreshTabBar();
            RefreshLineNumbers();
            editor.Focus();
        }

        private void SaveCurrentTab(bool immediate)
        {
            var tab = FindTab(selectedTabId);
            if (tab == null) return;

            store.SaveTab(tab, editor);
            SaveSession();
        }

        private void SaveSession()
        {
            store.SaveSession(new MemoSession(tabs, selectedTabId));
        }

        private void ScheduleSave()
        {
            saveTimer.Stop();
            saveTimer.Start();
        }

        private void CreateTab()
        {
            SaveCurrentTab(false);
            var tab = MemoTab.Create(NextTabNumber());
            tabs.Add(tab);
            selectedTabId = tab.Id;
            LoadSelectedTab();
            SaveSession();
        }

        private void CloseCurrentTab()
        {
            CloseTab(selectedTabId);
        }

        private void CloseTab(string id)
        {
            var index = tabs.FindIndex(t => t.Id == id);
            if (index < 0) return;

            SaveCurrentTab(false);
            var removed = tabs[index];
            tabs.RemoveAt(index);
            store.DeleteTab(removed);

            if (tabs.Count == 0)
            {
                tabs.Add(MemoTab.Create(1));
            }

            selectedTabId = tabs[Math.Min(index, tabs.Count - 1)].Id;
            LoadSelectedTab();
            SaveSession();
        }

        private void SelectTabOffset(int offset)
        {
            if (tabs.Count < 2) return;

            var index = tabs.FindIndex(t => t.Id == selectedTabId);
            if (index < 0) return;

            SelectTab(tabs[(index + offset + tabs.Count) % tabs.Count].Id);
        }

        private void SelectTab(string id)
        {
            if (id == selectedTabId || FindTab(id) == null) return;

            SaveCurrentTab(false);
            selectedTabId = id;
            LoadSelectedTab();
            SaveSession();
        }

        private void RefreshTabBar()
        {
            tabBar.SuspendLayout();
            tabBar.Controls.Clear();
            tabBar.Visible = tabs.Count > 1;

            if (tabs.Count > 1)
            {
                foreach (var tab in tabs)
                {
                    var item = new MemoTabHeader(tab, tab.Id == selectedTabId);
                    item.Selected += delegate { SelectTab(tab.Id); };
                    item.Closed += delegate { CloseTab(tab.Id); };
                    tabBar.Controls.Add(item);
                }
            }

            tabBar.ResumeLayout();
        }

        private void PaintLineNumbers(object sender, PaintEventArgs e)
        {
            e.Graphics.Clear(Colors.Gutter);
            using (var pen = new Pen(Colors.Border))
            {
                e.Graphics.DrawLine(pen, linePanel.Width - 1, 0, linePanel.Width - 1, linePanel.Height);
            }

            var starts = LogicalLineStarts(editor.Text);
            using (var brush = new SolidBrush(Colors.LineNumber))
            using (var font = new Font("Consolas", Math.Max(10f, Math.Min(13f, fontSize - 2f))))
            {
                for (var i = 0; i < starts.Count; i++)
                {
                    var y = LineY(starts[i]);
                    if (y < -24 || y > linePanel.Height + 24) continue;

                    var text = (i + 1).ToString();
                    var size = e.Graphics.MeasureString(text, font);
                    e.Graphics.DrawString(text, font, brush, linePanel.Width - size.Width - 13, y);
                }
            }
        }

        private int LineY(int charIndex)
        {
            if (editor.TextLength == 0)
            {
                return 4;
            }

            var safeIndex = Math.Min(charIndex, Math.Max(0, editor.TextLength - 1));
            var point = editor.GetPositionFromCharIndex(safeIndex);
            return point.Y + 2;
        }

        private List<int> LogicalLineStarts(string text)
        {
            var starts = new List<int>();
            starts.Add(0);

            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    starts.Add(i + 1);
                }
            }

            return starts;
        }

        private void RefreshLineNumbers()
        {
            linePanel.Invalidate();
        }

        private void SanitizeEditorStyle()
        {
            var selectionStart = editor.SelectionStart;
            var selectionLength = editor.SelectionLength;

            editor.SelectAll();
            editor.SelectionFont = EditorFont(fontSize);
            editor.SelectionColor = Colors.Text;
            editor.SelectionBackColor = Colors.Background;
            editor.DeselectAll();
            editor.Select(Math.Min(selectionStart, editor.TextLength), Math.Min(selectionLength, Math.Max(0, editor.TextLength - selectionStart)));
        }

        private void SetAlignment(HorizontalAlignment alignment)
        {
            editor.SelectionAlignment = alignment;
            ScheduleSave();
        }

        private void ChangeFontSize(float delta)
        {
            SetFontSize(fontSize + delta);
        }

        private void SetFontSize(float newSize)
        {
            fontSize = Math.Max(10f, Math.Min(34f, newSize));
            SanitizeEditorStyle();
            RefreshLineNumbers();
            ScheduleSave();
        }

        private Font EditorFont(float size)
        {
            return new Font("Consolas", size, FontStyle.Regular, GraphicsUnit.Point);
        }

        private void UpdateCurrentTabTitle()
        {
            var tab = FindTab(selectedTabId);
            if (tab == null) return;

            var title = FirstNonEmptyLine(editor.Text);
            if (title.Length == 0) return;

            if (title.Length > 24)
            {
                title = title.Substring(0, 24);
            }

            if (tab.Title == title) return;

            tab.Title = title;
            RefreshTabBar();
            SaveSession();
        }

        private string FirstNonEmptyLine(string text)
        {
            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.Length > 0)
                {
                    return trimmed;
                }
            }

            return "";
        }

        private int NextTabNumber()
        {
            var number = 1;

            while (tabs.Exists(t => t.Title == "메모 " + number))
            {
                number++;
            }

            return number;
        }

        private MemoTab FindTab(string id)
        {
            return tabs.Find(t => t.Id == id);
        }
    }

    internal sealed class MemoRichTextBox : RichTextBox
    {
        public event Action<float> ZoomRequested;

        public void PastePlainText()
        {
            if (!Clipboard.ContainsText()) return;
            SelectedText = Sanitize(Clipboard.GetText(TextDataFormat.UnicodeText));
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_PASTE = 0x0302;

            if (m.Msg == WM_PASTE)
            {
                PastePlainText();
                return;
            }

            base.WndProc(ref m);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if ((ModifierKeys & Keys.Control) == Keys.Control)
            {
                var handler = ZoomRequested;
                if (handler != null)
                {
                    handler(e.Delta > 0 ? 0.25f : -0.25f);
                }

                return;
            }

            base.OnMouseWheel(e);
        }

        private static string Sanitize(string text)
        {
            var builder = new StringBuilder(text.Length);

            foreach (var ch in text)
            {
                if (ch == '\uFEFF' || ch == '\uFFFC') continue;
                if (ch == '\u2028' || ch == '\u2029')
                {
                    builder.Append(Environment.NewLine);
                }
                else
                {
                    builder.Append(ch);
                }
            }

            return builder.ToString();
        }
    }

    internal sealed class MemoTabHeader : UserControl
    {
        public event Action Selected;
        public event Action Closed;

        private readonly Label title = new Label();
        private readonly Label close = new Label();

        public MemoTabHeader(MemoTab tab, bool selected)
        {
            Width = 178;
            Height = 31;
            Margin = new Padding(0);
            BackColor = selected ? Colors.SelectedTab : Colors.Tab;

            title.Text = tab.Title;
            title.ForeColor = Colors.Text;
            title.BackColor = BackColor;
            title.Left = 12;
            title.Top = 7;
            title.Width = 132;
            title.Height = 18;
            title.AutoEllipsis = true;
            title.Click += delegate { if (Selected != null) Selected(); };
            Controls.Add(title);

            close.Text = "x";
            close.ForeColor = Colors.LineNumber;
            close.BackColor = BackColor;
            close.Left = 150;
            close.Top = 7;
            close.Width = 16;
            close.Height = 18;
            close.TextAlign = ContentAlignment.MiddleCenter;
            close.Click += delegate { if (Closed != null) Closed(); };
            Controls.Add(close);

            Click += delegate { if (Selected != null) Selected(); };
        }
    }

    internal sealed class MemoStore
    {
        private readonly string root;
        private readonly string tabsDir;
        private readonly string manifestPath;

        public MemoStore()
        {
            root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MEMO");
            tabsDir = Path.Combine(root, "tabs");
            manifestPath = Path.Combine(root, "tabs.tsv");
        }

        public MemoSession LoadSession()
        {
            var tabs = new List<MemoTab>();
            string selected = null;

            if (File.Exists(manifestPath))
            {
                var lines = File.ReadAllLines(manifestPath, Encoding.UTF8);
                if (lines.Length > 0)
                {
                    selected = Unescape(lines[0]);
                }

                for (var i = 1; i < lines.Length; i++)
                {
                    var parts = lines[i].Split(new[] { '\t' }, 2);
                    if (parts.Length != 2) continue;

                    var id = CleanId(Unescape(parts[0]));
                    if (id.Length == 0) continue;

                    tabs.Add(new MemoTab(id, Unescape(parts[1])));
                }
            }

            if (tabs.Count == 0)
            {
                tabs.Add(MemoTab.Create(1));
            }

            return new MemoSession(tabs, selected);
        }

        public void SaveSession(MemoSession session)
        {
            Directory.CreateDirectory(root);

            var lines = new List<string>();
            lines.Add(Escape(session.SelectedTabId ?? ""));

            foreach (var tab in session.Tabs)
            {
                lines.Add(Escape(tab.Id) + "\t" + Escape(tab.Title));
            }

            File.WriteAllLines(manifestPath, lines.ToArray(), Encoding.UTF8);
        }

        public void LoadTab(MemoTab tab, RichTextBox editor)
        {
            Directory.CreateDirectory(tabsDir);
            var rtfPath = RtfPath(tab);
            var txtPath = TextPath(tab);

            if (File.Exists(rtfPath))
            {
                try
                {
                    editor.LoadFile(rtfPath, RichTextBoxStreamType.RichText);
                    return;
                }
                catch
                {
                    editor.Clear();
                }
            }

            if (File.Exists(txtPath))
            {
                editor.Text = File.ReadAllText(txtPath, Encoding.UTF8);
            }
        }

        public void SaveTab(MemoTab tab, RichTextBox editor)
        {
            Directory.CreateDirectory(tabsDir);
            editor.SaveFile(RtfPath(tab), RichTextBoxStreamType.RichText);
            File.WriteAllText(TextPath(tab), editor.Text, Encoding.UTF8);
        }

        public void DeleteTab(MemoTab tab)
        {
            TryDelete(RtfPath(tab));
            TryDelete(TextPath(tab));
        }

        private string RtfPath(MemoTab tab)
        {
            return Path.Combine(tabsDir, CleanId(tab.Id) + ".rtf");
        }

        private string TextPath(MemoTab tab)
        {
            return Path.Combine(tabsDir, CleanId(tab.Id) + ".txt");
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
            }
        }

        private static string CleanId(string id)
        {
            var builder = new StringBuilder();

            foreach (var ch in id)
            {
                if (char.IsLetterOrDigit(ch) || ch == '-' || ch == '_')
                {
                    builder.Append(ch);
                }
            }

            return builder.ToString();
        }

        private static string Escape(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? ""));
        }

        private static string Unescape(string value)
        {
            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(value ?? ""));
            }
            catch
            {
                return "";
            }
        }
    }

    internal sealed class MemoSession
    {
        public readonly List<MemoTab> Tabs;
        public readonly string SelectedTabId;

        public MemoSession(List<MemoTab> tabs, string selectedTabId)
        {
            Tabs = tabs;
            SelectedTabId = selectedTabId;
        }
    }

    internal sealed class MemoTab
    {
        public readonly string Id;
        public string Title;

        public MemoTab(string id, string title)
        {
            Id = id;
            Title = string.IsNullOrWhiteSpace(title) ? "메모" : title;
        }

        public static MemoTab Create(int index)
        {
            return new MemoTab(Guid.NewGuid().ToString("D"), "메모 " + index);
        }
    }

    internal static class Colors
    {
        public static readonly Color Background = Color.FromArgb(18, 19, 21);
        public static readonly Color Gutter = Color.FromArgb(14, 15, 17);
        public static readonly Color Menu = Color.FromArgb(45, 46, 49);
        public static readonly Color TabBar = Color.FromArgb(14, 15, 17);
        public static readonly Color Tab = Color.FromArgb(25, 26, 29);
        public static readonly Color SelectedTab = Color.FromArgb(32, 33, 37);
        public static readonly Color Border = Color.FromArgb(58, 59, 63);
        public static readonly Color Text = Color.FromArgb(235, 235, 236);
        public static readonly Color LineNumber = Color.FromArgb(155, 156, 160);
    }

    internal sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            using (var brush = new SolidBrush(e.Item.Selected ? Colors.SelectedTab : Colors.Menu))
            {
                e.Graphics.FillRectangle(brush, new Rectangle(Point.Empty, e.Item.Size));
            }
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
        }
    }
}

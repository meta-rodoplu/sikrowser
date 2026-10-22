using System.ComponentModel;
using System.Diagnostics;
using Sikrowser.Benchmark;
using Sikrowser.Core;

namespace Sikrowser.UI;

public partial class MainForm : Form
{
    private readonly TabManager _tabManager;
    private readonly Panel _webContainer;
    private readonly FlowLayoutPanel _tabStrip;
    private readonly FlowLayoutPanel _bookmarksBar;
    private readonly Panel _pnlResourceWarning;
    private readonly Label _lblResourceWarning;
    private readonly Button _btnWarningCloseTab;
    private readonly Button _btnWarningHibernate;
    private readonly Button _btnWarningDismiss;
    private readonly TextBox _txtUrl;
    private readonly Button _btnBack;
    private readonly Button _btnForward;
    private readonly Button _btnReload;
    private readonly Button _btnBookmarks;
    private readonly Button _btnAdBlock;
    private readonly Button _btnTrimRam;
    private readonly Button _btnBenchmark;
    private readonly Button _btnNewTab;
    private readonly StatusStrip _statusStrip;
    private readonly ToolStripStatusLabel _lblStatus;
    private readonly ToolStripStatusLabel _lblRamSummary;
    private readonly System.Windows.Forms.Timer _ramTimer;
    private BrowserTab? _highResourceTab;
    private bool _bookmarksRendered;
    private volatile bool _isClosing;

    public MainForm()
    {
        Text = $"{BrowserConfig.AppName} - Ultra Hafif Tarayıcı";
        Width = 1100;
        Height = 750;
        MinimumSize = new Size(600, 400);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(24, 24, 27); // Modern dark zinc
        ForeColor = Color.FromArgb(244, 244, 245);
        Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        KeyPreview = true;

        // Load Custom Sikrowser Icon
        try
        {
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "sikrowser.ico");
            if (File.Exists(iconPath))
            {
                Icon = new Icon(iconPath);
            }
        }
        catch { }

        // Content Container and TabManager initialized early
        _webContainer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(20, 20, 23)
        };
        _tabManager = new TabManager(_webContainer);

        // 1. Top Control Bar (Navigation + Omnibox + Actions)
        var topBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 44,
            BackColor = Color.FromArgb(34, 34, 38),
            Padding = new Padding(6, 6, 6, 6)
        };

        _btnBack = CreateFlatButton("◄", "Geri (Alt+Sol)", 32);
        _btnBack.Click += (s, e) => NavigateBack();

        _btnForward = CreateFlatButton("►", "İleri (Alt+Sağ)", 32);
        _btnForward.Click += (s, e) => NavigateForward();

        _btnReload = CreateFlatButton("↻", "Yenile (F5)", 32);
        _btnReload.Click += (s, e) => Reload();

        _txtUrl = new TextBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(24, 24, 27),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point)
        };
        _txtUrl.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                NavigateToInput();
            }
        };
        _txtUrl.TextChanged += (s, e) =>
        {
            AdaptivePerformanceManager.Instance.NotifyUserActivity(_tabManager.ActiveTab?.WebViewControl?.CoreWebView2);
        };

        var urlContainer = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(6, 2, 6, 2)
        };
        urlContainer.Controls.Add(_txtUrl);

        var btnSpeed = CreatePillButton("⚡ Hız: %100", Color.FromArgb(13, 148, 136), "Tam Hat Hızı: HTTP/3 (QUIC), Paralel İndirme, Sıfır Disk Gecikmesi ve Reklamsız Yüksek Bant Genişliği devrede.");

        _btnBookmarks = CreatePillButton("⭐ Yer İmleri", Color.FromArgb(45, 45, 52), "Yer İmleri Çubuğunu Aç/Kapat (Kısayol: Ctrl+B)");
        _btnBookmarks.Click += (s, e) => ToggleBookmarksBar();

        _btnAdBlock = CreatePillButton("🛡️ Reklam: Açık (0)", Color.FromArgb(22, 101, 52), "Reklam & İzleyici Engelleme (Tıklayınca Değiştir)");
        _btnAdBlock.Click += (s, e) => ToggleAdBlock();

        var btnPrivacy = CreatePillButton("🕶️ Geçmiş: Tutulmuyor", Color.FromArgb(51, 65, 85), "Gizli Oturum (InPrivate): Geçmiş, çerezler ve geçici dosyalar diske kaydedilmez ve tarayıcı kapandığında tamamen silinir.");

        _btnTrimRam = CreatePillButton("🧹 RAM Temizle", Color.FromArgb(30, 58, 138), "Kullanılmayan belleği kırp ve GC çalıştır");
        _btnTrimRam.Click += (s, e) =>
        {
            _tabManager.TrimMemory();
            UpdateRamDisplay();
        };

        _btnBenchmark = CreatePillButton("⚡ Test", Color.FromArgb(120, 53, 15), "5 Aşamalı RAM Testini Başlat");
        _btnBenchmark.Click += async (s, e) => await StartBenchmarkAsync();

        _btnNewTab = CreateFlatButton("➕", "Yeni Sekme (Ctrl+T)", 32);
        _btnNewTab.Click += async (s, e) => await _tabManager.CreateTabAsync("about:blank", true);

        // Right-side actions panel
        var rightPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        rightPanel.Controls.Add(_btnBookmarks);
        rightPanel.Controls.Add(btnSpeed);
        rightPanel.Controls.Add(btnPrivacy);
        rightPanel.Controls.Add(_btnAdBlock);
        rightPanel.Controls.Add(_btnTrimRam);
        rightPanel.Controls.Add(_btnBenchmark);
        rightPanel.Controls.Add(_btnNewTab);

        // Left-side navigation buttons panel
        var leftPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Left,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        leftPanel.Controls.Add(_btnBack);
        leftPanel.Controls.Add(_btnForward);
        leftPanel.Controls.Add(_btnReload);

        topBar.Controls.Add(urlContainer);
        topBar.Controls.Add(rightPanel);
        topBar.Controls.Add(leftPanel);

        // 2. Tab Strip (Minimalist Tabs)
        _tabStrip = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 32,
            BackColor = Color.FromArgb(28, 28, 32),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(4, 2, 4, 2)
        };

        // 3. Bookmarks Bar (Yer İmleri / Hızlı Erişim Çubuğu) - Lazy loaded
        _bookmarksBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 28,
            BackColor = Color.FromArgb(22, 22, 25),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(6, 2, 6, 2),
            Visible = false // Lazy loading: Start hidden for instant startup
        };

        // 4. Resource Warning Banner (Alerts if a tab consumes excessive RAM/CPU)
        _pnlResourceWarning = new Panel
        {
            Dock = DockStyle.Top,
            Height = 36,
            BackColor = Color.FromArgb(88, 28, 28), // Deep warning crimson
            Padding = new Padding(10, 2, 10, 2),
            Visible = false
        };

        _lblResourceWarning = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(254, 242, 242),
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "⚠️ Bu sekme yüksek RAM ve CPU harcıyor!"
        };

        var warningActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };

        _btnWarningCloseTab = CreatePillButton("🚫 Sekmeyi Kapat", Color.FromArgb(185, 28, 28), "Bu sekmeyi hemen kapat");
        _btnWarningCloseTab.Click += async (s, e) =>
        {
            if (_highResourceTab != null)
            {
                await _tabManager.CloseTabAsync(_highResourceTab);
            }
            _pnlResourceWarning.Visible = false;
        };

        _btnWarningHibernate = CreatePillButton("💤 Sekmeyi Uyut", Color.FromArgb(146, 64, 14), "Sekmeyi uyut ve belleği serbest bırak");
        _btnWarningHibernate.Click += (s, e) =>
        {
            if (_highResourceTab != null)
            {
                _highResourceTab.Hibernate();
                _tabManager.TrimMemory();
            }
            _pnlResourceWarning.Visible = false;
        };

        _btnWarningDismiss = CreatePillButton("✕ Uyarıyı Kapat", Color.FromArgb(63, 63, 70), "Uyarıyı kapat");
        _btnWarningDismiss.Click += (s, e) =>
        {
            if (_highResourceTab != null)
            {
                _tabManager.DismissHighResourceWarning(_highResourceTab.Id);
            }
            _pnlResourceWarning.Visible = false;
        };

        warningActions.Controls.Add(_btnWarningCloseTab);
        warningActions.Controls.Add(_btnWarningHibernate);
        warningActions.Controls.Add(_btnWarningDismiss);

        _pnlResourceWarning.Controls.Add(_lblResourceWarning);
        _pnlResourceWarning.Controls.Add(warningActions);

        // 5. Status Bar
        _statusStrip = new StatusStrip
        {
            BackColor = Color.FromArgb(24, 24, 27),
            ForeColor = Color.FromArgb(161, 161, 170),
            SizingGrip = false
        };
        _lblStatus = new ToolStripStatusLabel("Başlatılıyor...")
        {
            Spring = true,
            TextAlign = ContentAlignment.MiddleLeft
        };
        _lblRamSummary = new ToolStripStatusLabel("RAM: Ölçülüyor...")
        {
            Font = new Font("Consolas", 8.5F, FontStyle.Regular, GraphicsUnit.Point)
        };
        _statusStrip.Items.Add(_lblStatus);
        _statusStrip.Items.Add(_lblRamSummary);

        // Add controls in docking order (Top docks from bottom up: statusStrip -> webContainer -> warning -> bookmarksBar -> topBar -> tabStrip)
        Controls.Add(_webContainer);
        Controls.Add(_pnlResourceWarning);
        Controls.Add(_bookmarksBar);
        Controls.Add(topBar);
        Controls.Add(_tabStrip);
        Controls.Add(_statusStrip);

        // Wire Bookmark Manager (re-renders only if already loaded)
        BookmarkManager.Instance.BookmarksChanged += () =>
        {
            if (_bookmarksRendered) RenderBookmarksBar();
        };

        // Wire Tab Manager events
        _tabManager.ActiveTabChanged += OnActiveTabChanged;
        _tabManager.TabsListChanged += RenderTabStrip;
        _tabManager.HighResourceTabDetected += OnHighResourceTabDetected;
        _tabManager.StatusMessage += msg =>
        {
            if (InvokeRequired) Invoke(() => _lblStatus.Text = msg);
            else _lblStatus.Text = msg;
        };

        // Wire AdBlock real-time count
        AdBlockEngine.Instance.RequestEvaluated += (uri, blocked, reason) =>
        {
            if (blocked && !IsDisposed && IsHandleCreated)
            {
                try
                {
                    BeginInvoke(UpdateAdBlockButtonText);
                }
                catch { }
            }
        };

        // Wire Adaptive Performance Manager
        AdaptivePerformanceManager.Instance.ProfileChanged += (profile, text) =>
        {
            if (!IsDisposed && IsHandleCreated)
            {
                try
                {
                    BeginInvoke(() => _lblStatus.Text = text);
                }
                catch { }
            }
        };

        // RAM update timer (every 1.5 seconds)
        _ramTimer = new System.Windows.Forms.Timer { Interval = 1500 };
        _ramTimer.Tick += (s, e) => UpdateRamDisplay();
        _ramTimer.Start();

        KeyDown += OnFormKeyDown;

        Resize += (s, e) =>
        {
            if (WindowState == FormWindowState.Minimized)
            {
                _ = _tabManager.SuspendActiveTabAsync();
            }
            else
            {
                _tabManager.ResumeActiveTab();
            }
        };

        // Load initial tab
        Load += async (s, e) =>
        {
            BrowserConfig.CleanResidualUserData();
            await _tabManager.CreateTabAsync("about:blank", true);
            UpdateRamDisplay();
        };
    }

    private Button CreateFlatButton(string text, string toolTip, int width = 34)
    {
        var btn = new Button
        {
            Text = text,
            Width = width,
            Height = 32,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(45, 45, 52),
            ForeColor = Color.White,
            Cursor = Cursors.Hand,
            Margin = new Padding(2, 0, 2, 0)
        };
        btn.FlatAppearance.BorderSize = 0;
        var tt = new ToolTip();
        tt.SetToolTip(btn, toolTip);
        return btn;
    }

    private Button CreatePillButton(string text, Color backColor, string toolTip)
    {
        var btn = new Button
        {
            Text = text,
            AutoSize = true,
            Height = 32,
            FlatStyle = FlatStyle.Flat,
            BackColor = backColor,
            ForeColor = Color.White,
            Cursor = Cursors.Hand,
            Padding = new Padding(6, 0, 6, 0),
            Margin = new Padding(3, 0, 3, 0),
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
        };
        btn.FlatAppearance.BorderSize = 0;
        var tt = new ToolTip();
        tt.SetToolTip(btn, toolTip);
        return btn;
    }

    private void OnActiveTabChanged(BrowserTab? tab)
    {
        if (tab == null) return;

        tab.StateChanged -= OnTabStateChanged;
        tab.StateChanged += OnTabStateChanged;

        _txtUrl.Text = (tab.Url == "about:blank") ? "" : tab.Url;
        Text = $"{tab.Title} - {BrowserConfig.AppName}";
        _lblStatus.Text = tab.IsLoading ? "Yükleniyor..." : "Hazır";

        RenderTabStrip();
        UpdateNavigationButtons();
    }

    private void OnTabStateChanged(BrowserTab tab)
    {
        if (_isClosing || IsDisposed || !IsHandleCreated) return;

        if (_tabManager.ActiveTab == tab)
        {
            if (InvokeRequired)
            {
                try
                {
                    Invoke(() =>
                    {
                        if (_isClosing || IsDisposed) return;
                        _txtUrl.Text = (tab.Url == "about:blank") ? "" : tab.Url;
                        Text = $"{tab.Title} - {BrowserConfig.AppName}";
                        _lblStatus.Text = tab.IsLoading ? "Yükleniyor..." : "Hazır";
                        UpdateNavigationButtons();
                    });
                }
                catch { }
            }
            else
            {
                _txtUrl.Text = (tab.Url == "about:blank") ? "" : tab.Url;
                Text = $"{tab.Title} - {BrowserConfig.AppName}";
                _lblStatus.Text = tab.IsLoading ? "Yükleniyor..." : "Hazır";
                UpdateNavigationButtons();
            }
        }
        RenderTabStrip();
    }

    private void RenderTabStrip()
    {
        if (_isClosing || IsDisposed || !IsHandleCreated) return;

        if (InvokeRequired)
        {
            try
            {
                Invoke(RenderTabStrip);
            }
            catch { }
            return;
        }

        _tabStrip.SuspendLayout();
        _tabStrip.Controls.Clear();

        foreach (var tab in _tabManager.Tabs)
        {
            bool isActive = tab == _tabManager.ActiveTab;
            var tabBtn = new Button
            {
                Height = 26,
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                BackColor = isActive ? Color.FromArgb(45, 45, 52) : Color.FromArgb(24, 24, 27),
                ForeColor = isActive ? Color.White : Color.FromArgb(161, 161, 170),
                Cursor = Cursors.Hand,
                Margin = new Padding(2, 1, 2, 1),
                Padding = new Padding(6, 0, 4, 0)
            };
            tabBtn.FlatAppearance.BorderSize = 0;

            string titleDisplay = string.IsNullOrWhiteSpace(tab.Title) ? "Yeni Sekme" : tab.Title;
            if (titleDisplay.Length > 20) titleDisplay = titleDisplay.Substring(0, 18) + "...";

            string statusIcon = tab.IsHibernated ? " [💤]" : (tab.IsSuspended ? " [❄️]" : (tab.IsLoading ? " [↻]" : ""));
            tabBtn.Text = $"{titleDisplay}{statusIcon} ✕";

            string stateDesc = tab.IsHibernated ? "Uyutuldu (RAM serbest bırakıldı)" : (tab.IsSuspended ? "Donduruldu (İşlemci ve JS duraklatıldı)" : "Aktif");
            var tt = new ToolTip();
            tt.SetToolTip(tabBtn, $"{tab.Title}\n{tab.Url}\nDurum: {stateDesc}\nSol tık: Sekmeye Geç | Sağ kenar ✕: Kapat");

            tabBtn.Click += async (s, e) =>
            {
                var me = e as MouseEventArgs;
                // If clicked near the close button '✕' (last 20px)
                if (me != null && me.X > tabBtn.Width - 25)
                {
                    await _tabManager.CloseTabAsync(tab);
                }
                else
                {
                    await _tabManager.SwitchToTabAsync(tab);
                }
            };

            _tabStrip.Controls.Add(tabBtn);
        }

        _tabStrip.ResumeLayout();
    }

    private void UpdateNavigationButtons()
    {
        var activeTab = _tabManager.ActiveTab;
        bool canGoBack = activeTab?.WebViewControl?.CanGoBack ?? false;
        bool canGoForward = activeTab?.WebViewControl?.CanGoForward ?? false;

        _btnBack.Enabled = canGoBack;
        _btnForward.Enabled = canGoForward;
    }

    private void NavigateBack()
    {
        var tab = _tabManager.ActiveTab;
        if (tab?.WebViewControl != null && tab.WebViewControl.CanGoBack)
        {
            tab.WebViewControl.GoBack();
        }
    }

    private void NavigateForward()
    {
        var tab = _tabManager.ActiveTab;
        if (tab?.WebViewControl != null && tab.WebViewControl.CanGoForward)
        {
            tab.WebViewControl.GoForward();
        }
    }

    private void Reload()
    {
        var tab = _tabManager.ActiveTab;
        if (tab?.WebViewControl != null)
        {
            tab.WebViewControl.Reload();
        }
    }

    private void NavigateToInput()
    {
        string input = _txtUrl.Text.Trim();
        if (string.IsNullOrWhiteSpace(input)) return;

        string targetUrl;
        if (input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            input.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            input.StartsWith("about:", StringComparison.OrdinalIgnoreCase))
        {
            targetUrl = input;
        }
        else if (input.Contains('.') && !input.Contains(' '))
        {
            targetUrl = "https://" + input;
        }
        else
        {
            targetUrl = string.Format(BrowserConfig.SearchUrlFormat, Uri.EscapeDataString(input));
        }

        NavigateToUrl(targetUrl);
    }

    private void NavigateToUrl(string targetUrl)
    {
        if (string.IsNullOrWhiteSpace(targetUrl)) return;

        var tab = _tabManager.ActiveTab;
        if (tab?.WebViewControl?.CoreWebView2 != null)
        {
            tab.WebViewControl.CoreWebView2.Navigate(targetUrl);
        }
        else if (tab != null)
        {
            tab.Url = targetUrl;
            _ = _tabManager.SwitchToTabAsync(tab);
        }
        else
        {
            _ = _tabManager.CreateTabAsync(targetUrl, true);
        }
    }

    private void RenderBookmarksBar()
    {
        if (_isClosing || IsDisposed || !IsHandleCreated) return;

        if (InvokeRequired)
        {
            try
            {
                Invoke(RenderBookmarksBar);
            }
            catch { }
            return;
        }

        _bookmarksBar.SuspendLayout();
        _bookmarksBar.Controls.Clear();

        // 1. "+ Ekle" button at start
        var btnAddBookmark = new Button
        {
            Text = "⭐ + Ekle",
            Height = 22,
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(39, 39, 42),
            ForeColor = Color.FromArgb(244, 244, 245),
            Cursor = Cursors.Hand,
            Margin = new Padding(2, 2, 6, 2),
            Padding = new Padding(5, 0, 5, 0),
            Font = new Font("Segoe UI", 8F, FontStyle.Bold)
        };
        btnAddBookmark.FlatAppearance.BorderSize = 0;
        var addTip = new ToolTip();
        addTip.SetToolTip(btnAddBookmark, "Mevcut sayfayı yer işaretlerine ekle (Kısayol: Ctrl+D)");
        btnAddBookmark.Click += (s, e) => AddCurrentPageToBookmarks();
        _bookmarksBar.Controls.Add(btnAddBookmark);

        // 2. Bookmark items
        var bookmarks = BookmarkManager.Instance.Bookmarks;
        foreach (var bookmark in bookmarks)
        {
            var bmItem = bookmark;
            var bmBtn = new Button
            {
                Text = $"{bmItem.IconEmoji} {bmItem.Title}",
                Height = 22,
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(30, 30, 34),
                ForeColor = Color.FromArgb(228, 228, 231),
                Cursor = Cursors.Hand,
                Margin = new Padding(2, 2, 2, 2),
                Padding = new Padding(5, 0, 5, 0),
                Font = new Font("Segoe UI", 8F, FontStyle.Regular)
            };
            bmBtn.FlatAppearance.BorderSize = 0;

            var tip = new ToolTip();
            tip.SetToolTip(bmBtn, $"{bmItem.Title}\n{bmItem.Url}\nSol tık: Sayfayı Aç\nSağ tık: Menü");

            // Context Menu for right-click options
            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Yeni Sekmede Aç", null, async (s, e) =>
            {
                await _tabManager.CreateTabAsync(bmItem.Url, true);
            });
            contextMenu.Items.Add("Bağlantıyı Kopyala", null, (s, e) =>
            {
                try { Clipboard.SetText(bmItem.Url); } catch { }
            });
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add("Yer İşaretini Sil", null, (s, e) =>
            {
                BookmarkManager.Instance.RemoveBookmark(bmItem);
            });

            bmBtn.ContextMenuStrip = contextMenu;

            bmBtn.Click += (s, e) =>
            {
                var me = e as MouseEventArgs;
                if (me != null && me.Button == MouseButtons.Right) return;
                NavigateToUrl(bmItem.Url);
            };

            _bookmarksBar.Controls.Add(bmBtn);
        }

        _bookmarksBar.ResumeLayout();
    }

    private void AddCurrentPageToBookmarks()
    {
        var activeTab = _tabManager.ActiveTab;
        string defaultUrl = (activeTab != null && activeTab.Url != "about:blank") ? activeTab.Url : _txtUrl.Text.Trim();
        string defaultTitle = (activeTab != null && !string.IsNullOrWhiteSpace(activeTab.Title)) ? activeTab.Title : defaultUrl;

        if (string.IsNullOrWhiteSpace(defaultUrl) || defaultUrl.Equals("about:blank", StringComparison.OrdinalIgnoreCase))
        {
            defaultUrl = "https://";
            defaultTitle = "Yeni Yer İşareti";
        }

        using var prompt = new Form
        {
            Width = 460,
            Height = 220,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            Text = "Yer İşareti Ekle",
            StartPosition = FormStartPosition.CenterParent,
            BackColor = Color.FromArgb(24, 24, 27),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false
        };

        var lblTitle = new Label { Text = "Başlık:", Left = 20, Top = 20, Width = 80, ForeColor = Color.FromArgb(212, 212, 216) };
        var txtTitle = new TextBox
        {
            Left = 100, Top = 18, Width = 320,
            Text = defaultTitle,
            BackColor = Color.FromArgb(39, 39, 42),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };

        var lblUrl = new Label { Text = "URL Adresi:", Left = 20, Top = 65, Width = 80, ForeColor = Color.FromArgb(212, 212, 216) };
        var txtBookmarkUrl = new TextBox
        {
            Left = 100, Top = 63, Width = 320,
            Text = defaultUrl,
            BackColor = Color.FromArgb(39, 39, 42),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };

        var btnSave = new Button
        {
            Text = "Kaydet",
            Left = 240,
            Top = 115,
            Width = 85,
            Height = 32,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(13, 148, 136),
            ForeColor = Color.White,
            DialogResult = DialogResult.OK,
            Cursor = Cursors.Hand
        };
        btnSave.FlatAppearance.BorderSize = 0;

        var btnCancel = new Button
        {
            Text = "İptal",
            Left = 335,
            Top = 115,
            Width = 85,
            Height = 32,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(63, 63, 70),
            ForeColor = Color.White,
            DialogResult = DialogResult.Cancel,
            Cursor = Cursors.Hand
        };
        btnCancel.FlatAppearance.BorderSize = 0;

        prompt.Controls.Add(lblTitle);
        prompt.Controls.Add(txtTitle);
        prompt.Controls.Add(lblUrl);
        prompt.Controls.Add(txtBookmarkUrl);
        prompt.Controls.Add(btnSave);
        prompt.Controls.Add(btnCancel);
        prompt.AcceptButton = btnSave;
        prompt.CancelButton = btnCancel;

        if (prompt.ShowDialog(this) == DialogResult.OK)
        {
            string finalTitle = txtTitle.Text.Trim();
            string finalUrl = txtBookmarkUrl.Text.Trim();
            if (!string.IsNullOrWhiteSpace(finalUrl))
            {
                if (!finalUrl.Contains("://") && !finalUrl.StartsWith("about:"))
                {
                    finalUrl = "https://" + finalUrl;
                }
                BookmarkManager.Instance.AddBookmark(finalTitle, finalUrl);
                _lblStatus.Text = $"⭐ Yer işareti eklendi: {finalTitle}";
            }
        }
    }

    private void ToggleAdBlock()
    {
        var engine = AdBlockEngine.Instance;
        engine.IsEnabled = !engine.IsEnabled;
        UpdateAdBlockButtonText();
    }

    private void UpdateAdBlockButtonText()
    {
        var engine = AdBlockEngine.Instance;
        if (engine.IsEnabled)
        {
            _btnAdBlock.Text = $"🛡️ Reklam: Açık ({engine.BlockedRequests})";
            _btnAdBlock.BackColor = Color.FromArgb(22, 101, 52);
        }
        else
        {
            _btnAdBlock.Text = "🛡️ Reklam: Kapalı";
            _btnAdBlock.BackColor = Color.FromArgb(127, 29, 29);
        }
    }

    private void UpdateRamDisplay()
    {
        if (_isClosing || IsDisposed || !IsHandleCreated) return;
        try
        {
            var snap = ProcessMemoryTracker.CaptureSnapshot();
            _lblRamSummary.Text =
                $"Fiziksel RAM: {snap.TotalWorkingSetMB:F1} MB | Host: {snap.HostWorkingSetMB:F1} MB | Web: {snap.WebViewWorkingSetMB:F1} MB ({snap.WebViewProcesses.Count} süreç) | Commit: {snap.TotalPrivateMB:F1} MB";
        }
        catch
        {
            // Transient error safe ignore
        }
    }

    private async Task StartBenchmarkAsync()
    {
        _btnBenchmark.Enabled = false;
        _lblStatus.Text = "⚡ RAM Benchmark yürütülüyor...";

        try
        {
            var runner = new BenchmarkRunner(_tabManager, msg =>
            {
                if (InvokeRequired) Invoke(() => _lblStatus.Text = msg);
                else _lblStatus.Text = msg;
            });

            var results = await runner.RunAllBenchmarksAsync();
            string summary = BenchmarkRunner.GenerateSummaryTable(results);

            MessageBox.Show(
                this,
                summary,
                "Sikrowser RAM Performans Raporu",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Benchmark hatası: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnBenchmark.Enabled = true;
            _lblStatus.Text = "Hazır";
            UpdateRamDisplay();
        }
    }

    private void OnFormKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.T)
        {
            e.SuppressKeyPress = true;
            _ = _tabManager.CreateTabAsync("about:blank", true);
        }
        else if (e.Control && e.KeyCode == Keys.W)
        {
            e.SuppressKeyPress = true;
            if (_tabManager.ActiveTab != null)
            {
                _ = _tabManager.CloseTabAsync(_tabManager.ActiveTab);
            }
        }
        else if (e.Control && e.KeyCode == Keys.L)
        {
            e.SuppressKeyPress = true;
            _txtUrl.Focus();
            _txtUrl.SelectAll();
        }
        else if (e.Alt && e.KeyCode == Keys.Left)
        {
            e.SuppressKeyPress = true;
            NavigateBack();
        }
        else if (e.Alt && e.KeyCode == Keys.Right)
        {
            e.SuppressKeyPress = true;
            NavigateForward();
        }
        else if (e.KeyCode == Keys.F5 || (e.Control && e.KeyCode == Keys.R))
        {
            e.SuppressKeyPress = true;
            Reload();
        }
        else if (e.Control && e.KeyCode == Keys.D)
        {
            e.SuppressKeyPress = true;
            AddCurrentPageToBookmarks();
        }
        else if (e.Control && e.KeyCode == Keys.B)
        {
            e.SuppressKeyPress = true;
            ToggleBookmarksBar();
        }
    }

    private void OnHighResourceTabDetected(BrowserTab tab, double memoryMB)
    {
        if (_isClosing || IsDisposed || !IsHandleCreated) return;

        try
        {
            BeginInvoke(() =>
            {
                if (_isClosing || IsDisposed) return;
                _highResourceTab = tab;
                string title = string.IsNullOrWhiteSpace(tab.Title) ? tab.Url : tab.Title;
                if (title.Length > 28) title = title.Substring(0, 25) + "...";
                _lblResourceWarning.Text = $"⚠️ Yüksek Kaynak Uyarısı: \"{title}\" sekmesi {memoryMB:F0} MB RAM tüketiyor!";
                _pnlResourceWarning.Visible = true;
            });
        }
        catch { }
    }

    private void ToggleBookmarksBar()
    {
        if (!_bookmarksRendered)
        {
            _bookmarksRendered = true;
            RenderBookmarksBar();
        }

        _bookmarksBar.Visible = !_bookmarksBar.Visible;
        _lblStatus.Text = _bookmarksBar.Visible ? "⭐ Yer imleri çubuğu gösterildi" : "Yer imleri çubuğu gizlendi";
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _isClosing = true;

        // Instant exit watchdog: forces process exit within 200ms
        ThreadPool.QueueUserWorkItem(_ =>
        {
            Thread.Sleep(150);
            Environment.Exit(0);
        });

        try
        {
            _ramTimer.Stop();
            _ramTimer.Dispose();
        }
        catch { }

        try
        {
            AdaptivePerformanceManager.Instance.Dispose();
        }
        catch { }

        try
        {
            _tabManager.Dispose();
        }
        catch { }

        base.OnFormClosing(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);

        // Immediate, clean shutdown so no lingering processes remain in Task Manager
        Environment.Exit(0);
    }
}

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using ImagoLib.Models;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using SkiaSharp;

namespace ImagoAdmin {

    /// <summary>
    /// Hlavní okno ImagoAdmin. Схема работы:
    ///  - слева дерево страниц (● N — неопубликованные změny);
    ///  - в центре náhled: страница сайта в режиме черновика (?nahled=<ключ>, см. BaseController на сайте);
    ///    клик по тексту или блоку открывает его справа;
    ///  - справа правка: тексты и стиль, блоки (по шаблонам BlockTemplates), фото;
    ///    всё сохраняется в черновик и попадает на сайт только по «Publikovat stránku» (PageDraft).
    /// </summary>
    public partial class MainWindow : Window {
        public ObservableCollection<Pages> PagesList { get; set; } = new ObservableCollection<Pages>();
        public ObservableCollection<Meeting> MeetingList { get; set; } = new ObservableCollection<Meeting>();
        public ObservableCollection<Noviny> NovinkyList { get; set; } = new ObservableCollection<Noviny>();

        public class ColorInfo {
            public string Name { get; set; } = "";
            public string Hex { get; set; } = "";
            public Color Color => (Color)ColorConverter.ConvertFromString(Hex);
            public SolidColorBrush Brush => new SolidColorBrush(Color);
        }

        /// <summary>Блок страницы в списке «Bloky» (контакт, spolupracovník, video, параметр прибора…).</summary>
        public class BlockItem {
            public BlockInstance Instance { get; init; } = null!;
            public BlockTemplate Template => Instance.Template;
            public int Number => Instance.Number;
            public string Label => Instance.Label;
            public string Key => Instance.Key;
            public IReadOnlyDictionary<string, string> FieldKeys => Instance.FieldKeys;
            public string TemplateTitle => $"{Template.Title} {Number}";
        }

        /// <summary>Поле в форме блока.</summary>
        public class BlockFieldValue {
            public string Name { get; init; } = "";
            public string Caption { get; init; } = "";
            public string Hint { get; init; } = "";
            public bool Multiline { get; init; }
            public string Value { get; set; } = "";
        }

        // Страницы с особым поведением (номера из таблицы Pages)
        private static class PageIds {
            public const int Carousel = 3;          // Úvod: фото карусели (обрезаются до 1200×350), общие тексты (телефон, e-mail)
            public const int DevicesParent = 5;     // Přístroje Diacom: подстраницы — страницы приборов
            public const int News = 8;              // Novinky
            public const int Meetings = 38;         // Mítinky
        }

        /// <summary>Сайт, который редактирует админка. Для проверки на локальном сайте: переменная окружения IMAGO_ADMIN_SITE (см. Properties/launchSettings.json).</summary>
        private static readonly string SiteUrl = (Environment.GetEnvironmentVariable("IMAGO_ADMIN_SITE") ?? "https://imagodt.cz").TrimEnd('/');

        private string _previewToken = "";
        private Pages? _currentPage;
        private List<DictionaryEntryForText> _entries = new();                 // черновик текстов страницы
        private Dictionary<string, string> _published = new();                // опубликованные тексты страницы (для «Vrátit původní»)
        private DictionaryEntryForText? _currentEntry;
        private TextStyle? _currentStyle;
        private bool _textDirty;
        private bool _loadingText;
        private bool _loadingStyle;
        private bool _selectingEntry;

        private List<BlockTemplate> _templates = new();   // виды блоков текущей страницы
        private List<BlockItem> _blocks = new();          // блоки текущей страницы
        private BlockTemplate? _formTemplate;             // открытая форма блока
        private BlockItem? _formBlock;                    // null — новый блок

        private double? _restoreScrollY;
        private string? _highlightKey;

        public MainWindow() {
            try {
                InitializeComponent();
                DataContext = this;
                Title = $"IMAGO Admin v{Assembly.GetExecutingAssembly().GetName().Version}" + (SiteUrl.Contains("imagodt.cz") ? "" : $"  —  {SiteUrl}");

                TextColorComboBox.ItemsSource = new List<ColorInfo> {
                    new ColorInfo { Name = "Černá", Hex = "#FF000000" },
                    new ColorInfo { Name = "Tmavě modrá (web)", Hex = "#FF1D4F99" },
                    new ColorInfo { Name = "Modrá (web)", Hex = "#FF265FBF" },
                    new ColorInfo { Name = "Červená", Hex = "#FFFF0000" },
                    new ColorInfo { Name = "Zelená", Hex = "#FF008000" },
                    new ColorInfo { Name = "Modrá", Hex = "#FF0000FF" },
                    new ColorInfo { Name = "Žlutá", Hex = "#FFFFFF00" },
                    new ColorInfo { Name = "Oranžová", Hex = "#FFFFA500" },
                    new ColorInfo { Name = "Fialová", Hex = "#FF800080" },
                    new ColorInfo { Name = "Šedá", Hex = "#FF808080" },
                    new ColorInfo { Name = "Bílá", Hex = "#FFFFFFFF" }
                };

                TextEditor.TextChanged += TextEditor_TextChanged;
                Closing += MainWindow_Closing;

                // Черновик при запуске больше НЕ перезаписывается с сайта: сохранённое, но не опубликованное остаётся.
                ReloadPagesTree();
                InitializeWebViewAsync();
                CheckForUpdatesAsync().ConfigureAwait(false);
                UpdatePageControls(null);
            }
            catch (Exception ex) {
                ShowError("Chyba při spuštění", ex);
            }
        }

        private static void ShowError(string title, Exception ex) {
            MessageBox.Show($"{title}:\n{ex.Message}", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        #region Обновление программы (GitHub Releases)

        private const string GitHubRepo = "DanilDiacom/ImagoWebApplication";

        public async Task CheckForUpdatesAsync() {
            try {
                if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable()) {
                    MessageBox.Show("Není připojení k internetu. Zkontrolujte síť.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                Version currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
                (string latestTag, string downloadUrl) = await GetLatestReleaseInfoAsync();

                if (latestTag == null || downloadUrl == null) {
                    return;
                }

                Version latestVersion = new Version(latestTag.TrimStart('v') + ".0");

                if (latestVersion > currentVersion) {
                    var result = MessageBox.Show(
                        $"Je k dispozici nová verze {latestVersion}. Nainstalovat aktualizaci?",
                        "Aktualizace",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes) {
                        await DownloadAndInstallUpdate(downloadUrl);
                    }
                }
            }
            catch (Exception ex) {
                MessageBox.Show($"Chyba při kontrole aktualizací: {ex.Message}", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task<(string latestTag, string downloadUrl)> GetLatestReleaseInfoAsync() {
            string apiUrl = $"https://api.github.com/repos/{GitHubRepo}/releases/latest";

            using (var client = new HttpClient()) {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("ImagoAdmin-Updater/1.0");

                HttpResponseMessage response = await client.GetAsync(apiUrl);
                if (!response.IsSuccessStatusCode) return (null, null);

                if (response.StatusCode == HttpStatusCode.NotFound) {
                    return (null, null); // Нет релизов
                }

                string json = await response.Content.ReadAsStringAsync();
                using (JsonDocument doc = JsonDocument.Parse(json)) {
                    string tag = doc.RootElement.GetProperty("tag_name").GetString();
                    foreach (var asset in doc.RootElement.GetProperty("assets").EnumerateArray()) {
                        string fileName = asset.GetProperty("name").GetString();
                        if (fileName.EndsWith(".msi")) return (tag, asset.GetProperty("browser_download_url").GetString());
                    }
                }
            }
            return (null, null);
        }

        public class DownloadProgressWindow : Window {
            public ProgressBar ProgressBar { get; } = new ProgressBar { Minimum = 0, Maximum = 100, Height = 20 };
            public TextBlock StatusText { get; } = new TextBlock { Margin = new Thickness(0, 10, 0, 0) };

            public DownloadProgressWindow() {
                Title = "Stahování aktualizace";
                Width = 350;
                Height = 160;
                WindowStartupLocation = WindowStartupLocation.CenterScreen;

                var stackPanel = new StackPanel { Margin = new Thickness(10) };
                stackPanel.Children.Add(ProgressBar);
                stackPanel.Children.Add(StatusText);

                Content = stackPanel;
            }
        }

        private async Task DownloadAndInstallUpdate(string url) {
            string tempFile = Path.Combine(Path.GetTempPath(), "ImagoAdmin_Update.msi");

            try {
                // Создаем окно прогресса
                var progressWindow = new DownloadProgressWindow();
                progressWindow.StatusText.Text = "Příprava stahování...";
                progressWindow.Show();

                using (var client = new HttpClient()) {
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("ImagoAdmin-Updater/1.0");

                    using (HttpResponseMessage response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead)) {
                        response.EnsureSuccessStatusCode();

                        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                        var receivedBytes = 0L;
                        var buffer = new byte[8192];

                        using (var contentStream = await response.Content.ReadAsStreamAsync())
                        using (var fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write)) {
                            progressWindow.StatusText.Text = "Stahování aktualizace...";

                            int bytesRead;
                            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0) {
                                await fs.WriteAsync(buffer, 0, bytesRead);

                                receivedBytes += bytesRead;
                                if (totalBytes > 0) {
                                    var progressPercentage = (int)((double)receivedBytes / totalBytes * 100);
                                    progressWindow.ProgressBar.Value = progressPercentage;
                                    progressWindow.StatusText.Text = $"Staženo: {progressPercentage}% ({receivedBytes / 1024} KB / {totalBytes / 1024} KB)";
                                }

                                // Даем возможность обработать сообщения UI
                                await Task.Delay(1);
                                Application.Current.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
                            }
                        }
                    }
                }

                progressWindow.StatusText.Text = "Instalace aktualizace...";
                progressWindow.ProgressBar.IsIndeterminate = true;

                Process process = new Process {
                    StartInfo = new ProcessStartInfo {
                        FileName = "msiexec",
                        Arguments = $"/i \"{tempFile}\" /quiet",
                        Verb = "runas",
                        UseShellExecute = true
                    }
                };

                process.Start();
                await Task.Run(() => process.WaitForExit());

                if (process.ExitCode == 0) {
                    progressWindow.Close();
                    Application.Current.Shutdown();
                }
                else {
                    progressWindow.Close();
                    MessageBox.Show($"Chyba instalace. Kód: {process.ExitCode}", "Chyba instalace", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex) {
                MessageBox.Show($"Chyba při stahování aktualizace: {ex.Message}", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally {
                if (File.Exists(tempFile)) {
                    try { File.Delete(tempFile); } catch { }
                }
            }
        }

        #endregion

        #region Náhled (WebView2) — страница сайта в режиме черновика

        // Скрипт для каждой страницы náhledu: подсветка редактируемого, клик по тексту/блоку -> сообщение в админку,
        // ссылки и формы не работают (чтобы не уйти со страницы), меню и подвал не кликаются
        private const string PreviewScript = @"
(function () {
    function setup() {
        var css = document.createElement('style');
        css.textContent =
            '[data-key],[data-value]{cursor:pointer}' +
            '[data-key]:hover,[data-value]:hover{outline:2px dashed #265fbf;outline-offset:2px}' +
            '[data-block]:hover{box-shadow:0 0 0 2px rgba(38,95,191,.25);border-radius:6px}' +
            '.imago-selected{outline:3px solid #265fbf !important;outline-offset:3px;background:rgba(38,95,191,.06)}' +
            '#ic-chat{display:none !important}';
        document.head.appendChild(css);
        ['.navbar-custom', '#box-bottom-container', 'header.header'].forEach(function (s) {
            var el = document.querySelector(s); if (el) el.style.pointerEvents = 'none';
        });
        document.querySelectorAll('form').forEach(function (f) { f.addEventListener('submit', function (e) { e.preventDefault(); }, true); });
    }
    document.addEventListener('click', function (e) {
        if (!window.chrome || !window.chrome.webview) return;
        var link = e.target.closest('a'); if (link) e.preventDefault();
        var el = e.target.closest('[data-key],[data-value]');
        var block = e.target.closest('[data-block]');
        if (!el && !block) return;
        e.preventDefault(); e.stopPropagation();
        var key = null;
        if (el) key = (el.tagName === 'SPAN' && el.getAttribute('data-value')) ? el.getAttribute('data-value') : (el.getAttribute('data-key') || el.getAttribute('data-value'));
        window.chrome.webview.postMessage(JSON.stringify({ key: key, block: block ? block.getAttribute('data-block') : null }));
    }, true);
    window.__imagoSelect = function (key, block) {
        document.querySelectorAll('.imago-selected').forEach(function (x) { x.classList.remove('imago-selected'); });
        var target = null;
        if (key) document.querySelectorAll('[data-key=""' + key + '""],[data-value=""' + key + '""]').forEach(function (x) { x.classList.add('imago-selected'); target = target || x; });
        if (!target && block) { target = document.querySelector('[data-block=""' + block + '""]'); if (target) target.classList.add('imago-selected'); }
        if (target) {
            var r = target.getBoundingClientRect();
            if (r.top < 0 || r.bottom > window.innerHeight) target.scrollIntoView({ block: 'center', behavior: 'smooth' });
        }
    };
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', setup); else setup();
})();";

        private async void InitializeWebViewAsync() {
            try {
                string webView2DataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ImagoAdmin", "WebView2Data");
                Directory.CreateDirectory(webView2DataPath);

                var env = await CoreWebView2Environment.CreateAsync(browserExecutableFolder: null, userDataFolder: webView2DataPath);
                await webView.EnsureCoreWebView2Async(env);

                webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                webView.CoreWebView2.Settings.IsZoomControlEnabled = false;
                webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(PreviewScript);
                webView.CoreWebView2.WebMessageReceived += WebView_WebMessageReceived;
                webView.CoreWebView2.NavigationCompleted += WebView_NavigationCompleted;

                _previewToken = await Task.Run(SiteSettings.GetPreviewToken);
                webView.CoreWebView2.Navigate(SiteUrl + "/Home/Index?nahled=" + _previewToken);
            }
            catch (Exception ex) {
                ShowError("Náhled stránky se nepodařilo spustit (WebView2)", ex);
            }
        }

        private string PreviewUrl(Pages page) {
            var url = (page.ParentId == PageIds.DevicesParent || page.Id == PageIds.DevicesParent) ? $"{page.Url}?id={page.Id}" : page.Url;
            return SiteUrl + url + (url.Contains('?') ? "&" : "?") + "nahled=" + _previewToken;
        }

        private void NavigatePreview() {
            if (_currentPage == null || webView?.CoreWebView2 == null || string.IsNullOrEmpty(_previewToken)) return;
            webView.CoreWebView2.Navigate(PreviewUrl(_currentPage));
        }

        /// <summary>Перезагрузить náhled (после изменения блоков, фото, стиля по умолчанию), сохранив прокрутку.</summary>
        private async Task ReloadPreviewAsync() {
            if (webView?.CoreWebView2 == null) return;
            try {
                var y = await webView.CoreWebView2.ExecuteScriptAsync("window.scrollY");
                if (double.TryParse(y, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var scroll)) _restoreScrollY = scroll;
            }
            catch { }
            NavigatePreview();
        }

        private async void WebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e) {
            try {
                if (_restoreScrollY.HasValue) {
                    await webView.CoreWebView2.ExecuteScriptAsync($"window.scrollTo(0, {_restoreScrollY.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)})");
                    _restoreScrollY = null;
                }
                if (_highlightKey != null) await HighlightAsync(_highlightKey, null, scroll: false);
            }
            catch { }
        }

        private async Task HighlightAsync(string? key, string? block, bool scroll = true) {
            _highlightKey = key;
            if (webView?.CoreWebView2 == null) return;
            try {
                await webView.CoreWebView2.ExecuteScriptAsync($"window.__imagoSelect && window.__imagoSelect({JsonSerializer.Serialize(key)}, {JsonSerializer.Serialize(block)})");
            }
            catch { }
        }

        /// <summary>Клик по тексту или блоку в náhledu — открываем его справа.</summary>
        private void WebView_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e) {
            try {
                using var doc = JsonDocument.Parse(e.TryGetWebMessageAsString());
                var key = doc.RootElement.TryGetProperty("key", out var k) && k.ValueKind == JsonValueKind.String ? k.GetString() : null;
                var block = doc.RootElement.TryGetProperty("block", out var b) && b.ValueKind == JsonValueKind.String ? b.GetString() : null;

                // поле блока или сам блок -> вкладка «Bloky»
                var clickedBlock = _blocks.FirstOrDefault(x => key != null && x.FieldKeys.Values.Contains(key))
                                   ?? _blocks.FirstOrDefault(x => block != null && x.Instance.HasDataBlock && x.Key == block);
                if (clickedBlock != null) {
                    EditorTabs.SelectedItem = BlocksTab;
                    BlockList.SelectedItem = clickedBlock;
                    BlockList.ScrollIntoView(clickedBlock);
                    return;
                }

                if (key == null) return;
                var entry = _entries.FirstOrDefault(x => x.EntryKey == key);
                if (entry == null) {
                    MessageBox.Show("Tento text zatím nejde upravit v administraci.", "Úprava textu", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                EditorTabs.SelectedItem = TextsTab;
                if (!string.IsNullOrEmpty(EntrySearch.Text)) EntrySearch.Text = "";
                EntryList.SelectedItem = (EntryList.ItemsSource as IEnumerable<DictionaryEntryForText>)?.FirstOrDefault(x => x.EntryKey == key);
                EntryList.ScrollIntoView(EntryList.SelectedItem);
                TextEditor.Focus();
            }
            catch (Exception ex) {
                Debug.WriteLine("Náhled: " + ex.Message);
            }
        }

        /// <summary>Живое обновление текста в náhledu, пока его печатают.</summary>
        private async Task UpdatePreviewTextAsync(string key, string text) {
            if (webView?.CoreWebView2 == null) return;
            var script = $@"(function(k, t) {{
    document.querySelectorAll('[data-key=""' + k + '""],[data-value=""' + k + '""]').forEach(function (el) {{
        if (el.tagName === 'LI' && el.querySelector('[data-key=""' + k + '""],[data-value=""' + k + '""]')) return; // текст во вложенном элементе
        el.innerHTML = t;
    }});
}})({JsonSerializer.Serialize(key)}, {JsonSerializer.Serialize(text)});";
            try { await webView.CoreWebView2.ExecuteScriptAsync(script); } catch { }
        }

        /// <summary>Стиль текста в náhledu (null -> оформление страницы по умолчанию).</summary>
        private async Task UpdatePreviewStyleAsync(string key, TextStyle? style) {
            if (webView?.CoreWebView2 == null) return;
            string Px(string? v) => string.IsNullOrEmpty(v) ? "" : (double.TryParse(v, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _) ? v + "px" : v);
            var css = new Dictionary<string, string> {
                ["fontFamily"] = style?.FontFamily ?? "",
                ["fontSize"] = Px(style?.FontSize),
                ["fontWeight"] = style?.FontWeight ?? "",
                ["fontStyle"] = style?.FontStyle ?? "",
                ["textDecoration"] = style?.TextDecoration ?? "",
                ["color"] = NormalizeColor(style?.TextColor),
            };
            var script = $@"(function(k, css) {{
    document.querySelectorAll('[data-key=""' + k + '""],[data-value=""' + k + '""]').forEach(function (el) {{
        if (el.tagName === 'LI' && el.querySelector('[data-key=""' + k + '""],[data-value=""' + k + '""]')) return;
        Object.keys(css).forEach(function (p) {{ el.style[p] = css[p]; }});
    }});
}})({JsonSerializer.Serialize(key)}, {JsonSerializer.Serialize(css)});";
            try { await webView.CoreWebView2.ExecuteScriptAsync(script); } catch { }
        }

        private static string NormalizeColor(string? color) {
            if (string.IsNullOrEmpty(color)) return "";
            if (color.Length == 9 && color.StartsWith("#")) return "#" + color.Substring(3);   // #AARRGGBB -> #RRGGBB
            return color;
        }

        #endregion

        #region Страницы, черновик и публикация

        private void ReloadPagesTree() {
            var selectedId = _currentPage?.Id;
            PagesList = Pages.GetPagesHierarchy() ?? new ObservableCollection<Pages>();
            tvPageList.ItemsSource = PagesList;
            if (selectedId.HasValue) _currentPage = AllPages().FirstOrDefault(p => p.Id == selectedId.Value) ?? _currentPage;
            RefreshChangeCounts();
        }

        private IEnumerable<Pages> AllPages() {
            IEnumerable<Pages> Walk(IEnumerable<Pages> list) => list.SelectMany(p => new[] { p }.Concat(Walk(p.SubPages)));
            return Walk(PagesList);
        }

        /// <summary>«● N» у страниц, статус текущей страницы, доступность кнопок публикации.</summary>
        private void RefreshChangeCounts() {
            try {
                var counts = PageDraft.CountChangesByPage();
                foreach (var p in AllPages()) p.PendingChanges = counts.TryGetValue(p.Id, out var n) ? n : 0;
                PublishAllButton.IsEnabled = counts.Count > 0;

                var current = _currentPage != null && counts.TryGetValue(_currentPage.Id, out var c) ? c : 0;
                var pageLoaded = _currentPage != null && _currentPage.Url != "#";
                PublishPageButton.IsEnabled = pageLoaded && current > 0;
                DiscardPageButton.IsEnabled = pageLoaded && current > 0;
                HistoryButton.IsEnabled = pageLoaded;
                if (!pageLoaded) PageStatusText.Text = "";
                else if (current == 0) {
                    PageStatusText.Text = "✔ Vše je publikováno — náhled odpovídá webu.";
                    PageStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x2e, 0x7d, 0x32));
                }
                else {
                    PageStatusText.Text = $"● Nepublikované změny: {current}. Na webu se objeví po „📤 Publikovat stránku“.";
                    PageStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xd9, 0x82, 0x2b));
                }
            }
            catch (Exception ex) {
                Debug.WriteLine("Počet změn: " + ex.Message);
            }
        }

        private async void TreeView_SelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e) {
            if (e.NewValue is not Pages page) return;
            try {
                SaveCurrentTextIfDirty();
                await LoadPageAsync(page);
            }
            catch (Exception ex) {
                ShowError("Stránku se nepodařilo načíst", ex);
            }
        }

        private async Task LoadPageAsync(Pages page) {
            _currentPage = page;
            _currentEntry = null;
            _highlightKey = null;
            UpdatePageControls(page);
            PageTitleText.Text = page.Title;
            CloseBlockForm();
            ClearEditor();

            if (page.Url == "#") {
                // только группа в меню сайта — своей страницы нет
                PageStatusText.Text = "Tato položka je jen skupina v menu — vyberte stránku pod ní.";
                PageStatusText.Foreground = Brushes.Gray;
                EntryList.ItemsSource = null;
                BlockList.ItemsSource = null;
                PhotoList.ItemsSource = null;
                _templates = new();
                _blocks = new();
                BlocksTab.Visibility = Visibility.Collapsed;
                RefreshChangeCounts();
                return;
            }

            _templates = BlockTemplates.ForPage(page.Id, page.ParentId);
            NewBlockButtons.ItemsSource = _templates;
            BlocksTab.Visibility = _templates.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            BlocksTab.Header = _templates.Any(t => t.LabelValueRows) ? "🧩 Parametry" : "🧩 Bloky";
            if (_templates.Count == 0 && EditorTabs.SelectedItem == BlocksTab) EditorTabs.SelectedItem = TextsTab;
            await ReloadPageDataAsync();

            NavigatePreview();
            RefreshChangeCounts();
        }

        /// <summary>Тексты, блоки и фото текущей страницы из черновика.</summary>
        private async Task ReloadPageDataAsync(string? selectBlock = null) {
            if (_currentPage == null) return;
            var pageId = _currentPage.Id;
            var (entries, published, photos) = await Task.Run(() => (
                DictionaryEntryForText.GetEntriesForEditind(pageId).ToList(),
                DictionaryEntryForText.GetEntriesForPage(pageId).GroupBy(x => x.EntryKey).ToDictionary(g => g.Key, g => g.First().ContentText ?? ""),
                DictionaryEntryForImages.GetEntriesForEditing(pageId)));
            _entries = entries;
            _published = published;
            PhotoList.ItemsSource = photos;
            RefreshBlocks(selectBlock);   // до списка текстов: поля блоков в него не попадают
            RefreshTextList();
        }

        private void UpdatePageControls(Pages? page) {
            var isMeetings = page?.Id == PageIds.Meetings;
            var isNews = page?.Id == PageIds.News;
            var isDevice = page?.ParentId == PageIds.DevicesParent;

            AddMeetingButton.Visibility = lv_Meeting.Visibility = EditMeetingButton.Visibility = DeleteMeetingButton.Visibility =
                isMeetings ? Visibility.Visible : Visibility.Collapsed;
            AddNovinyButton.Visibility = lv_Noviny.Visibility = EditNovinyButton.Visibility = DeleteNovinyButton.Visibility =
                isNews ? Visibility.Visible : Visibility.Collapsed;
            AddDeviceButton.Visibility = DeleteDeviceButton.Visibility = isDevice ? Visibility.Visible : Visibility.Collapsed;

            if (isMeetings) LoadMeetingList();
            if (isNews) LoadNovinkyList();
        }

        private async void PublishPageButton_Click(object sender, RoutedEventArgs e) {
            if (_currentPage == null) return;
            try {
                SaveCurrentTextIfDirty();
                var count = PageDraft.CountChanges(_currentPage.Id);
                if (count == 0) {
                    MessageBox.Show("Na této stránce nejsou žádné nepublikované změny.", "Publikovat", MessageBoxButton.OK, MessageBoxImage.Information);
                    RefreshChangeCounts();
                    return;
                }
                if (MessageBox.Show($"Publikovat {count} změn stránky „{_currentPage.Title}“ na web?\n\nPředchozí verze se uloží do historie a půjde vrátit.",
                                    "Publikovat stránku", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

                var pageId = _currentPage.Id;
                await Task.Run(() => PageDraft.Publish(pageId, Environment.UserName));
                await ReloadPageDataAsync();
                RefreshChangeCounts();
                MessageBox.Show("Stránka byla publikována — změny jsou na webu.", "Hotovo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) {
                ShowError("Publikování se nezdařilo", ex);
            }
        }

        private async void PublishAllButton_Click(object sender, RoutedEventArgs e) {
            try {
                SaveCurrentTextIfDirty();
                var counts = PageDraft.CountChangesByPage();
                if (counts.Count == 0) { RefreshChangeCounts(); return; }
                var titles = AllPages().Where(p => counts.ContainsKey(p.Id)).Select(p => $"• {p.Title} ({counts[p.Id]})");
                if (MessageBox.Show("Publikovat změny těchto stránek na web?\n\n" + string.Join("\n", titles),
                                    "Publikovat vše", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

                await Task.Run(() => { foreach (var pageId in counts.Keys) PageDraft.Publish(pageId, Environment.UserName); });
                if (_currentPage != null && _currentPage.Url != "#") await ReloadPageDataAsync();
                RefreshChangeCounts();
                MessageBox.Show("Všechny změny byly publikovány.", "Hotovo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) {
                ShowError("Publikování se nezdařilo", ex);
            }
        }

        private async void MenuButton_Click(object sender, RoutedEventArgs e) {
            try {
                SaveCurrentTextIfDirty();
                var dialog = new MenuWindow(Pages.GetPagesHierarchy()) { Owner = this };
                if (dialog.ShowDialog() != true) return;
                ReloadPagesTree();
                if (_currentPage != null) PageTitleText.Text = AllPages().FirstOrDefault(p => p.Id == _currentPage.Id)?.Title ?? _currentPage.Title;
                await ReloadPreviewAsync();
                MessageBox.Show("Menu bylo uloženo — změny jsou na webu.", "Menu webu", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) {
                ShowError("Menu se nepodařilo otevřít", ex);
            }
        }

        private async void DiscardPageButton_Click(object sender, RoutedEventArgs e) {
            if (_currentPage == null) return;
            if (MessageBox.Show($"Zahodit všechny nepublikované změny stránky „{_currentPage.Title}“?\n\nNávrh bude znovu stejný jako web. Tuto akci nelze vrátit.",
                                "Zahodit změny", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            try {
                _textDirty = false;
                var pageId = _currentPage.Id;
                await Task.Run(() => PageDraft.Discard(pageId));
                CloseBlockForm();
                ClearEditor();
                await ReloadPageDataAsync();
                await ReloadPreviewAsync();
                RefreshChangeCounts();
            }
            catch (Exception ex) {
                ShowError("Změny se nepodařilo zahodit", ex);
            }
        }

        private async void HistoryButton_Click(object sender, RoutedEventArgs e) {
            if (_currentPage == null) return;
            try {
                SaveCurrentTextIfDirty();
                var history = PageDraft.GetHistory(_currentPage.Id);
                var dialog = new HistoryWindow(_currentPage.Title, history) { Owner = this };
                if (dialog.ShowDialog() != true || dialog.SelectedItem == null) return;

                PageDraft.RestoreToDraft(dialog.SelectedItem.Id);
                CloseBlockForm();
                ClearEditor();
                await ReloadPageDataAsync();
                await ReloadPreviewAsync();
                RefreshChangeCounts();
                MessageBox.Show("Verze je v návrhu. Zkontrolujte náhled a klikněte na „📤 Publikovat stránku“.", "Historie", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) {
                ShowError("Verzi se nepodařilo vrátit", ex);
            }
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e) {
            try {
                SaveCurrentTextIfDirty();
                var counts = PageDraft.CountChangesByPage();
                if (counts.Count == 0) return;
                var result = MessageBox.Show($"Na {counts.Count} stránkách jsou nepublikované změny. Zůstanou uložené v návrhu a můžete je publikovat později.\n\nZavřít program?",
                                             "Nepublikované změny", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes) e.Cancel = true;
            }
            catch { }
        }

        #endregion

        #region Texty

        /// <summary>Тексты страницы (без полей блоков — их правят во вкладке «Bloky»), с поиском.</summary>
        private void RefreshTextList() {
            var blockKeys = _blocks.SelectMany(b => b.FieldKeys.Values).ToHashSet();
            var search = EntrySearch.Text.Trim();
            var list = _entries
                .Where(e => !blockKeys.Contains(e.EntryKey))
                .Where(e => search.Length == 0
                            || e.DisplayName.Contains(search, StringComparison.CurrentCultureIgnoreCase)
                            || (e.ContentText ?? "").Contains(search, StringComparison.CurrentCultureIgnoreCase))
                .ToList();
            var selectedKey = _currentEntry?.EntryKey;
            _selectingEntry = true;
            EntryList.ItemsSource = list;
            EntryList.SelectedItem = list.FirstOrDefault(x => x.EntryKey == selectedKey);
            _selectingEntry = false;
        }

        private void EntrySearch_TextChanged(object sender, TextChangedEventArgs e) => RefreshTextList();

        private async void EntryList_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (_selectingEntry) return;
            SaveCurrentTextIfDirty();
            if (EntryList.SelectedItem is not DictionaryEntryForText entry) return;

            _currentEntry = entry;
            EditorFieldName.Text = entry.DisplayName;
            SetEditorHtml(entry.ContentText);
            TextEditor.IsEnabled = SaveTextButton.IsEnabled = StyleExpander.IsEnabled = true;
            var plain = BlockTemplates.IsPlainField(entry.EntryKey);
            FormatToolbar.IsEnabled = !plain;
            PlainFieldHint.Visibility = plain ? Visibility.Visible : Visibility.Collapsed;
            RevertTextButton.IsEnabled = _published.ContainsKey(entry.EntryKey);

            _currentStyle = await Task.Run(() => TextStyle.GetTextStyle(entry.EntryKey, draft: true));
            ApplyStyleToControls(_currentStyle);
            await HighlightAsync(entry.EntryKey, null);
        }

        /// <summary>Текст поля в редактор (форматированный; теги пользователь не видит).</summary>
        private void SetEditorHtml(string? html) {
            _loadingText = true;
            TextEditor.Document = RichTextHtml.ToDocument(html);
            _loadingText = false;
        }

        /// <summary>Текст из редактора как «лёгкий HTML» сайта (для e-mailů, odkazů, telefonů — без форматирования).</summary>
        private string GetEditorHtml() =>
            RichTextHtml.ToHtml(TextEditor.Document, plain: _currentEntry != null && BlockTemplates.IsPlainField(_currentEntry.EntryKey));

        private void TextEditor_TextChanged(object sender, TextChangedEventArgs e) {
            if (_loadingText || _currentEntry == null) return;
            _textDirty = true;
            _ = UpdatePreviewTextAsync(_currentEntry.EntryKey, HtmlLite.Sanitize(GetEditorHtml()));
        }

        /// <summary>Сохраняет текст из редактора в черновик (при смене текста/страницы, публикации, закрытии). Пустой текст тоже сохраняется.</summary>
        private void SaveCurrentTextIfDirty() {
            if (_currentEntry == null || !_textDirty) return;
            try {
                _currentEntry.ContentText = GetEditorHtml();
                DictionaryEntryForText.SaveEntryForEditing(_currentEntry);
                _textDirty = false;
                EntryList.Items.Refresh();
                RefreshChangeCounts();
            }
            catch (Exception ex) {
                ShowError("Text se nepodařilo uložit", ex);
            }
        }

        private void SaveTextButton_Click(object sender, RoutedEventArgs e) {
            _textDirty = true;   // сохраняем, даже если текст не меняли
            SaveCurrentTextIfDirty();
        }

        private void RevertTextButton_Click(object sender, RoutedEventArgs e) {
            if (_currentEntry == null || !_published.TryGetValue(_currentEntry.EntryKey, out var published)) return;
            SetEditorHtml(published);
            _ = UpdatePreviewTextAsync(_currentEntry.EntryKey, HtmlLite.Sanitize(published));
            _textDirty = true;
            SaveCurrentTextIfDirty();
        }

        private void ClearEditor() {
            _currentEntry = null;
            _currentStyle = null;
            _textDirty = false;
            SetEditorHtml("");
            EditorFieldName.Text = "";
            TextEditor.IsEnabled = SaveTextButton.IsEnabled = RevertTextButton.IsEnabled = StyleExpander.IsEnabled = FormatToolbar.IsEnabled = false;
            PlainFieldHint.Visibility = Visibility.Collapsed;
            ApplyStyleToControls(null);
        }

        // ----- Форматирование части текста (жирный, курсив, подчёркнутый, ссылка) -----

        private void FmtBold_Click(object sender, RoutedEventArgs e) { EditingCommands.ToggleBold.Execute(null, TextEditor); TextEditor.Focus(); }
        private void FmtItalic_Click(object sender, RoutedEventArgs e) { EditingCommands.ToggleItalic.Execute(null, TextEditor); TextEditor.Focus(); }
        private void FmtUnderline_Click(object sender, RoutedEventArgs e) { EditingCommands.ToggleUnderline.Execute(null, TextEditor); TextEditor.Focus(); }

        private void FmtLink_Click(object sender, RoutedEventArgs e) {
            var selection = TextEditor.Selection;
            if (selection.IsEmpty) {
                MessageBox.Show("Nejdřív v textu označte slova, ze kterých má být odkaz.", "Odkaz", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var url = InputWindow.Ask(this, "Odkaz", "Adresa odkazu (web, e-mail nebo telefon):", "https://");
            if (string.IsNullOrWhiteSpace(url)) return;
            url = url.Trim();
            if (!url.Contains(':') && !url.StartsWith("/")) url = url.Contains('@') ? "mailto:" + url : "https://" + url;
            if (!HtmlLite.IsSafeHref(url) || !Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out var uri)) {
                MessageBox.Show("Tato adresa odkazu není platná.", "Odkaz", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            new Hyperlink(selection.Start, selection.End) { NavigateUri = uri };
            TextEditor.Focus();
        }

        private void FmtClear_Click(object sender, RoutedEventArgs e) {
            var selection = TextEditor.Selection;
            if (selection.IsEmpty) return;
            // ссылки, которые задевает выделение, снимаем (текст остаётся), затем убираем жирный/курсив/подчёркивание
            foreach (var link in Hyperlinks(TextEditor.Document).ToList()) {
                if (link.ContentEnd.CompareTo(selection.Start) > 0 && link.ContentStart.CompareTo(selection.End) < 0) Unwrap(link);
            }
            selection.ClearAllProperties();
            _textDirty = true;
            _ = UpdatePreviewTextAsync(_currentEntry?.EntryKey ?? "", HtmlLite.Sanitize(GetEditorHtml()));
            TextEditor.Focus();
        }

        private static IEnumerable<Hyperlink> Hyperlinks(FlowDocument doc) {
            IEnumerable<Inline> Walk(InlineCollection inlines) =>
                inlines.SelectMany(i => i is Span span ? new[] { i }.Concat(Walk(span.Inlines)) : new[] { i });
            return doc.Blocks.OfType<Paragraph>().SelectMany(p => Walk(p.Inlines)).OfType<Hyperlink>();
        }

        /// <summary>Убирает ссылку, оставляя её текст на том же месте.</summary>
        private static void Unwrap(Hyperlink link) {
            var children = link.Inlines.ToList();
            InlineCollection? siblings = link.Parent switch { Paragraph p => p.Inlines, Span sp => sp.Inlines, _ => null };
            if (siblings == null) return;
            foreach (var child in children) {
                link.Inlines.Remove(child);
                siblings.InsertBefore(link, child);
            }
            siblings.Remove(link);
        }

        #endregion

        #region Styl textu — сохраняется в черновик только при явном изменении

        private void ApplyStyleToControls(TextStyle? style) {
            _loadingStyle = true;
            try {
                FontFamilyComboBox.SelectedItem = FontFamilyComboBox.Items.Cast<ComboBoxItem>().FirstOrDefault(i => i.Content.ToString() == style?.FontFamily);
                FontSizeComboBox.SelectedItem = FontSizeComboBox.Items.Cast<ComboBoxItem>().FirstOrDefault(i => i.Content.ToString() == style?.FontSize?.Replace("px", ""));
                TextColorComboBox.SelectedItem = null;
                if (!string.IsNullOrEmpty(style?.TextColor)) {
                    try {
                        var color = (Color)ColorConverter.ConvertFromString(style.TextColor);
                        TextColorComboBox.SelectedItem = TextColorComboBox.Items.Cast<ColorInfo>().FirstOrDefault(c => c.Color == color);
                    }
                    catch { }
                }
                BoldButton.Background = Pressed(style?.FontWeight == "Bold");
                ItalicButton.Background = Pressed(style?.FontStyle == "Italic");
                UnderlineButton.Background = Pressed(style?.TextDecoration == "Underline");
            }
            finally {
                _loadingStyle = false;
            }
        }

        private static Brush Pressed(bool on) => new SolidColorBrush(on ? Color.FromRgb(0xb8, 0xcc, 0xef) : Color.FromRgb(0xe8, 0xee, 0xf9));

        /// <summary>Меняет одно свойство стиля текущего текста, сохраняет в черновик и показывает в náhledu.</summary>
        private async Task ChangeStyleAsync(Action<TextStyle> change) {
            if (_loadingStyle || _currentEntry == null) return;
            try {
                var style = _currentStyle ?? new TextStyle { EntryKey = _currentEntry.EntryKey };
                style.EntryKey = _currentEntry.EntryKey;
                change(style);
                TextStyle.SaveTextStyle(style, draft: true);
                _currentStyle = style;
                ApplyStyleToControls(style);
                await UpdatePreviewStyleAsync(_currentEntry.EntryKey, style);
                RefreshChangeCounts();
            }
            catch (Exception ex) {
                ShowError("Styl se nepodařilo uložit", ex);
            }
        }

        private async void FontFamilyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (FontFamilyComboBox.SelectedItem is ComboBoxItem item) await ChangeStyleAsync(s => s.FontFamily = item.Content.ToString());
        }

        private async void FontSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (FontSizeComboBox.SelectedItem is ComboBoxItem item) await ChangeStyleAsync(s => s.FontSize = item.Content.ToString());
        }

        private async void BoldButton_Click(object sender, RoutedEventArgs e) =>
            await ChangeStyleAsync(s => s.FontWeight = s.FontWeight == "Bold" ? "Normal" : "Bold");

        private async void ItalicButton_Click(object sender, RoutedEventArgs e) =>
            await ChangeStyleAsync(s => s.FontStyle = s.FontStyle == "Italic" ? "Normal" : "Italic");

        private async void UnderlineButton_Click(object sender, RoutedEventArgs e) =>
            await ChangeStyleAsync(s => s.TextDecoration = s.TextDecoration == "Underline" ? "None" : "Underline");

        private async void TextColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (TextColorComboBox.SelectedItem is ColorInfo c) await ChangeStyleAsync(s => s.TextColor = $"#{c.Color.R:X2}{c.Color.G:X2}{c.Color.B:X2}");
        }

        private async void DefaultStyleButton_Click(object sender, RoutedEventArgs e) {
            if (_currentEntry == null) return;
            try {
                TextStyle.DeleteTextStyle(_currentEntry.EntryKey, draft: true);
                _currentStyle = null;
                ApplyStyleToControls(null);
                await ReloadPreviewAsync();   // вернуть оформление страницы по умолчанию
                RefreshChangeCounts();
            }
            catch (Exception ex) {
                ShowError("Styl se nepodařilo zrušit", ex);
            }
        }

        #endregion

        #region Bloky — новые по шаблону, правка формой, перестановка, удаление

        private void RefreshBlocks(string? selectBlock = null) {
            _blocks = BlockTemplates.FindBlocks(_entries, _templates).Select(b => new BlockItem { Instance = b }).ToList();
            BlockList.ItemsSource = _blocks;
            if (selectBlock != null) {
                var selected = _blocks.FirstOrDefault(b => b.Key == selectBlock);
                BlockList.SelectedItem = selected;
                if (selected != null) BlockList.ScrollIntoView(selected);
            }
            UpdateBlockButtons();
        }

        private void UpdateBlockButtons() {
            var block = BlockList.SelectedItem as BlockItem;
            var siblings = SiblingsOf(block);
            MoveUpButton.IsEnabled = block != null && siblings.IndexOf(block) > 0;
            MoveDownButton.IsEnabled = block != null && siblings.IndexOf(block) < siblings.Count - 1;
            DuplicateBlockButton.IsEnabled = DeleteBlockButton.IsEnabled = block != null;
        }

        private List<BlockItem> SiblingsOf(BlockItem? block) =>
            block == null ? new List<BlockItem>() : _blocks.Where(b => b.Template == block.Template).ToList();

        private Task HighlightBlockAsync(BlockItem block) =>
            block.Instance.HasDataBlock ? HighlightAsync(null, block.Key) : HighlightAsync(block.Key, null);

        private async void BlockList_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            UpdateBlockButtons();
            if (BlockList.SelectedItem is not BlockItem block) return;
            SaveCurrentTextIfDirty();
            OpenBlockForm(block.Template, block);
            await HighlightBlockAsync(block);
        }

        /// <summary>Форма блока: все поля на одном экране. block = null — новый блок.</summary>
        private void OpenBlockForm(BlockTemplate template, BlockItem? block) {
            _formTemplate = template;
            _formBlock = block;
            BlockFormTitle.Text = block == null ? $"Nový blok: {template.Title}" : block.TemplateTitle;
            BlockFormFields.ItemsSource = template.Fields.Select(f => new BlockFieldValue {
                Name = f.Name,
                Caption = f.Label + (f.Required ? " *" : ""),
                Hint = f.Hint,
                Multiline = f.Multiline,
                // поля блока — без форматирования (в форме виден текст, а не теги)
                Value = block != null && block.FieldKeys.TryGetValue(f.Name, out var key)
                    ? HtmlLite.ToPlainText(_entries.FirstOrDefault(x => x.EntryKey == key)?.ContentText)
                    : "",
            }).ToList();
            BlockFormPanel.Visibility = Visibility.Visible;
            SaveBlockButton.Content = block == null ? "💾 Přidat blok" : "💾 Uložit blok";
        }

        private void CloseBlockForm() {
            _formTemplate = null;
            _formBlock = null;
            BlockFormPanel.Visibility = Visibility.Collapsed;
            BlockFormFields.ItemsSource = null;
        }

        /// <summary>Ключи полей нового блока: следующий свободный номер группы (Kontakty_Partner7_…, Device39_Param1_…).</summary>
        private Dictionary<string, string> NewBlockKeys(BlockTemplate template) {
            var n = DictionaryEntryForText.NextBlockNumber(_currentPage!.Id, template.Group);
            return template.Fields.ToDictionary(f => f.Name, f => template.EntryKey(n, f.Name));
        }

        private string KeyForSelection(BlockTemplate template, IReadOnlyDictionary<string, string> fieldKeys) =>
            template.LabelValueRows ? fieldKeys["Label"] : DictionaryEntryForText.GetBlockKey(fieldKeys.Values.First()) ?? "";

        private void NewBlockButton_Click(object sender, RoutedEventArgs e) {
            if (_currentPage == null || (sender as Button)?.Tag is not BlockTemplate template) return;
            SaveCurrentTextIfDirty();
            BlockList.SelectedItem = null;
            OpenBlockForm(template, null);
        }

        private async void SaveBlockButton_Click(object sender, RoutedEventArgs e) {
            if (_currentPage == null || _formTemplate == null) return;
            var values = (BlockFormFields.ItemsSource as IEnumerable<BlockFieldValue> ?? Enumerable.Empty<BlockFieldValue>()).ToDictionary(f => f.Name, f => f.Value ?? "");
            var missing = _formTemplate.Fields.Where(f => f.Required && string.IsNullOrWhiteSpace(values.GetValueOrDefault(f.Name))).Select(f => f.Label).ToList();
            if (missing.Count > 0) {
                MessageBox.Show("Vyplňte prosím: " + string.Join(", ", missing), "Chybí údaje", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            try {
                var template = _formTemplate;
                var keys = _formBlock?.FieldKeys.ToDictionary(x => x.Key, x => x.Value) ?? NewBlockKeys(template);
                // текст полей формы — простой; спецсимволы экранируем, чтобы «<» и «&» на сайте остались текстом
                DictionaryEntryForText.SaveEntriesForEditing(_currentPage.Id,
                    keys.ToDictionary(k => k.Value, k => EncodePlain(values.GetValueOrDefault(k.Key) ?? "")));
                var select = KeyForSelection(template, keys);
                await ReloadPageDataAsync(select);
                await ReloadPreviewAsync();
                if (_blocks.FirstOrDefault(b => b.Key == select) is BlockItem saved) await HighlightBlockAsync(saved);
                RefreshChangeCounts();
            }
            catch (Exception ex) {
                ShowError("Blok se nepodařilo uložit", ex);
            }
        }

        private static string EncodePlain(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

        private void CancelBlockButton_Click(object sender, RoutedEventArgs e) {
            CloseBlockForm();
            BlockList.SelectedItem = null;
        }

        private async void DuplicateBlockButton_Click(object sender, RoutedEventArgs e) {
            if (_currentPage == null || BlockList.SelectedItem is not BlockItem block) return;
            try {
                var keys = NewBlockKeys(block.Template);
                var texts = _entries.GroupBy(x => x.EntryKey).ToDictionary(g => g.Key, g => g.First().ContentText ?? "");
                DictionaryEntryForText.SaveEntriesForEditing(_currentPage.Id,
                    keys.ToDictionary(k => k.Value, k => block.FieldKeys.TryGetValue(k.Key, out var from) && texts.TryGetValue(from, out var t) ? t : ""));
                await ReloadPageDataAsync(KeyForSelection(block.Template, keys));
                await ReloadPreviewAsync();
                RefreshChangeCounts();
            }
            catch (Exception ex) {
                ShowError("Blok se nepodařilo zkopírovat", ex);
            }
        }

        private async void DeleteBlockButton_Click(object sender, RoutedEventArgs e) {
            if (_currentPage == null || BlockList.SelectedItem is not BlockItem block) return;
            if (MessageBox.Show($"Opravdu smazat „{block.TemplateTitle}: {block.Label}“?\n\nZ webu zmizí po „📤 Publikovat stránku“. Do té doby jde vrátit tlačítkem „↩ Zahodit změny“.",
                                "Smazat blok", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            try {
                DictionaryEntryForText.DeleteEntriesForEditing(_currentPage.Id, block.FieldKeys.Values);
                CloseBlockForm();
                await ReloadPageDataAsync();
                await ReloadPreviewAsync();
                RefreshChangeCounts();
            }
            catch (Exception ex) {
                ShowError("Blok se nepodařilo smazat", ex);
            }
        }

        private async void MoveUpButton_Click(object sender, RoutedEventArgs e) => await MoveBlockAsync(-1);
        private async void MoveDownButton_Click(object sender, RoutedEventArgs e) => await MoveBlockAsync(+1);

        /// <summary>Перестановка с соседним блоком того же вида: меняется местами содержимое (тексты и стили), ключи остаются.</summary>
        private async Task MoveBlockAsync(int direction) {
            if (_currentPage == null || BlockList.SelectedItem is not BlockItem block) return;
            var siblings = SiblingsOf(block);
            var index = siblings.IndexOf(block) + direction;
            if (index < 0 || index >= siblings.Count) return;
            try {
                var other = siblings[index];
                var pairs = block.Template.Fields
                    .Where(f => block.FieldKeys.ContainsKey(f.Name) && other.FieldKeys.ContainsKey(f.Name))
                    .Select(f => (block.FieldKeys[f.Name], other.FieldKeys[f.Name]));
                DictionaryEntryForText.SwapEntryContentsForEditing(_currentPage.Id, pairs);
                await ReloadPageDataAsync(other.Key);   // содержимое блока теперь на месте соседа
                await ReloadPreviewAsync();
                RefreshChangeCounts();
            }
            catch (Exception ex) {
                ShowError("Blok se nepodařilo přesunout", ex);
            }
        }

        #endregion

        #region Foto — в черновик; на сайт по «Publikovat stránku»

        private async Task ReloadPhotosAsync() {
            if (_currentPage == null) return;
            var pageId = _currentPage.Id;
            PhotoList.ItemsSource = await Task.Run(() => DictionaryEntryForImages.GetEntriesForEditing(pageId));
            await ReloadPreviewAsync();
            RefreshChangeCounts();
        }

        private async void AddPhotoButton_Click(object sender, RoutedEventArgs e) {
            if (_currentPage == null || _currentPage.Url == "#") return;
            var dialog = new OpenFileDialog { Filter = "Obrázky|*.jpg;*.jpeg;*.png;*.bmp;*.gif", Multiselect = false };
            if (dialog.ShowDialog() != true) return;
            try {
                var bytes = File.ReadAllBytes(dialog.FileName);
                if (_currentPage.Id == PageIds.Carousel) bytes = ResizeImage(bytes, 1200, 350);
                DictionaryEntryForImages.SaveEntryForEditing(new DictionaryEntryForImages {
                    PageId = _currentPage.Id,
                    EntryKey = _currentPage.Title + "_" + Guid.NewGuid(),
                    ImageData = bytes,
                    ImageName = Path.GetFileName(dialog.FileName),
                });
                await ReloadPhotosAsync();
            }
            catch (Exception ex) {
                ShowError("Fotografii se nepodařilo přidat", ex);
            }
        }

        private async void UpdatePhotosButton_Click(object sender, RoutedEventArgs e) {
            if (PhotoList.SelectedItem is not DictionaryEntryForImages photo) {
                MessageBox.Show("Vyberte fotografii, kterou chcete vyměnit.", "Upozornění", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var dialog = new OpenFileDialog { Filter = "Obrázky (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg", Title = "Vyberte nový obrázek" };
            if (dialog.ShowDialog() != true) return;
            try {
                var bytes = File.ReadAllBytes(dialog.FileName);
                if (photo.PageId == PageIds.Carousel) bytes = ResizeImage(bytes, 1200, 350);
                DictionaryEntryForImages.UpdateImageForEditing(photo.PageId, photo.EntryKey, bytes, Path.GetFileName(dialog.FileName));
                await ReloadPhotosAsync();
            }
            catch (Exception ex) {
                ShowError("Fotografii se nepodařilo vyměnit", ex);
            }
        }

        private async void DeletePhotosButton_Click(object sender, RoutedEventArgs e) {
            if (PhotoList.SelectedItem is not DictionaryEntryForImages photo) return;
            if (MessageBox.Show("Opravdu smazat tuto fotografii?\n\nZ webu zmizí po „📤 Publikovat stránku“.", "Smazat fotografii",
                                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            try {
                DictionaryEntryForImages.DeleteImageById(photo.Id);
                await ReloadPhotosAsync();
            }
            catch (Exception ex) {
                ShowError("Fotografii se nepodařilo smazat", ex);
            }
        }

        public static byte[] ResizeImage(byte[] imageBytes, int width, int height) {
            using (var original = SKBitmap.Decode(imageBytes)) {
                var resized = new SKBitmap(width, height);
                using (var canvas = new SKCanvas(resized)) {
                    canvas.DrawBitmap(original, new SKRect(0, 0, width, height));
                }
                using (var ms = new MemoryStream()) {
                    resized.Encode(ms, SKEncodedImageFormat.Jpeg, 90);
                    return ms.ToArray();
                }
            }
        }

        #endregion

        #region Mítinky, novinky (черновик — Editing*-таблицы, на сайт по «Publikovat stránku»), přístroje

        private void LoadMeetingList() {
            MeetingList.Clear();
            foreach (var meeting in Meeting.GetMeetings(draft: true)) MeetingList.Add(meeting);
        }

        private void LoadNovinkyList() {
            NovinkyList.Clear();
            foreach (var item in Noviny.GetNoviny(draft: true)) NovinkyList.Add(item);
        }

        private async void AddMeetingButton_Click(object sender, RoutedEventArgs e) {
            new AddMeetingWindow { Owner = this }.ShowDialog();
            LoadMeetingList();
            await ReloadPreviewAsync();
            RefreshChangeCounts();
        }

        private async void EditMeetingButton_Click(object sender, RoutedEventArgs e) {
            if (lv_Meeting.SelectedItem is not Meeting meeting) return;
            new UpdateMeeting(meeting) { Owner = this }.ShowDialog();
            LoadMeetingList();
            await ReloadPreviewAsync();
            RefreshChangeCounts();
        }

        private async void DeleteMeetingButton_Click(object sender, RoutedEventArgs e) {
            if (lv_Meeting.SelectedItem is not Meeting meeting) return;
            if (MessageBox.Show("Opravdu chcete tento mítink smazat?\n\nZ webu zmizí po „📤 Publikovat stránku“.", "Potvrzení smazání", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            Meeting.DeleteMeeting(meeting.Id, draft: true);
            LoadMeetingList();
            await ReloadPreviewAsync();
            RefreshChangeCounts();
        }

        private async void AddNovinyButton_Click(object sender, RoutedEventArgs e) {
            if (new AddNovinyWindow { Owner = this }.ShowDialog() == true) {
                LoadNovinkyList();
                await ReloadPreviewAsync();
                RefreshChangeCounts();
            }
        }

        private async void EditNovinyButton_Click(object sender, RoutedEventArgs e) {
            if (lv_Noviny.SelectedItem is not Noviny novinka) return;
            new UpdateNovinky(novinka) { Owner = this }.ShowDialog();
            LoadNovinkyList();
            await ReloadPreviewAsync();
            RefreshChangeCounts();
        }

        private async void DeleteNovinyButton_Click(object sender, RoutedEventArgs e) {
            if (lv_Noviny.SelectedItem is not Noviny novinka) return;
            if (MessageBox.Show("Opravdu chcete tuto novinku smazat?\n\nZ webu zmizí po „📤 Publikovat stránku“.", "Potvrzení smazání", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            Noviny.DeleteNoviny(novinka.Id, draft: true);
            LoadNovinkyList();
            await ReloadPreviewAsync();
            RefreshChangeCounts();
        }

        private async void AddDeviceButton_Click(object sender, RoutedEventArgs e) {
            if (new AddNewDevice { Owner = this }.ShowDialog() == true) {
                ReloadPagesTree();
                if (_currentPage != null) await LoadPageAsync(_currentPage);
            }
        }

        private void DeleteDeviceButton_Click(object sender, RoutedEventArgs e) {
            if (tvPageList.SelectedItem is not Pages page || page.ParentId != PageIds.DevicesParent) {
                MessageBox.Show("Vyberte v seznamu stránek přístroj, který chcete odstranit.", "Není vybrán přístroj", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }
            if (MessageBox.Show($"Opravdu chcete smazat přístroj „{page.Title}“ včetně jeho textů a fotografií? Z webu zmizí hned.",
                                "Potvrzení smazání", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            try {
                if (Pages.DeletePageWithDependencies(page.Id)) {
                    _currentPage = null;
                    ReloadPagesTree();
                    ClearEditor();
                    EntryList.ItemsSource = null;
                    BlockList.ItemsSource = null;
                    PhotoList.ItemsSource = null;
                    PageTitleText.Text = "Vyberte stránku vlevo";
                    MessageBox.Show("Přístroj byl odstraněn.", "Hotovo", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex) {
                ShowError("Přístroj se nepodařilo odstranit", ex);
            }
        }

        #endregion
    }
}

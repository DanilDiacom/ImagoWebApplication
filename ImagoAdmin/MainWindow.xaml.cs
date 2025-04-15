using System.Collections.ObjectModel;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using ImagoLib.Models;
using Microsoft.Win32;
using System.IO;
using System.Windows.Media;
using SkiaSharp;
using System.Security.Policy;
using System.Diagnostics;
using Microsoft.Web.WebView2.Core;
using System.Reflection;
using System.Net;
using NuGet.Common;
using System.Text.Json;
using System.Windows.Threading;
using System.Text;

namespace ImagoAdmin {
    public partial class MainWindow : Window {
        public ObservableCollection<Pages> PagesList { get; set; } = new ObservableCollection<Pages>();
        public ObservableCollection<Meeting> MeetingList { get; set; } = new ObservableCollection<Meeting>();
        public ObservableCollection<Noviny> NovinkyList { get; set; } = new ObservableCollection<Noviny>();


        private bool isContentModified = false;
        public int pageId;
        public string pageName;
        private string selectedAttribute = "data-key";
        public string currentEntryKey;
        private DictionaryEntryForText previousEntry = null;
        private bool _isTransitionCancelled = false;
        private bool _isProcessingSelectionChange = false;

        private readonly Dictionary<int, string> _pageCache = new();
        private bool _isWebViewNavigating;

        public MainWindow() {
            try {
                InitializeComponent();
                InitializeAsync();
                PagesList = Pages.GetPagesHierarchy() ?? new ObservableCollection<Pages>();
                DataContext = this;

                this.Title = $"IMAGO Admin v{Assembly.GetExecutingAssembly().GetName().Version}";

                TextEditor.TextChanged += (s, args) => {
                    try {
                        if (EntryList.SelectedItem is DictionaryEntryForText selectedEntry) {
                            var task = UpdateWebViewContent(selectedEntry.EntryKey, TextEditor.Text);
                            isContentModified = true;
                        }
                    }
                    catch (Exception ex) {
                        MessageBox.Show($"Ошибка при изменении текста: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                };

                DictionaryEntryForText.SyncEditingDictionary();
                DictionaryEntryForImages.SyncEditingDictionary();

                CheckForUpdatesAsync().ConfigureAwait(false);
                AddDeviceButton.Visibility = Visibility.Collapsed;
                DeleteDeviceButton.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex) {
                MessageBox.Show($"Ошибка при инициализации: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


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

        private async void InitializeAsync() {
            try {
                string webView2DataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ImagoAdmin",
                    "WebView2Data"
                );

                Directory.CreateDirectory(webView2DataPath);

                var env = await CoreWebView2Environment.CreateAsync(
                    browserExecutableFolder: null,
                    userDataFolder: webView2DataPath
                );

                await webView.EnsureCoreWebView2Async(env);

                webView.CoreWebView2.NavigationCompleted += (s, e) =>
                {
                    _isWebViewNavigating = false;
                };

                webView.CoreWebView2.Navigate("https://imagodt.cz");

                webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                webView.CoreWebView2.Settings.IsZoomControlEnabled = false;
                webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            }
            catch (Exception ex) {
                MessageBox.Show($"Ошибка инициализации WebView2: {ex.Message}");
            }
        }

        private async Task LoadPageFromDatabase(int pageId) {
            try {
                // 1. Проверяем кэш
                if (!_pageCache.TryGetValue(pageId, out string pageHtml)) {
                    var pageContent = await Task.Run(() => ContentForPage.GetPageContent(pageId));
                    if (pageContent == null) {
                        await Dispatcher.InvokeAsync(() =>
                            MessageBox.Show("Содержимое страницы не найдено"));
                        return;
                    }
                    pageHtml = pageContent.ContentText;
                    _pageCache[pageId] = pageHtml;
                }

                // 2. Обрабатываем HTML
                var entries = await Task.Run(() => DictionaryEntryForText.GetEntriesForEditind(pageId));
                if (entries != null) {
                    pageHtml = ProcessHtmlWithEntries(pageHtml, entries);
                }

                // 3. Обновляем UI
                await Dispatcher.InvokeAsync(() =>
                {
                    isContentModified = false;
                    TextEditor.Clear();
                });

                // 4. Загружаем в WebView с ожиданием стилей
                await LoadHtmlToWebView(pageHtml, pageId);
            }
            catch (Exception ex) {
                await Dispatcher.InvokeAsync(() =>
                    MessageBox.Show($"Ошибка при загрузке страницы: {ex.Message}"));
            }
        }

        private string ProcessHtmlWithEntries(string html, IEnumerable<DictionaryEntryForText> entries) {
            var sb = new StringBuilder(html);
            foreach (var entry in entries) {
                sb.Replace(
                    $"data-key=\"{entry.EntryKey}\"></",
                    $"data-key=\"{entry.EntryKey}\">{entry.ContentText}</");
            }
            return sb.ToString();
        }

        private async Task LoadHtmlToWebView(string html, int pageId) {
            if (webView?.CoreWebView2 == null) return;

            // 1. Создаем временный файл HTML с полной структурой
            string tempHtml = $@"
<!DOCTYPE html>
<html>
<head>
    <base href='https://imagodt.cz/' />
    <meta charset='UTF-8'>
    <meta http-equiv='X-UA-Compatible' content='IE=edge'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
</head>
<body>
    {html}
</body>
</html>";

            // 2. Сохраняем во временный файл
            string tempFile = Path.GetTempFileName() + ".html";
            File.WriteAllText(tempFile, tempHtml, Encoding.UTF8);

            // 3. Загружаем через Navigate
            webView.CoreWebView2.Navigate($"file://{tempFile.Replace("\\", "/")}");

            // 4. Ждем завершения загрузки
            await WaitForPageLoad();

            // 5. Блокируем навигационные элементы
            string disableScript = @"
        const disableElements = ['.navbar-custom', '#box-bottom-container'];
        disableElements.forEach(selector => {
            const el = document.querySelector(selector);
            if (el) el.style.pointerEvents = 'none';
        });";
            await webView.CoreWebView2.ExecuteScriptAsync(disableScript);

            // 6. Загружаем фото для редактирования
            var entriesFoto = await Task.Run(() => DictionaryEntryForImages.GetEntriesForEditing(pageId));
            await Dispatcher.InvokeAsync(() => PhotoList.ItemsSource = entriesFoto);
        }

        private async Task WaitForPageLoad() {
            var tcs = new TaskCompletionSource<bool>();
            EventHandler<CoreWebView2NavigationCompletedEventArgs> handler = null;

            handler = (s, e) =>
            {
                webView.CoreWebView2.NavigationCompleted -= handler;
                tcs.SetResult(e.IsSuccess);
            };

            webView.CoreWebView2.NavigationCompleted += handler;
            await tcs.Task;
        }

        private string EscapeForJavascript(string str) {
            return str.Replace("`", "\\`")
                     .Replace("$", "\\$");
        }

        private async Task SavePageHtmlToDatabase(string pageUrl, int pageId) {
            try {
                using (HttpClient client = new HttpClient()) {
                    string pageHtml = await client.GetStringAsync(pageUrl);
                    if (!string.IsNullOrEmpty(pageHtml)) {
                        ContentForPage.SaveContent(pageId, pageHtml);
                    }
                }
            }
            catch (Exception ex) {
                MessageBox.Show($"Ошибка при сохранении страницы: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e) {
            if (EntryList.SelectedItem is DictionaryEntryForText selectedEntry) {

                if (selectedAttribute == "data-key") {
                    selectedEntry.ContentText = TextEditor.Text;
                }
                else if (selectedAttribute == "data-value") {
                    selectedEntry.ContentText = TextEditor.Text;
                }

                DictionaryEntryForText.SaveEntryForEditing(selectedEntry);

                string updatedHtml = await GetWebViewHtmlAsync();
                ContentForPage.SaveContent(selectedEntry.PageId, updatedHtml);

                EntryList.Items.Refresh();

                MessageBox.Show("Změny byly uloženy do databáze.");
            }
        }


        private async Task<string> GetWebViewHtmlAsync() {
            try {
                if (webView?.CoreWebView2 == null) {
                    MessageBox.Show("WebView не инициализирован 2", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return string.Empty;
                }

                string script = "document.documentElement.innerHTML";
                var htmlContent = await webView.CoreWebView2.ExecuteScriptAsync(script);

                if (string.IsNullOrEmpty(htmlContent)) {
                    MessageBox.Show("Не удалось получить содержимое страницы", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return string.Empty;
                }

                htmlContent = htmlContent.Trim('"');
                var unescapedHtml = System.Text.RegularExpressions.Regex.Unescape(htmlContent);

                return unescapedHtml;
            }
            catch (Exception ex) {
                MessageBox.Show($"Ошибка при получении HTML: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return string.Empty;
            }
        }

        private void PublishButton_Click(object sender, RoutedEventArgs e) {
            var entriesToPublish = DictionaryEntryForText.GetAllEntriesForEditing();

            foreach (var entry in entriesToPublish) {
                DictionaryEntryForText.SaveEntry(entry);
            }

            isContentModified = false;
            MessageBox.Show("Změny byly úspěšně publikovány.");
        }

        private async void TreeView_SelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e) {
            if (e.NewValue == null || _isProcessingSelectionChange || _isWebViewNavigating)
                return;

            _isProcessingSelectionChange = true;
            _isTransitionCancelled = false;
            try {
                if (isContentModified && !await ConfirmDiscardChangesAsync())
                    return;

                if (e.NewValue is Pages selectedPage && selectedPage.Url != "#") {
                    await LoadPageAsync(selectedPage);
                }
            }
            finally {
                _isProcessingSelectionChange = false;
            }
        }

        private async Task<bool> ConfirmDiscardChangesAsync() {
            return await Dispatcher.InvokeAsync(() =>
            {
                var result = MessageBox.Show(
                    "Máte neuložené změny na této stránce. Pokud se rozhodnete přejít na jinou stránku, vaše změny nebudou uloženy. Chcete pokračovat?",
                    "Varování",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                return result == MessageBoxResult.Yes;
            });
        }

        private async Task LoadPageAsync(Pages page) {
            // 1. Обновляем UI элементов управления
            UpdatePageControls(page);

            // 2. Параллельно загружаем данные и контент
            await Task.WhenAll(
                LoadPageContentAsync(page),
                LoadAdditionalDataAsync(page)
            );
        }

        private void UpdatePageControls(Pages page) {
            Dispatcher.Invoke(() =>
            {
                pageId = page.Id;
                pageName = page.Title;

                AddMeetingButton.Visibility = page.Id == 38 ? Visibility.Visible : Visibility.Collapsed;
                lv_Meeting.Visibility = page.Id == 38 ? Visibility.Visible : Visibility.Collapsed;
                EditMeetingButton.Visibility = page.Id == 38 ? Visibility.Visible : Visibility.Collapsed;
                DeleteMeetingButton.Visibility = page.Id == 38 ? Visibility.Visible : Visibility.Collapsed;

                AddNovinyButton.Visibility = page.Id == 8 ? Visibility.Visible : Visibility.Collapsed;
                lv_Noviny.Visibility = page.Id == 8 ? Visibility.Visible : Visibility.Collapsed;
                EditNovinyButton.Visibility = page.Id == 8 ? Visibility.Visible : Visibility.Collapsed;
                DeleteNovinyButton.Visibility = page.Id == 8 ? Visibility.Visible : Visibility.Collapsed;

                AddDeviceButton.Visibility = page.ParentId == 5 ? Visibility.Visible : Visibility.Collapsed;
                DeleteDeviceButton.Visibility = page.ParentId == 5 ? Visibility.Visible : Visibility.Collapsed;


                if (page.Id == 38) LoadMeetingList();
                if (page.Id == 8) LoadNovinkyList();
            });
        }

        private async Task LoadPageContentAsync(Pages page) {
            // 1. Загружаем из БД
            await LoadPageFromDatabase(page.Id);

            // 2. Сохраняем резервную копию с сайта
            try {
                string url = (page.ParentId == 5 || page.Id == 5)
                    ? $"{page.Url}?id={page.Id}"
                    : page.Url;

                await SavePageHtmlToDatabase($"https://imagodt.cz{url}", page.Id);
            }
            catch {
                Debug.WriteLine("Не удалось сохранить резервную копию страницы");
            }
        }

        private async Task LoadAdditionalDataAsync(Pages page) {
            // Загружаем записи для редактирования
            var entries = await Task.Run(() => DictionaryEntryForText.GetEntriesForEditind(page.Id));
            await Dispatcher.InvokeAsync(() => EntryList.ItemsSource = entries);
        }

        private void LoadMeetingList() {
            MeetingList.Clear();
            foreach (var meeting in Meeting.GetMeetings()) {
                MeetingList.Add(meeting);
            }
        }

        private void LoadNovinkyList() {
            NovinkyList.Clear();
            foreach (var meeting in Noviny.GetNoviny()) {
                NovinkyList.Add(meeting);
            }
        }

        private async void EntryList_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (previousEntry != null && isContentModified) {
                await SaveChanched(previousEntry);
                await SaveCurrentStyles(previousEntry.EntryKey);
            }

            if (EntryList.SelectedItem is DictionaryEntryForText selectedEntry) {
                // Устанавливаем currentEntryKey
                currentEntryKey = selectedEntry.EntryKey;

                var style = TextStyle.GetTextStyle(selectedEntry.EntryKey);

                if (style != null) {
                    TextEditor.FontFamily = new FontFamily(style.FontFamily ?? "Arial, sans-serif");
                    TextEditor.FontSize = double.Parse(style.FontSize?.Replace("px", "") ?? "16");
                    TextEditor.FontWeight = (FontWeight)new FontWeightConverter().ConvertFromString(style.FontWeight ?? "Normal");
                    TextEditor.FontStyle = (FontStyle)new FontStyleConverter().ConvertFromString(style.FontStyle ?? "Normal");
                    TextEditor.TextDecorations = style.TextDecoration == "Underline" ? TextDecorations.Underline : null;

                    ApplyStylesToControls(style);
                }

                TextEditor.Text = selectedEntry.ContentText;

                if (selectedEntry.EntryKey.EndsWith("_Label")) {
                    selectedAttribute = "data-key";
                }
                else if (selectedEntry.EntryKey.EndsWith("_Value")) {
                    selectedAttribute = "data-value";
                }
                else {
                    selectedAttribute = "data-key";
                }

                previousEntry = selectedEntry;

                await UpdateWebViewContent(selectedEntry.EntryKey, selectedEntry.ContentText);
                await UpdateWebViewStyles(selectedEntry.EntryKey, style);
            }
        }

        private void ApplyStylesToControls(TextStyle style) {
            if (style == null) return;

            if (FontFamilyComboBox.Items.Cast<ComboBoxItem>().FirstOrDefault(item => item.Content.ToString() == style.FontFamily) is ComboBoxItem fontFamilyItem) {
                FontFamilyComboBox.SelectedItem = fontFamilyItem;
            }

            if (FontSizeComboBox.Items.Cast<ComboBoxItem>().FirstOrDefault(item => item.Content.ToString() == style.FontSize?.Replace("px", "")) is ComboBoxItem fontSizeItem) {
                FontSizeComboBox.SelectedItem = fontSizeItem;
            }

            BoldButton.Tag = style.FontWeight == "Bold" ? "True" : "False";
            BoldButton.FontWeight = style.FontWeight == "Bold" ? FontWeights.Bold : FontWeights.Normal;

            ItalicButton.Tag = style.FontStyle == "Italic" ? "True" : "False";
            ItalicButton.FontStyle = style.FontStyle == "Italic" ? FontStyles.Italic : FontStyles.Normal;

            UnderlineButton.Tag = style.TextDecoration == "Underline" ? "True" : "False";
            if (UnderlineButton.Content is TextBlock underlineTextBlock) {
                underlineTextBlock.TextDecorations = style.TextDecoration == "Underline" ? TextDecorations.Underline : null;
            }
        }

        private async Task SaveChanched(DictionaryEntryForText entry) {
            if (entry == null || string.IsNullOrEmpty(TextEditor.Text)) {
                return;
            }

            if (selectedAttribute == "data-key") {
                entry.ContentText = TextEditor.Text;
            }
            else if (selectedAttribute == "data-value") {
                entry.ContentText = TextEditor.Text;
            }

            DictionaryEntryForText.SaveEntryForEditing(entry);

            SaveCurrentStyles(entry.EntryKey);

            string updatedHtml = await GetWebViewHtmlAsync();
            ContentForPage.SaveContent(entry.PageId, updatedHtml);

            EntryList.Items.Refresh();
            isContentModified = false;
        }

        private async Task<TextStyle> SaveCurrentStyles(string entryKey) {
            if (string.IsNullOrEmpty(entryKey)) {
                return null;
            }

            var style = new TextStyle {
                EntryKey = entryKey,
                FontFamily = TextEditor.FontFamily.Source,
                FontSize = TextEditor.FontSize.ToString(),
                FontWeight = TextEditor.FontWeight.ToString(),
                FontStyle = TextEditor.FontStyle.ToString(),
                TextDecoration = TextEditor.TextDecorations == TextDecorations.Underline ? "Underline" : "None",
            };

            await TextStyle.SaveTextStyleAsync(style);

            return style;
        }

        private async Task UpdateWebViewContent(string key, string newText) {
            if (string.IsNullOrEmpty(selectedAttribute)) {
                return;
            }

            string script;

            if (selectedAttribute == "data-key") {
                script = $@"
        var element = document.querySelector('[data-key=""{key}""]');
        if (element) {{
            if (element.tagName === 'LI') {{
                var strongElement = element.querySelector('strong[data-key]');
                if (strongElement) {{
                    strongElement.innerHTML = `{newText.Replace("`", "\\`")}`;
                }}
            }} else {{
                element.innerHTML = `{newText.Replace("`", "\\`")}`;
            }}
        }}
    ";
            }
            else if (selectedAttribute == "data-value") {
                script = $@"
        var element = document.querySelector('[data-value=""{key}""]');
        if (element) {{
            if (element.tagName === 'LI') {{
                var valueElement = element.querySelector('span[data-value]');
                if (valueElement) {{
                    valueElement.innerHTML = `{newText.Replace("`", "\\`")}`;
                }}
            }} else {{
                element.innerHTML = `{newText.Replace("`", "\\`")}`;
            }}
        }}
    ";
            }
            else {
                return;
            }

            await webView.CoreWebView2.ExecuteScriptAsync(script);
        }

        private async Task UpdateWebViewStyles(string key, TextStyle style) {
            if (string.IsNullOrEmpty(selectedAttribute)) {
                return;
            }

            var fontFamily = style?.FontFamily ?? "Arial, sans-serif";
            var fontSize = $"{style?.FontSize ?? "16"}px";
            var fontWeight = style?.FontWeight ?? "normal";
            var fontStyle = style?.FontStyle ?? "normal";
            var textDecoration = style?.TextDecoration ?? "none";

            string script;

            if (selectedAttribute == "data-key") {
                script = $@"
        var element = document.querySelector('[data-key=""{key}""]');
        if (element) {{
            if (element.tagName === 'LI') {{
                var strongElement = element.querySelector('strong[data-key]');
                if (strongElement) {{
                    strongElement.style.fontFamily = '{fontFamily}';
                    strongElement.style.fontSize = '{fontSize}';
                    strongElement.style.fontWeight = '{fontWeight}';
                    strongElement.style.fontStyle = '{fontStyle}';
                    strongElement.style.textDecoration = '{textDecoration}';
                }}
            }} else {{
                element.style.fontFamily = '{fontFamily}';
                element.style.fontSize = '{fontSize}';
                element.style.fontWeight = '{fontWeight}';
                element.style.fontStyle = '{fontStyle}';
                element.style.textDecoration = '{textDecoration}';
            }}
        }}
    ";
            }
            else if (selectedAttribute == "data-value") {
                script = $@"
        var element = document.querySelector('[data-value=""{key}""]');
        if (element) {{
            if (element.tagName === 'LI') {{
                var valueElement = element.querySelector('span[data-value]');
                if (valueElement) {{
                    valueElement.style.fontFamily = '{fontFamily}';
                    valueElement.style.fontSize = '{fontSize}';
                    valueElement.style.fontWeight = '{fontWeight}';
                    valueElement.style.fontStyle = '{fontStyle}';
                    valueElement.style.textDecoration = '{textDecoration}';
                }}
            }} else {{
                element.style.fontFamily = '{fontFamily}';
                element.style.fontSize = '{fontSize}';
                element.style.fontWeight = '{fontWeight}';
                element.style.fontStyle = '{fontStyle}';
                element.style.textDecoration = '{textDecoration}';
            }}
        }}
    ";
            }
            else {
                return;
            }

            await webView.CoreWebView2.ExecuteScriptAsync(script);

            await webView.CoreWebView2.ExecuteScriptAsync("");
        }

        private async void FontFamilyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (FontFamilyComboBox.SelectedItem is ComboBoxItem selectedItem) {
                TextEditor.FontFamily = new FontFamily(selectedItem.Content.ToString());

                if (!string.IsNullOrEmpty(currentEntryKey)) {
                    var style = await SaveCurrentStyles(currentEntryKey);
                    await UpdateWebViewStyles(currentEntryKey, style);
                }
            }
        }

        private async void FontSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (FontSizeComboBox.SelectedItem is ComboBoxItem selectedItem && double.TryParse(selectedItem.Content.ToString(), out double newSize)) {
                TextEditor.FontSize = newSize;

                if (!string.IsNullOrEmpty(currentEntryKey)) {
                    var style = await SaveCurrentStyles(currentEntryKey);
                    await UpdateWebViewStyles(currentEntryKey, style);
                }
            }
        }

        private async void BoldButton_Click(object sender, RoutedEventArgs e) {
            TextEditor.FontWeight = TextEditor.FontWeight == FontWeights.Bold ? FontWeights.Normal : FontWeights.Bold;

            if (!string.IsNullOrEmpty(currentEntryKey)) {
                var style = await SaveCurrentStyles(currentEntryKey);
                await UpdateWebViewStyles(currentEntryKey, style);
            }
        }

        private async void ItalicButton_Click(object sender, RoutedEventArgs e) {
            TextEditor.FontStyle = TextEditor.FontStyle == FontStyles.Italic ? FontStyles.Normal : FontStyles.Italic;

            if (!string.IsNullOrEmpty(currentEntryKey)) {
                var style = await SaveCurrentStyles(currentEntryKey);
                await UpdateWebViewStyles(currentEntryKey, style);
            }
        }

        private async void UnderlineButton_Click(object sender, RoutedEventArgs e) {
            TextEditor.TextDecorations = TextEditor.TextDecorations == TextDecorations.Underline ? null : TextDecorations.Underline;

            if (!string.IsNullOrEmpty(currentEntryKey)) {
                var style = await SaveCurrentStyles(currentEntryKey);
                await UpdateWebViewStyles(currentEntryKey, style);
            }
        }

        private void TextColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {

        }

        private void AddPhotoButton_Click(object sender, RoutedEventArgs e) {
            OpenFileDialog openFileDialog = new OpenFileDialog {
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif",
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() == true) {
                string filePath = openFileDialog.FileName;
                string fileName = Path.GetFileName(filePath);
                byte[] imageBytes = File.ReadAllBytes(filePath);

                if (pageId == 3) {
                    imageBytes = ResizeImage(imageBytes, 1200, 350);
                }

                DictionaryEntryForImages entry = new DictionaryEntryForImages {
                    PageId = pageId,
                    EntryKey = pageName + "_" + Guid.NewGuid().ToString(),
                    ImageData = imageBytes,
                    ImageName = fileName,
                };

                DictionaryEntryForImages.SaveEntryForEditing(entry);

                var updatedEntries = DictionaryEntryForImages.GetEntriesForEditing(pageId);
                PhotoList.ItemsSource = null;
                PhotoList.ItemsSource = updatedEntries;

                isContentModified = true;
                MessageBox.Show("Fotografie byla úspěšně uložena!", "Úspěch!", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void UpdatePhotosButton_Click_1(object sender, RoutedEventArgs e) {
            if (PhotoList.SelectedItem is DictionaryEntryForImages selectedPhoto) {
                OpenFileDialog openFileDialog = new OpenFileDialog {
                    Filter = "Изображения (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg",
                    Title = "Выберите новое изображение"
                };

                if (openFileDialog.ShowDialog() == true) {
                    string filePath = openFileDialog.FileName;
                    byte[] imageData = File.ReadAllBytes(filePath);

                    if (pageId == 3) {
                        imageData = ResizeImage(imageData, 1200, 350);
                    }

                    DictionaryEntryForImages.UpdateImageForEditing(selectedPhoto.PageId, selectedPhoto.EntryKey, imageData, selectedPhoto.ImageName);
                    PhotoList.ItemsSource = DictionaryEntryForImages.GetEntriesForEditing(pageId);
                    PhotoList.Items.Refresh();
                    isContentModified = true;
                    MessageBox.Show("Fotografie byla úspěšně nahrazena!", "Úspěch!", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            else {
                MessageBox.Show("Vyberte fotografii k nahrazení!", "Upozornění", MessageBoxButton.OK, MessageBoxImage.Warning);
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

        private void AddMeetingButton_Click(object sender, RoutedEventArgs e) {
            AddMeetingWindow meetingWindow = new AddMeetingWindow();
            meetingWindow.ShowDialog();
            LoadMeetingList();
        }

        private void EditMeetingButton_Click(object sender, RoutedEventArgs e) {
            if (lv_Meeting.SelectedItem == null) return;
            var meeting = (Meeting)lv_Meeting.SelectedItem;
            UpdateMeeting meetingWindow = new UpdateMeeting(meeting);
            meetingWindow.ShowDialog();
            LoadMeetingList();
        }

        private void DeleteMeetingButton_Click(object sender, RoutedEventArgs e) {
            if (lv_Meeting.SelectedItem == null) return;

            var meeting = (Meeting)lv_Meeting.SelectedItem;
            var result = MessageBox.Show("Opravdu chcete tuto schůzku smazat?","Potvrzení smazání",MessageBoxButton.YesNo,MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes) {
                Meeting.DeleteMeeting(meeting.Id);

                MessageBox.Show("Schůzka byla úspěšně smazána!","Dokončeno",MessageBoxButton.OK,MessageBoxImage.Information);

                LoadMeetingList();
            }
        }

        private void SavePhotosButton_Click(object sender, RoutedEventArgs e) {
            var existingEntries = DictionaryEntryForImages.GetEntriesForPage(pageId);

            var entriesToPublishFoto = DictionaryEntryForImages.GetAllEntriesForEditing();

            foreach (var existingEntry in existingEntries) {
                if (!entriesToPublishFoto.Any(entry => entry.EntryKey == existingEntry.EntryKey)) {
                    DictionaryEntryForImages.DeleteEntryFromMainTable(existingEntry.Id);
                }
            }

            foreach (var entry in entriesToPublishFoto) {
                DictionaryEntryForImages.SaveEntry(entry);
            }

            PhotoList.ItemsSource = DictionaryEntryForImages.GetEntriesForEditing(pageId);
            PhotoList.Items.Refresh();

            isContentModified = false;
            MessageBox.Show("Изменения успешно опубликованы.");
        }

        private void DeletePhotosButton_Click(object sender, RoutedEventArgs e) {
            if (PhotoList.SelectedItem == null) return;

            var meeting = (DictionaryEntryForImages)PhotoList.SelectedItem;
            var result = MessageBox.Show("Opravdu chcete tuto foto smazat?", "Potvrzení smazání", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes) {
                DictionaryEntryForImages.DeleteImageById(meeting.Id);

                PhotoList.ItemsSource = DictionaryEntryForImages.GetEntriesForEditing(pageId);
                PhotoList.Items.Refresh();

                MessageBox.Show("Foto bylo úspěšně smazáne!", "Dokončeno", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void AddDeviceButton_Click(object sender, RoutedEventArgs e) {
            AddNewDevice addNovinyWindow = new AddNewDevice();
            bool? result = addNovinyWindow.ShowDialog();

            if (result == true) {
                PagesList = Pages.GetPagesHierarchy() ?? new ObservableCollection<Pages>();
                await LoadPageFromDatabase(pageId);
            }
        }

        private async void AddNovinyButton_Click(object sender, RoutedEventArgs e) {
            AddNovinyWindow addNovinyWindow = new AddNovinyWindow();
            bool? result = addNovinyWindow.ShowDialog();

            if (result == true) {
                LoadNovinkyList();
                await LoadPageFromDatabase(pageId);
            }
        }

        private void EditNovinyButton_Click(object sender, RoutedEventArgs e) {
            if (lv_Noviny.SelectedItem == null) return;
            var novinky = (Noviny)lv_Noviny.SelectedItem;
            UpdateNovinky novinkyWindow = new UpdateNovinky(novinky);
            novinkyWindow.ShowDialog();
            LoadNovinkyList();
        }

        private void DeleteNovinyButton_Click(object sender, RoutedEventArgs e) {
            if (lv_Noviny.SelectedItem == null) return;

            var meeting = (Noviny)lv_Noviny.SelectedItem;
            var result = MessageBox.Show("Opravdu chcete tuto novou položku smazat?", "Potvrzení smazání", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes) {
                Noviny.DeleteNoviny(meeting.Id);

                MessageBox.Show("Informace o novém produktu byly odstraněny", "Dokončeno", MessageBoxButton.OK, MessageBoxImage.Information);

                LoadNovinkyList();
            }
        }

        private void DeleteDeviceButton_Click(object sender, RoutedEventArgs e) {
            if (tvPageList.SelectedItem is Pages selectedPage) {
                if (selectedPage.ParentId == 5) {
                    var result = MessageBox.Show($"Opravdu chcete smazat vybraný přístroj '{selectedPage.Title}'?",
                                              "Potvrzení smazání",
                                              MessageBoxButton.YesNo,
                                              MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes) {
                        try {
                            bool success = Pages.DeletePageWithDependencies(selectedPage.Id);

                            if (success) {
                                PagesList = Pages.GetPagesHierarchy() ?? new ObservableCollection<Pages>();
                                tvPageList.ItemsSource = PagesList;
                                MessageBox.Show("Vybraný přístroj byl úspěšně odstraněn.", "Hotovo",
                                              MessageBoxButton.OK,
                                              MessageBoxImage.Information);
                            }
                        }
                        catch (Exception ex) {
                            MessageBox.Show($"Nastala chyba při odstraňování: {ex.Message}",
                                          "Chyba",
                                          MessageBoxButton.OK,
                                          MessageBoxImage.Error);
                        }
                    }
                }
                else {
                    MessageBox.Show("Tuto stránku nelze odstranit, protože není v seznamu přístrojů.\n" +
                                  "Lze mazat pouze přístroje označené speciální kategorií (ParentId = 5).",
                                  "Nelze smazat",
                                  MessageBoxButton.OK,
                                  MessageBoxImage.Warning);
                }
            }
            else {
                MessageBox.Show("Pro odstranění musíte nejprve vybrat přístroj ze seznamu.",
                               "Není vybrán přístroj",
                               MessageBoxButton.OK,
                               MessageBoxImage.Exclamation);
            }
        }
    }
}

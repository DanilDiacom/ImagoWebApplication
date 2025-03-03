using System.Collections.ObjectModel;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using ImagoLib.Models;
using Microsoft.Win32;
using System.IO;
using System.Windows.Media;
using SkiaSharp;

namespace ImagoAdmin {
    public partial class MainWindow : Window {
        public ObservableCollection<Pages> PagesList { get; set; } = new ObservableCollection<Pages>();
        public ObservableCollection<Meeting> MeetingList { get; set; } = new ObservableCollection<Meeting>();
        private bool isContentModified = false;
        public int pageId;
        public string pageName;
        private string selectedAttribute = "data-key"; // По умолчанию редактируем data-key
        public string currentEntryKey;
        private DictionaryEntryForText previousEntry = null;
        private bool _isTransitionCancelled = false; // Флаг для отслеживания отмены перехода
        private bool _isProcessingSelectionChange = false; // Флаг для предотвращения двойного вызова

        public MainWindow() {
            InitializeComponent();
            PagesList = Pages.GetPagesHierarchy();
            DataContext = this;

            TextEditor.TextChanged += (s, args) => {
                if (EntryList.SelectedItem is DictionaryEntryForText selectedEntry) {
                    Task task = UpdateWebViewContent(selectedEntry.EntryKey, TextEditor.Text);
                    isContentModified = true;
                }
            };

            DictionaryEntryForText.SyncEditingDictionary();
            DictionaryEntryForImages.SyncEditingDictionary();
        }

        private async Task LoadPageFromDatabase(int pageId) {
            var pageContent = ContentForPage.GetPageContent(pageId);
            if (pageContent == null) return;

            string pageHtml = pageContent.ContentText;

            var entries = DictionaryEntryForText.GetEntriesForEditind(pageId);

            foreach (var entry in entries) {
                pageHtml = pageHtml.Replace($"data-key=\"{entry.EntryKey}\"></", $"data-key=\"{entry.EntryKey}\">{entry.ContentText}</");
            }

            isContentModified = false;
            TextEditor.Clear();
            string script = $"document.documentElement.innerHTML = `{pageHtml.Replace("`", "\\`")}`;";
            await webView.CoreWebView2.ExecuteScriptAsync(script);
            var entriesFoto = DictionaryEntryForImages.GetEntriesForEditing(pageId);
            PhotoList.ItemsSource = entriesFoto;
        }

        private async Task SavePageHtmlToDatabase(string pageUrl, int pageId) {
            HttpClient client = new HttpClient();
            string pageHtml = await client.GetStringAsync(pageUrl);
            ContentForPage.SaveContent(pageId, pageHtml);
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

                MessageBox.Show("Изменения сохранены в базе данных.");
            }
        }

        private async Task SaveChanched(DictionaryEntryForText entry) {
            if (entry == null || string.IsNullOrEmpty(TextEditor.Text)) {
                return; // Если текст пуст или объект не выбран, ничего не делаем
            }

            if (selectedAttribute == "data-key") {
                entry.ContentText = TextEditor.Text;
            }
            else if (selectedAttribute == "data-value") {
                entry.ContentText = TextEditor.Text;
            }

            // Сохраняем текст
            DictionaryEntryForText.SaveEntryForEditing(entry);

            // Сохраняем стили
            SaveCurrentStyles(entry.EntryKey);

            // Обновляем HTML
            string updatedHtml = await GetWebViewHtmlAsync();
            ContentForPage.SaveContent(entry.PageId, updatedHtml);

            // Обновляем список записей
            EntryList.Items.Refresh();
            isContentModified = false;
        }

        private async Task<string> GetWebViewHtmlAsync() {
            string script = $"document.documentElement.innerHTML";
            var htmlContent = await webView.CoreWebView2.ExecuteScriptAsync(script);

            htmlContent = htmlContent.Trim('"');

            var unescapedHtml = System.Text.RegularExpressions.Regex.Unescape(htmlContent);

            return unescapedHtml;
        }

        private void EditButton_Click(object sender, RoutedEventArgs e) {
        }

        private void PublishButton_Click(object sender, RoutedEventArgs e) {
            var entriesToPublish = DictionaryEntryForText.GetAllEntriesForEditing();

            foreach (var entry in entriesToPublish) {
                DictionaryEntryForText.SaveEntry(entry);
            }

            isContentModified = false;
            MessageBox.Show("Изменения успешно опубликованы.");
        }

        private async void TreeView_SelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e) {
            if (e.NewValue == null)
                return;

            if (_isProcessingSelectionChange)
                return;

            _isProcessingSelectionChange = true;
            _isTransitionCancelled = false;

            try {
                if (isContentModified) {
                    var result = MessageBox.Show("Máte neuložené změny na této stránce. Pokud se rozhodnete přejít na jinou stránku, vaše změny nebudou uloženy. Chcete pokračovat?","Varování", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                    if (result == MessageBoxResult.No) {_isTransitionCancelled = true; return;}
                }

                if (e.NewValue is Pages selectedPage && selectedPage.Url != "#") {
                    pageId = selectedPage.Id;
                    pageName = selectedPage.Title;

                    AddMeetingButton.Visibility = selectedPage.Id == 38 ? Visibility.Visible : Visibility.Collapsed;
                    lv_Meeting.Visibility = selectedPage.Id == 38 ? Visibility.Visible : Visibility.Collapsed;
                    EditMeetingButton.Visibility = selectedPage.Id == 38 ? Visibility.Visible : Visibility.Collapsed;
                    DeleteMeetingButton.Visibility = selectedPage.Id == 38 ? Visibility.Visible : Visibility.Collapsed;

                    if (selectedPage.Id == 38) {
                        MeetingList.Clear();
                        foreach (var meeting in Meeting.GetMeetings()) {
                            MeetingList.Add(meeting);
                        }
                    }

                    await LoadPageFromDatabase(selectedPage.Id);

                    await SavePageHtmlToDatabase("https://localhost:7090" + selectedPage.Url, selectedPage.Id);

                    var entries = DictionaryEntryForText.GetEntriesForEditind(selectedPage.Id);
                    EntryList.ItemsSource = entries;

                    previousEntry = null;
                    isContentModified = false;
                }
            }
            finally {
                _isProcessingSelectionChange = false;
            }
        }

        private async void EntryList_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (previousEntry != null && isContentModified) {
                await SaveChanched(previousEntry); // Сохраняем изменения для предыдущего текста
            }

            if (EntryList.SelectedItem is DictionaryEntryForText selectedEntry) {
                // Загружаем текст
                TextEditor.Text = selectedEntry.ContentText;

                // Загружаем стили из базы данных
                var style = TextStyle.GetTextStyle(selectedEntry.EntryKey);
                if (style != null) {
                    TextEditor.FontFamily = new FontFamily(style.FontFamily);
                    TextEditor.FontSize = double.Parse(style.FontSize);
                    TextEditor.FontWeight = (FontWeight)new FontWeightConverter().ConvertFromString(style.FontWeight);
                    TextEditor.FontStyle = (FontStyle)new FontStyleConverter().ConvertFromString(style.FontStyle);
                    TextEditor.TextDecorations = style.TextDecoration == "Underline" ? TextDecorations.Underline : null;
                }

                // Обновляем текущий ключ и предыдущий текст
                currentEntryKey = selectedEntry.EntryKey;
                previousEntry = selectedEntry;
            }
        }

        private async Task UpdateWebViewContent(string key, string newText) {
            string script;

            if (selectedAttribute == "data-key") {
                script = $@"
        var element = document.querySelector('[data-key=""{key}""]');
        if (element) {{
            var strongElement = element.querySelector('strong[data-key]');
            if (strongElement) {{
                strongElement.innerHTML = `{newText.Replace("`", "\\`")}`;
            }} else {{
                element.innerHTML = `{newText.Replace("`", "\\`")}`;
            }}
        }}
    ";
            }
            else if (selectedAttribute == "data-value") {
                script = $@"
            var parentElement = document.querySelector('[data-value=""{key}""]');
            if (parentElement) {{
                var valueElement = parentElement.querySelector('span[data-value]');
                if (valueElement) {{
                    valueElement.innerHTML = `{newText.Replace("`", "\\`")}`;
                }}
            }}
        ";
            }
            else {
                return;
            }

            await webView.CoreWebView2.ExecuteScriptAsync(script);
        }

        private void AddPhotoButton_Click(object sender, RoutedEventArgs e) {
            OpenFileDialog openFileDialog = new OpenFileDialog {
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif",
                Multiselect = false // Оставляем выбор одного файла
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
                PhotoList.ItemsSource = null;  // Очищаем источник данных
                PhotoList.ItemsSource = updatedEntries; // Устанавливаем новый список

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

                    if(pageId == 3) {
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
            lv_Meeting.Items.Refresh();
        }

        private void EditMeetingButton_Click(object sender, RoutedEventArgs e) {
            if (lv_Meeting.SelectedItem == null) return;
            var meeting = (Meeting)lv_Meeting.SelectedItem;
            UpdateMeeting meetingWindow = new UpdateMeeting(meeting);
            meetingWindow.ShowDialog();
            lv_Meeting.Items.Refresh();
        }

        private void FontFamilyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (FontFamilyComboBox.SelectedItem is ComboBoxItem selectedItem) {
                TextEditor.FontFamily = new FontFamily(selectedItem.Content.ToString());
                SaveCurrentStyles(currentEntryKey); // Сохраняем стили
            }
        }

        private void FontSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (FontSizeComboBox.SelectedItem is ComboBoxItem selectedItem && double.TryParse(selectedItem.Content.ToString(), out double newSize)) {
                TextEditor.FontSize = newSize;
                SaveCurrentStyles(currentEntryKey); // Сохраняем стили
            }
        }

        private void BoldButton_Click(object sender, RoutedEventArgs e) {
            TextEditor.FontWeight = TextEditor.FontWeight == FontWeights.Bold ? FontWeights.Normal : FontWeights.Bold;
            SaveCurrentStyles(currentEntryKey); // Сохраняем стили
        }

        private void ItalicButton_Click(object sender, RoutedEventArgs e) {
            TextEditor.FontStyle = TextEditor.FontStyle == FontStyles.Italic ? FontStyles.Normal : FontStyles.Italic;
            SaveCurrentStyles(currentEntryKey); // Сохраняем стили
        }

        private void UnderlineButton_Click(object sender, RoutedEventArgs e) {
            TextEditor.TextDecorations = TextEditor.TextDecorations == TextDecorations.Underline ? null : TextDecorations.Underline;
            SaveCurrentStyles(currentEntryKey); // Сохраняем стили
        }
        private void TextColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (TextColorComboBox.SelectedItem is ComboBoxItem selectedItem) {
                TextEditor.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(selectedItem.Content.ToString()));
                SaveCurrentStyles(currentEntryKey); // Сохраняем стили
            }
        }

        private void TextAlignmentComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (TextAlignmentComboBox.SelectedItem is ComboBoxItem selectedItem) {
                switch (selectedItem.Content.ToString()) {
                    case "Left":
                        TextEditor.TextAlignment = TextAlignment.Left;
                        break;
                    case "Center":
                        TextEditor.TextAlignment = TextAlignment.Center;
                        break;
                    case "Right":
                        TextEditor.TextAlignment = TextAlignment.Right;
                        break;
                    case "Justify":
                        TextEditor.TextAlignment = TextAlignment.Justify;
                        break;
                }
                SaveCurrentStyles(currentEntryKey); // Сохраняем стили
            }
        }


        private void SaveCurrentStyles(string entryKey) {
            var style = new TextStyle {
                EntryKey = entryKey,
                FontFamily = TextEditor.FontFamily.Source,
                FontSize = TextEditor.FontSize.ToString(),
                FontWeight = TextEditor.FontWeight.ToString(),
                FontStyle = TextEditor.FontStyle.ToString(),
                TextDecoration = TextEditor.TextDecorations == TextDecorations.Underline ? "Underline" : "None",
                TextColor = ((SolidColorBrush)TextEditor.Foreground).Color.ToString(),
                TextAlignment = TextEditor.TextAlignment.ToString()
            };

            TextStyle.SaveTextStyle(style); // Сохраняем стили в базу данных
        }


        private void DeleteMeetingButton_Click(object sender, RoutedEventArgs e) {
            if (lv_Meeting.SelectedItem == null) return;

            var meeting = (Meeting)lv_Meeting.SelectedItem;
            var result = MessageBox.Show("Opravdu chcete tuto schůzku smazat?","Potvrzení smazání",MessageBoxButton.YesNo,MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes) {
                Meeting.DeleteMeeting(meeting.Id);

                MessageBox.Show("Schůzka byla úspěšně smazána!","Dokončeno",MessageBoxButton.OK,MessageBoxImage.Information);

                lv_Meeting.Items.Refresh();  
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

            // Обновляем PhotoList
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

        private void AddDeviceButton_Click(object sender, RoutedEventArgs e) {

        }
    }
}

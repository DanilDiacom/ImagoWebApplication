using ImagoLib.Models;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ImagoAdmin {
    public partial class AddNewDevice : Window {

        public ObservableCollection<Parameter> Parameters { get; set; }
        public ObservableCollection<BitmapImage> Photos { get; set; }

        public AddNewDevice() {
            InitializeComponent();
            DataContext = this;

            Parameters = new ObservableCollection<Parameter>
            {
                new Parameter(),
                new Parameter(),
                new Parameter(),
                new Parameter()
            };

            Photos = new ObservableCollection<BitmapImage>();

            ParametersItemsControl.ItemsSource = Parameters;
            PhotoListBox.ItemsSource = Photos;
        }


        private void AddPhoto_Click(object sender, RoutedEventArgs e) {
            OpenFileDialog openFileDialog = new OpenFileDialog {
                Multiselect = true,
                Filter = "Obrázky (*.jpg;*.png;*.jpeg)|*.jpg;*.png;*.jpeg"
            };

            if (openFileDialog.ShowDialog() == true) {
                foreach (var file in openFileDialog.FileNames) {
                    BitmapImage image = new BitmapImage(new Uri(file));
                    Photos.Add(image);
                }
                PhotoListBox.Items.Refresh();
            }
        }

        private void RemovePhoto_Click(object sender, RoutedEventArgs e) {
            if (sender is Button button && button.Tag is BitmapImage image) {
                Photos.Remove(image);
                PhotoListBox.Items.Refresh();
            }
        }


        private void Save_Click(object sender, RoutedEventArgs e) {
            try {
                if (string.IsNullOrWhiteSpace(TitleTextBox.Text)) {
                    MessageBox.Show("Název produktu je povinný!", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 1. Создаем страницу (Page)
                var page = new Pages {
                    Title = TitleTextBox.Text,
                    Url = "/Diacom/DeviceDiacom", // Нужно реализовать эту функцию
                    ParentId = 5
                };

                int pageId = Pages.InsertPageIfNotExists(page);

                // 2. Сохраняем заголовок товара
                var titleEntry = new DictionaryEntryForText {
                    PageId = pageId,
                    EntryKey = $"{TitleTextBox.Text}_Title",
                    ContentText = TitleTextBox.Text
                };
                DictionaryEntryForText.InsertEntryIfNotExists(titleEntry);

                // 3. Сохраняем описание (Popis)
                if (!string.IsNullOrWhiteSpace(DescriptionTextBox.Text)) {
                    var popisHeaderEntry = new DictionaryEntryForText {
                        PageId = pageId,
                        EntryKey = $"{TitleTextBox.Text}PopisHeader",
                        ContentText = "Popis produktu"
                    };
                    DictionaryEntryForText.InsertEntryIfNotExists(popisHeaderEntry);

                    var popisTextEntry = new DictionaryEntryForText {
                        PageId = pageId,
                        EntryKey = $"{TitleTextBox.Text}_PopisText",
                        ContentText = DescriptionTextBox.Text
                    };
                    DictionaryEntryForText.InsertEntryIfNotExists(popisTextEntry);
                }

                foreach (var param in Parameters) {
                    if (!string.IsNullOrWhiteSpace(param.Name) && !string.IsNullOrWhiteSpace(param.Value)) {
                        // Сохраняем Label (название параметра)
                        var labelEntry = new DictionaryEntryForText {
                            PageId = pageId,
                            EntryKey = $"{param.Name}_Label",
                            ContentText = param.Name
                        };
                        DictionaryEntryForText.InsertEntryIfNotExists(labelEntry);

                        // Сохраняем Value (значение параметра)
                        var valueEntry = new DictionaryEntryForText {
                            PageId = pageId,
                            EntryKey = $"{param.Name}_Value",
                            ContentText = param.Value
                        };
                        DictionaryEntryForText.InsertEntryIfNotExists(valueEntry);
                    }
                }

                // 5. Сохраняем фотографии
                foreach (var photo in Photos) {
                    byte[] imageData = ConvertBitmapImageToByteArray(photo);

                    var imageEntry = new DictionaryEntryForImages {
                        PageId = pageId,
                        EntryKey = $"Photo_{Guid.NewGuid().ToString("N")}",
                        ImageData = imageData,
                        ImageName = $"{TitleTextBox.Text}_photo.jpg"
                    };

                    DictionaryEntryForImages.InsertEntryIfNotExists(imageEntry);
                }

                MessageBox.Show("Produkt byl úspěšně uložen!", "Úspěch", MessageBoxButton.OK, MessageBoxImage.Information);
                this.Close();
            }
            catch (Exception ex) {
                MessageBox.Show($"Chyba při ukládání: {ex.Message}", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private byte[] ConvertBitmapImageToByteArray(BitmapImage bitmapImage) {
            using (MemoryStream stream = new MemoryStream()) {
                JpegBitmapEncoder encoder = new JpegBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmapImage));
                encoder.Save(stream);
                return stream.ToArray();
            }
        }


        private void AddParameter_Click(object sender, RoutedEventArgs e) {
            Parameters.Add(new Parameter());
        }

        private void RemoveParameter_Click(object sender, RoutedEventArgs e) {
            if (sender is Button button && button.Tag is Parameter parameter) {
                Parameters.Remove(parameter);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) {
            this.Close();
        }
    }
}

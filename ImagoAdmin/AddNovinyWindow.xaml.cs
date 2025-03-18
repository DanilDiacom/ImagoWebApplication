using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using ImagoLib.Models;
using Microsoft.CodeAnalysis;
using Microsoft.Win32;

namespace ImagoAdmin {
    public partial class AddNovinyWindow : Window {
        public ObservableCollection<Parameter> Parameters { get; set; }
        public ObservableCollection<BitmapImage> Photos { get; set; }
        public string SelectedIconPath { get; set; } // Путь к выбранной иконке

        public AddNovinyWindow() {
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

        private void SelectIcon_Click(object sender, RoutedEventArgs e) {
            var openFileDialog = new OpenFileDialog {
                Filter = "Image Files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg"
            };

            if (openFileDialog.ShowDialog() == true) {
                SelectedIconPath = openFileDialog.FileName;
                SelectedIconImage.Source = new BitmapImage(new Uri(SelectedIconPath));
            }
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

        private void AddParameter_Click(object sender, RoutedEventArgs e) {
            Parameters.Add(new Parameter());
        }

        private void RemoveParameter_Click(object sender, RoutedEventArgs e) {
            if (sender is Button button && button.Tag is Parameter parameter) {
                Parameters.Remove(parameter);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e) {
            try {
                // Проверка на null или пустые значения для обязательных полей
                if (string.IsNullOrWhiteSpace(TitleTextBox.Text)) {
                    MessageBox.Show("Vyplňte název novinky.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(CommentTextBox.Text)) {
                    MessageBox.Show("Vyplňte komentář k novince.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(DescriptionTextBox.Text)) {
                    MessageBox.Show("Vyplňte popis novinky.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(SelectedIconPath)) {
                    MessageBox.Show("Vyberte ikonu pro novinku.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Проверка параметров: хотя бы один параметр должен быть заполнен
                if (Parameters == null || Parameters.Count == 0) {
                    MessageBox.Show("Zapomněli jste zadat parametry pro novinku.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                bool isAnyParameterFilled = false;
                foreach (var parameter in Parameters) {
                    if (!string.IsNullOrWhiteSpace(parameter.Name) && !string.IsNullOrWhiteSpace(parameter.Value)) {
                        isAnyParameterFilled = true;
                        break; // Если хотя бы один параметр заполнен, прерываем цикл
                    }
                }

                if (!isAnyParameterFilled) {
                    MessageBox.Show("Vyplňte alespoň jeden parametr (název a hodnotu).", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Проверка фотографий
                if (Photos == null || Photos.Count == 0) {
                    MessageBox.Show("Přidejte alespoň jednu fotografii pro novinku.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Создание объекта Noviny
                var noviny = new Noviny {
                    PostedDate = DateTime.Now,
                    Title = TitleTextBox.Text,
                    Comment = CommentTextBox.Text,
                    Description = DescriptionTextBox.Text,
                    IconPhoto = File.ReadAllBytes(SelectedIconPath)
                };

                int novinyId = Noviny.InsertNoviny(noviny);

                // Сохранение параметров (только заполненных)
                foreach (var parameter in Parameters) {
                    if (!string.IsNullOrWhiteSpace(parameter.Name) && !string.IsNullOrWhiteSpace(parameter.Value)) {
                        var novinyParameter = new NovinyParameter {
                            NovinyId = novinyId,
                            ParameterName = parameter.Name,
                            ParameterValue = parameter.Value
                        };
                        NovinyParameter.InsertParameter(novinyParameter);
                    }
                }

                // Сохранение фотографий
                foreach (var photo in Photos) {
                    var photoInfo = new NovinyFoto {
                        NovinyId = novinyId,
                        PhotoName = System.IO.Path.GetFileName(photo.UriSource.LocalPath),
                        PhotoData = ConvertImageToByteArray(photo)
                    };
                    NovinyFoto.InsertPhoto(photoInfo, novinyId);
                }

                MessageBox.Show("Data úspěšně uložena!", "Hotovo", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                this.Close();
            }
            catch (Exception ex) {
                MessageBox.Show($"Ошибка при сохранении данных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private byte[] ConvertImageToByteArray(BitmapImage image) {
            using (var memoryStream = new System.IO.MemoryStream()) {
                BitmapEncoder encoder = new JpegBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(image));
                encoder.Save(memoryStream);
                return memoryStream.ToArray();
            }
        }


        private void Cancel_Click(object sender, RoutedEventArgs e) {
            this.Close();
        }
    }



    public class Parameter : INotifyPropertyChanged {
        private string _name;
        private string _value;

        public string Name {
            get => _name;
            set {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        public string Value {
            get => _value;
            set {
                _value = value;
                OnPropertyChanged(nameof(Value));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
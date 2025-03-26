using ImagoLib.Models;
using Microsoft.CodeAnalysis;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
    public partial class UpdateNovinky : Window {

        public class PhotoItem {
            public BitmapImage Image { get; set; }
            public string FileName { get; set; }
        }

        public class IconItem {
            public BitmapImage ImageIcon { get; set; }
        }

        public ObservableCollection<PhotoItem> Photos { get; set; } = new ObservableCollection<PhotoItem>();
        public ObservableCollection<IconItem> Icon { get; set; } = new ObservableCollection<IconItem>();
        public ObservableCollection<Parameter> Parameters { get; set; } = new ObservableCollection<Parameter>();

        public Noviny Noviny { get; private set; }
        public int id;

        public UpdateNovinky(Noviny noviny) {
            InitializeComponent();
            this.DataContext = this;

            Noviny = noviny;
            TitleTextBox.Text = noviny.Title;
            CommentTextBox.Text = noviny.Comment;
            DescriptionTextBox.Text = noviny.Description;

            id = noviny.Id;

            var photos = NovinyFoto.GetPhotosForRequest(noviny.Id);
            foreach (var photo in photos) {
                Photos.Add(new PhotoItem {
                    Image = ConvertByteArrayToImage(photo.PhotoData),
                    FileName = photo.PhotoName
                });
            }

            PhotoListBox.ItemsSource = Photos;


            //if (noviny.IconPhoto != null && noviny.IconPhoto.Length > 0) {
            //    Icon.Add(new IconItem {
            //        ImageIcon = ConvertByteArrayToImage(noviny.IconPhoto)
            //    });
            //}

            Icon.Add(new IconItem {
                ImageIcon = ConvertByteArrayToImage(noviny.IconPhoto),
            });

            IconListBox.ItemsSource = Icon;

            
            // Загрузка параметров
            var parameters = NovinyParameter.GetParametersForNoviny(noviny.Id);
            foreach (var parameter in parameters) {
                Parameters.Add(new Parameter {
                    Name = parameter.ParameterName,
                    Value = parameter.ParameterValue
                });
            }
        }

        private BitmapImage ConvertByteArrayToImage(byte[] photoData) {
            if (photoData == null || photoData.Length == 0) {
                Console.WriteLine("Данные изображения пустые!");
                return null;
            }

            try {
                using (var ms = new System.IO.MemoryStream(photoData)) {
                    var image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = ms;
                    image.EndInit();
                    image.Freeze();
                    return image;
                }
            }
            catch (Exception ex) {
                Console.WriteLine($"Ошибка загрузки изображения: {ex.Message}");
                return null;
            }
        }

        private void SelectIcon_Click(object sender, RoutedEventArgs e) {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog {
                Filter = "Image Files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg"
            };

            if (openFileDialog.ShowDialog() == true) {
                var iconItem = new IconItem {
                    ImageIcon = new BitmapImage(new Uri(openFileDialog.FileName))
                };

                Icon.Clear();
                Icon.Add(iconItem);
            }
        }

        private void AddParameter_Click(object sender, RoutedEventArgs e) {
            Parameters.Add(new Parameter());

        }

        private void AddPhoto_Click(object sender, RoutedEventArgs e) {
            OpenFileDialog openFileDialog = new OpenFileDialog {
                Multiselect = true,
                Filter = "Obrázky (*.jpg;*.png;*.jpeg)|*.jpg;*.png;*.jpeg"
            };

            if (openFileDialog.ShowDialog() == true) {
                foreach (var file in openFileDialog.FileNames) {
                    var image = LoadImage(file);

                    var photoItem = new PhotoItem {
                        Image = image,
                        FileName = System.IO.Path.GetFileName(file)
                    };

                    Photos.Add(photoItem);
                }
            }
        }
        private BitmapImage LoadImage(string filePath) {
            try {
                using (var stream = new System.IO.FileStream(filePath, System.IO.FileMode.Open, System.IO.FileAccess.Read)) {
                    var image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = stream;
                    image.EndInit();
                    image.Freeze();
                    return image;
                }
            }
            catch (Exception ex) {
                MessageBox.Show($"Ошибка загрузки изображения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e) {
            try {
                // Получаем значения из текстовых полей
                var title = TitleTextBox.Text;
                var comment = CommentTextBox.Text;
                var description = DescriptionTextBox.Text;
                var createdAt = DateTime.Now;

                // Проверка на null или пустые значения для обязательных полей
                if (string.IsNullOrWhiteSpace(title)) {
                    MessageBox.Show("Musíte zadat název nového produktu.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(comment)) {
                    MessageBox.Show("Musíte přidat komentář k novému produktu.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(description)) {
                    MessageBox.Show("Musíte poskytnout popis nového produktu.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Проверка на null или пустую коллекцию иконок
                byte[] iconPhoto = null;
                if (Icon != null && Icon.Count > 0 && Icon[0].ImageIcon != null) {
                    iconPhoto = ConvertImageToByteArray(Icon[0].ImageIcon);
                }

                // Создание объекта Noviny
                Noviny newDitales = new Noviny {
                    Title = title,
                    Description = description,
                    Comment = comment,
                    PostedDate = createdAt,
                    IconPhoto = iconPhoto
                };

                // Обновление записи в базе данных
                Noviny.UpdateNoviny(newDitales, id);

                // Удаление старых фотографий
                NovinyFoto.DeletePhotosForRequest(id);

                // Сохранение новых фотографий (если они есть)
                if (Photos != null && Photos.Count > 0) {
                    foreach (var photo in Photos) {
                        if (photo?.Image == null) {
                            MessageBox.Show("Některé fotografie jsou neplatné.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                            continue;
                        }

                        var photoName = photo.FileName ?? "uploaded_photo.jpg";
                        var photoData = ConvertImageToByteArray(photo.Image);

                        NovinyFoto newPhoto = new NovinyFoto {
                            NovinyId = id,
                            PhotoName = photoName,
                            PhotoData = photoData
                        };

                        NovinyFoto.InsertPhoto(newPhoto, id);
                    }
                }
                else {
                    MessageBox.Show("Nebyly přidány žádné fotografie.", "Upozornění", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                // Удаление старых параметров
                NovinyParameter.DeleteParametersForNoviny(id);

                // Проверка параметров: хотя бы один параметр должен быть заполнен
                if (Parameters != null && Parameters.Count > 0) {
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

                    // Сохранение новых параметров (только заполненных)
                    foreach (var parameter in Parameters) {
                        if (!string.IsNullOrWhiteSpace(parameter.Name) && !string.IsNullOrWhiteSpace(parameter.Value)) {
                            var novinyParameter = new NovinyParameter {
                                NovinyId = id,
                                ParameterName = parameter.Name,
                                ParameterValue = parameter.Value
                            };
                            NovinyParameter.InsertParameter(novinyParameter);
                        }
                    }
                }
                else {
                    MessageBox.Show("Nebyly přidány žádné parametry.", "Upozornění", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                MessageBox.Show("Upravené údaje o novém produktu byly uloženy!", "Hotovo", MessageBoxButton.OK, MessageBoxImage.Information);
                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex) {
                MessageBox.Show($"Došlo k chybě při ukládání dat: {ex.Message}", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
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

        private void RemoveParameter_Click(object sender, RoutedEventArgs e) {
            if (sender is Button button && button.Tag is Parameter parameter) {
                Parameters.Remove(parameter);
            }
        }

        private void RemovePhoto_Click(object sender, RoutedEventArgs e) {
            if (sender is Button button && button.Tag is PhotoItem photoItem) {
                Photos.Remove(photoItem);
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
}

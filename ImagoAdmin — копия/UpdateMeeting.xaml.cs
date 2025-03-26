using ImagoLib.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using System.Collections.ObjectModel;

namespace ImagoAdmin {

    public partial class UpdateMeeting : Window {
        public class PhotoItem {
            public BitmapImage Image { get; set; }
            public string FileName { get; set; }
        }

        public ObservableCollection<PhotoItem> Photos { get; set; } = new ObservableCollection<PhotoItem>();
        public Meeting Meeting { get; private set; }

        public int id;
        public UpdateMeeting(Meeting meeting) {
            InitializeComponent();
            this.Meeting = meeting;
            TitleTextBox.Text = meeting.Title;
            LocationTextBox.Text = meeting.Location;
            DescriptionTextBox.Text = meeting.Description;
            FeedbackTextBox.Text = meeting.Feedback;

            id = meeting.Id;

            var photos = MeetingPhoto.GetPhotosForMeeting(meeting.Id);
            foreach (var photo in photos) {
                Photos.Add(new PhotoItem {
                    Image = ConvertByteArrayToImage(photo.PhotoData),
                    FileName = photo.PhotoName
                });
            }

            PhotoList.ItemsSource = Photos;
            this.DataContext = this;
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
                    image.Freeze();  // Разрешает использование в многопоточной среде
                    return image;
                }
            }
            catch (Exception ex) {
                Console.WriteLine($"Ошибка загрузки изображения: {ex.Message}");
                return null;
            }
        }

        private void AddPhotoButton_Click(object sender, RoutedEventArgs e) {
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

        private void SaveButton_Click(object sender, RoutedEventArgs e) {
            var title = TitleTextBox.Text;
            var location = LocationTextBox.Text;
            var description = DescriptionTextBox.Text;
            var feedback = FeedbackTextBox.Text;
            var createdAt = DateTime.Now;

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(location)) {
                MessageBox.Show("Vyplňte název a místo konání.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(description) || string.IsNullOrWhiteSpace(feedback)) {
                MessageBox.Show("Vyplňte popis a detaily setkání.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Meeting newMeeting = new Meeting {
                Title = title,
                Location = location,
                Description = description,
                Feedback = feedback,
                CreatedAt = createdAt
            };

            // Обновляем саму встречу
            Meeting.UpdateMeeting(newMeeting, id);

            // Удаляем старые фотографии для этой встречи
            MeetingPhoto.DeletePhotosForMeeting(id);

            // Добавляем новые фотографии
            foreach (var photo in Photos) {
                var photoName = photo.FileName ?? "uploaded_photo.jpg"; // Если фото новое, можно генерировать имя
                var photoData = ConvertImageToByteArray(photo.Image);

                MeetingPhoto newPhoto = new MeetingPhoto {
                    MeetingId = id,
                    PhotoName = photoName,
                    PhotoData = photoData
                };

                MeetingPhoto.InsertPhoto(newPhoto, id);
            }

            MessageBox.Show("Mítink byl úspěšně uložen!", "Hotovo", MessageBoxButton.OK, MessageBoxImage.Information);
            this.DialogResult = true;
            this.Close();
        }


        private byte[] ConvertImageToByteArray(BitmapImage image) {
            using (var memoryStream = new System.IO.MemoryStream()) {
                BitmapEncoder encoder = new JpegBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(image));
                encoder.Save(memoryStream);
                return memoryStream.ToArray();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e) {
            this.Close();
        }

        private void RemovePhoto_Click(object sender, RoutedEventArgs e) {
            if (sender is Button button && button.Tag is PhotoItem photoItem) {
                Photos.Remove(photoItem);
            }
        }
    }
}

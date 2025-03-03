using Microsoft.Win32;
using System;
using System.Collections.Generic;
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
using ImagoLib.Models;

namespace ImagoAdmin {
    /// <summary>
    /// Логика взаимодействия для AddMeetingWindow.xaml
    /// </summary>
    public partial class AddMeetingWindow : Window {

        public List<BitmapImage> Photos { get; set; } = new List<BitmapImage>();

        public AddMeetingWindow() {
            InitializeComponent();
            PhotoList.ItemsSource = Photos;
        }

        private void AddPhotoButton_Click(object sender, RoutedEventArgs e) {
            OpenFileDialog openFileDialog = new OpenFileDialog {
                Multiselect = true,
                Filter = "Obrázky (*.jpg;*.png;*.jpeg)|*.jpg;*.png;*.jpeg"
            };

            if (openFileDialog.ShowDialog() == true) {
                foreach (var file in openFileDialog.FileNames) {
                    BitmapImage image = new BitmapImage(new Uri(file));
                    Photos.Add(image);
                }
                PhotoList.Items.Refresh();
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

            var meetingId = Meeting.InsertMeeting(newMeeting);

            foreach (var photo in Photos) {
                var photoName = System.IO.Path.GetFileName(photo.UriSource.LocalPath);
                var photoData = ConvertImageToByteArray(photo);

                MeetingPhoto newPhoto = new MeetingPhoto {
                    MeetingId = newMeeting.Id,
                    PhotoName = photoName,
                    PhotoData = photoData
                };

                MeetingPhoto.InsertPhoto(newPhoto, meetingId);
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
            if (sender is Button button && button.Tag is BitmapImage image) {
                Photos.Remove(image);
                PhotoList.Items.Refresh();
            }
        }
    }
}

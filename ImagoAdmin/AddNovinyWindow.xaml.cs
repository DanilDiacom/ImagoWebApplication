using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace ImagoAdmin {
    public partial class AddNovinyWindow : Window {
        public ObservableCollection<Parameter> Parameters { get; set; }
        public ObservableCollection<string> Photos { get; set; }
        public string SelectedIconPath { get; set; } // Путь к выбранной иконке

        public AddNovinyWindow() {
            InitializeComponent();
            DataContext = this;

            // Инициализация коллекции параметров с 4 начальными полями
            Parameters = new ObservableCollection<Parameter>
            {
                new Parameter(),
                new Parameter(),
                new Parameter(),
                new Parameter()
            };

            // Инициализация коллекции фотографий
            Photos = new ObservableCollection<string>();

            ParametersItemsControl.ItemsSource = Parameters;
            PhotoListBox.ItemsSource = Photos;
        }

        // Выбор иконки
        private void SelectIcon_Click(object sender, RoutedEventArgs e) {
            var openFileDialog = new OpenFileDialog {
                Filter = "Image Files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg"
            };

            if (openFileDialog.ShowDialog() == true) {
                SelectedIconPath = openFileDialog.FileName;
                SelectedIconImage.Source = new BitmapImage(new Uri(SelectedIconPath));
            }
        }

        // Добавление фотографии
        private void AddPhoto_Click(object sender, RoutedEventArgs e) {
            var openFileDialog = new OpenFileDialog {
                Filter = "Image Files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg"
            };

            if (openFileDialog.ShowDialog() == true) {
                Photos.Add(openFileDialog.FileName);
            }
        }

        // Удаление фотографии
        private void RemovePhoto_Click(object sender, RoutedEventArgs e) {
            if (sender is Button button && button.Tag is string selectedPhoto) {
                Photos.Remove(selectedPhoto);
            }
        }

        // Добавление параметра
        private void AddParameter_Click(object sender, RoutedEventArgs e) {
            Parameters.Add(new Parameter());
        }

        // Удаление параметра
        private void RemoveParameter_Click(object sender, RoutedEventArgs e) {
            if (sender is Button button && button.Tag is Parameter parameter) {
                Parameters.Remove(parameter);
            }
        }

        // Сохранение данных
        private void Save_Click(object sender, RoutedEventArgs e) {
            // Логика сохранения данных в базу данных
            // Сохранить иконку
            SaveIconToDatabase(SelectedIconPath);

            // Сохранить параметры
            foreach (var parameter in Parameters) {
                SaveParameterNameToDatabase(parameter.Name);
                SaveParameterValueToDatabase(parameter.Value);
            }

            MessageBox.Show("Данные сохранены!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Отмена и закрытие окна
        private void Cancel_Click(object sender, RoutedEventArgs e) {
            this.Close();
        }

        // Методы для сохранения в базу данных (заглушки)
        private void SaveIconToDatabase(string iconPath) {
            // Логика сохранения иконки
        }

        private void SaveParameterNameToDatabase(string name) {
            // Логика сохранения названия параметра
        }

        private void SaveParameterValueToDatabase(string value) {
            // Логика сохранения значения параметра
        }
    }

    // Модель параметра
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
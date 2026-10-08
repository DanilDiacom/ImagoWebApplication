using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ImagoAdmin {

    /// <summary>Маленькое окно ввода одной строки (адрес odkazu, název stránky…).</summary>
    public class InputWindow : Window {

        private readonly TextBox _input;

        private InputWindow(string title, string prompt, string initial) {
            Title = title;
            Width = 480;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromRgb(0xf5, 0xf5, 0xf5));

            var root = new StackPanel { Margin = new Thickness(14) };
            root.Children.Add(new TextBlock { Text = prompt, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6) });
            _input = new TextBox { Text = initial, FontSize = 14, Padding = new Thickness(4, 3, 4, 3) };
            root.Children.Add(_input);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            var ok = new Button { Content = "OK", IsDefault = true, MinWidth = 80, Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(5, 0, 0, 0),
                                  Background = new SolidColorBrush(Color.FromRgb(0x1d, 0x4f, 0x99)), Foreground = Brushes.White, FontWeight = FontWeights.Bold, BorderThickness = new Thickness(0) };
            var cancel = new Button { Content = "Zrušit", IsCancel = true, MinWidth = 80, Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(5, 0, 0, 0) };
            ok.Click += (s, e) => DialogResult = true;
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            root.Children.Add(buttons);
            Content = root;

            Loaded += (s, e) => { _input.Focus(); _input.SelectAll(); };
        }

        /// <summary>Значение или null, если нажали «Zrušit».</summary>
        public static string? Ask(Window owner, string title, string prompt, string initial = "") {
            var w = new InputWindow(title, prompt, initial) { Owner = owner };
            return w.ShowDialog() == true ? w._input.Text : null;
        }
    }
}

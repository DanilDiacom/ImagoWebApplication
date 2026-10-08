using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ImagoLib.Models;

namespace ImagoAdmin {

    /// <summary>
    /// «🕘 Historie»: версии страницы, сохранённые перед каждой публикацией (PageDraft.GetHistory).
    /// Выбранная версия возвращается в черновик — на сайт она попадёт после «Publikovat stránku».
    /// </summary>
    public class HistoryWindow : Window {

        public PublishHistoryItem? SelectedItem { get; private set; }

        public HistoryWindow(string pageTitle, List<PublishHistoryItem> history) {
            Title = $"Historie — {pageTitle}";
            Width = 560;
            Height = 520;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromRgb(0xf5, 0xf5, 0xf5));

            var root = new DockPanel { Margin = new Thickness(12) };

            var info = new TextBlock {
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10),
                Text = history.Count == 0
                    ? "Tato stránka zatím nemá žádnou historii. Verze se ukládá automaticky při každém publikování."
                    : "Každá položka je verze stránky, která byla na webu PŘED publikováním v uvedený čas (texty a styly, bez fotografií). " +
                      "Vybranou verzi vrátíte do návrhu, zkontrolujete v náhledu a pak publikujete."
            };
            DockPanel.SetDock(info, Dock.Top);
            root.Children.Add(info);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
            var restore = new Button { Content = "↺ Vrátit tuto verzi do návrhu", IsEnabled = false, Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(5),
                                       Background = new SolidColorBrush(Color.FromRgb(0x1d, 0x4f, 0x99)), Foreground = Brushes.White, FontWeight = FontWeights.Bold, BorderThickness = new Thickness(0) };
            var close = new Button { Content = "Zavřít", Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(5), IsCancel = true };
            buttons.Children.Add(restore);
            buttons.Children.Add(close);
            DockPanel.SetDock(buttons, Dock.Bottom);
            root.Children.Add(buttons);

            var list = new ListBox { ItemsSource = history, DisplayMemberPath = nameof(PublishHistoryItem.Display), FontSize = 14 };
            list.SelectionChanged += (s, e) => restore.IsEnabled = list.SelectedItem != null;
            root.Children.Add(list);

            restore.Click += (s, e) => {
                if (list.SelectedItem is not PublishHistoryItem item) return;
                if (MessageBox.Show(this, $"Vrátit do návrhu verzi z {item.PublishedAt:dd.MM.yyyy HH:mm}?\n\nNeuložené změny textů této stránky v návrhu budou nahrazeny.",
                                    "Vrátit verzi", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
                SelectedItem = item;
                DialogResult = true;
            };

            Content = root;
        }
    }
}

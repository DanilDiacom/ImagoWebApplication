using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ImagoLib.Models;

namespace ImagoAdmin {

    /// <summary>
    /// «☰ Menu webu»: названия пунктов меню, порядок и скрытие страниц (Pages.Title, SortOrder, IsHidden).
    /// Изменения применяются к копии и сохраняются кнопкой «Uložit menu» — на сайте сразу.
    /// </summary>
    public class MenuWindow : Window {

        private class Item : INotifyPropertyChanged {
            public Pages Page { get; init; } = null!;
            public int Depth { get; init; }
            private string _title = "";
            private bool _hidden;
            public int SortOrder { get; set; }
            public string Title { get => _title; set { _title = value; Changed(); } }
            public bool IsHidden { get => _hidden; set { _hidden = value; Changed(); } }
            public string Display => new string(' ', Depth * 6) + (Depth > 0 ? "└ " : "") + Title + (IsHidden ? "   (skryto)" : "");
            public Brush Color => IsHidden ? Brushes.Gray : Brushes.Black;
            public event PropertyChangedEventHandler? PropertyChanged;
            private void Changed() {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Display)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Color)));
            }
        }

        private readonly List<Item> _items = new();
        private readonly ListBox _list;
        private readonly TextBox _title;
        private readonly CheckBox _hidden;
        private readonly Button _up, _down;
        private bool _loading;

        public bool Saved { get; private set; }

        public MenuWindow(IEnumerable<Pages> roots) {
            Title = "Menu webu";
            Width = 720;
            Height = 620;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xf5, 0xf5, 0xf5));

            void Add(IEnumerable<Pages> pages, int depth) {
                foreach (var p in pages.OrderBy(x => x.SortOrder).ThenBy(x => x.Id)) {
                    _items.Add(new Item { Page = p, Depth = depth, Title = p.Title, IsHidden = p.IsHidden, SortOrder = p.SortOrder });
                    Add(p.SubPages, depth + 1);
                }
            }
            Add(roots, 0);

            var root = new DockPanel { Margin = new Thickness(12) };

            var info = new TextBlock {
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10), Foreground = Brushes.DimGray,
                Text = "Názvy položek menu, jejich pořadí a skrytí stránek. Skrytá stránka v menu není, ale po přímé adrese zůstává dostupná. " +
                       "Změny menu se na webu projeví hned po „Uložit menu“."
            };
            DockPanel.SetDock(info, Dock.Top);
            root.Children.Add(info);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
            var save = MakeButton("💾 Uložit menu", primary: true);
            var cancel = MakeButton("Zrušit", primary: false);
            cancel.IsCancel = true;
            buttons.Children.Add(save);
            buttons.Children.Add(cancel);
            DockPanel.SetDock(buttons, Dock.Bottom);
            root.Children.Add(buttons);

            // правая часть: правка выбранного пункта
            var editor = new StackPanel { Width = 260, Margin = new Thickness(12, 0, 0, 0) };
            editor.Children.Add(new TextBlock { Text = "Název v menu:", FontWeight = FontWeights.SemiBold });
            _title = new TextBox { Margin = new Thickness(0, 4, 0, 10), FontSize = 14, Padding = new Thickness(4, 3, 4, 3) };
            editor.Children.Add(_title);
            _hidden = new CheckBox { Content = "Skrýt v menu", Margin = new Thickness(0, 0, 0, 14) };
            editor.Children.Add(_hidden);
            var moves = new StackPanel { Orientation = Orientation.Horizontal };
            _up = MakeButton("⬆ Výš", primary: false);
            _down = MakeButton("⬇ Níž", primary: false);
            moves.Children.Add(_up);
            moves.Children.Add(_down);
            editor.Children.Add(moves);
            editor.Children.Add(new TextBlock { Text = "Posouvá se v rámci stejné úrovně menu.", Foreground = Brushes.DimGray, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) });
            DockPanel.SetDock(editor, Dock.Right);
            root.Children.Add(editor);

            _list = new ListBox { FontSize = 14 };
            var template = new DataTemplate();
            var text = new FrameworkElementFactory(typeof(TextBlock));
            text.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(Item.Display)));
            text.SetBinding(TextBlock.ForegroundProperty, new System.Windows.Data.Binding(nameof(Item.Color)));
            text.SetValue(TextBlock.FontFamilyProperty, new FontFamily("Segoe UI"));
            template.VisualTree = text;
            _list.ItemTemplate = template;
            root.Children.Add(_list);

            _list.SelectionChanged += (s, e) => LoadSelected();
            _title.TextChanged += (s, e) => { if (!_loading && _list.SelectedItem is Item it) it.Title = _title.Text; };
            _hidden.Click += (s, e) => { if (_list.SelectedItem is Item it) it.IsHidden = _hidden.IsChecked == true; };
            _up.Click += (s, e) => Move(-1);
            _down.Click += (s, e) => Move(+1);
            save.Click += (s, e) => Save();

            Content = root;
            Rebuild(null);
        }

        private static Button MakeButton(string text, bool primary) => new Button {
            Content = text, Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(0, 0, 6, 0), FontWeight = FontWeights.Bold, BorderThickness = new Thickness(0),
            Background = new SolidColorBrush(primary ? System.Windows.Media.Color.FromRgb(0x1d, 0x4f, 0x99) : System.Windows.Media.Color.FromRgb(0xe8, 0xee, 0xf9)),
            Foreground = primary ? Brushes.White : new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1d, 0x4f, 0x99)),
        };

        private List<Item> Siblings(Item item) => _items.Where(i => i.Page.ParentId == item.Page.ParentId).OrderBy(i => i.SortOrder).ThenBy(i => i.Page.Id).ToList();

        private void LoadSelected() {
            var item = _list.SelectedItem as Item;
            _loading = true;
            _title.Text = item?.Title ?? "";
            _hidden.IsChecked = item?.IsHidden == true;
            _loading = false;
            _title.IsEnabled = _hidden.IsEnabled = item != null;
            var siblings = item != null ? Siblings(item) : new List<Item>();
            _up.IsEnabled = item != null && siblings.IndexOf(item) > 0;
            _down.IsEnabled = item != null && siblings.IndexOf(item) < siblings.Count - 1;
        }

        private void Move(int direction) {
            if (_list.SelectedItem is not Item item) return;
            var siblings = Siblings(item);
            var index = siblings.IndexOf(item);
            var target = index + direction;
            if (target < 0 || target >= siblings.Count) return;
            (siblings[index], siblings[target]) = (siblings[target], siblings[index]);
            for (var i = 0; i < siblings.Count; i++) siblings[i].SortOrder = i + 1;   // порядок 1..N
            Rebuild(item);
        }

        /// <summary>Пересобирает список в порядке меню (родитель, затем его подстраницы).</summary>
        private void Rebuild(Item? select) {
            var ordered = new List<Item>();
            void Add(int? parentId) {
                foreach (var it in _items.Where(i => i.Page.ParentId == parentId).OrderBy(i => i.SortOrder).ThenBy(i => i.Page.Id)) {
                    ordered.Add(it);
                    Add(it.Page.Id);
                }
            }
            Add(null);
            _list.ItemsSource = ordered;
            _list.SelectedItem = select;
            LoadSelected();
        }

        private void Save() {
            if (_items.Any(i => string.IsNullOrWhiteSpace(i.Title))) {
                MessageBox.Show(this, "Každá položka menu musí mít název.", "Menu", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            try {
                // порядок 1..N на каждом уровне (новые страницы без порядка — в конец)
                foreach (var group in _items.GroupBy(i => i.Page.ParentId)) {
                    var n = 0;
                    foreach (var it in group.OrderBy(i => i.SortOrder).ThenBy(i => i.Page.Id)) it.SortOrder = ++n;
                }
                Pages.SaveMenu(_items.Select(i => new Pages { Id = i.Page.Id, Title = i.Title.Trim(), SortOrder = i.SortOrder, IsHidden = i.IsHidden }));
                Saved = true;
                DialogResult = true;
            }
            catch (Exception ex) {
                MessageBox.Show(this, "Menu se nepodařilo uložit:\n" + ex.Message, "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

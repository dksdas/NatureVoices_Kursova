using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json;

namespace аудіобібліотека_голоси_природи
{
    /// <summary>
    /// Вікно управління плейлистами користувача.
    /// Дозволяє створювати, редагувати та видаляти плейлисти,
    /// додавати категорії та треки (окремо або через категорію).
    /// </summary>
    public partial class PlaylistWindow : Window
    {
        // ── передані ззовні ──────────────────────────────────────────────
        private readonly string _username;
        private readonly string _dataPath;
        private readonly List<MainWindow.AudioTrack> _allTracks;
        private readonly List<MainWindow.CategoryItem> _allCategories;

        // ── внутрішній стан ─────────────────────────────────────────────
        private ObservableCollection<Playlist> _playlists = new ObservableCollection<Playlist>();
        private Playlist _current;     // плейлист, що редагується зараз
        private bool _isNewPlaylist;   // true = новий, не збережений ще

        private static readonly string[] EmojiPalette = {
            "🎵","🎶","🎸","🎺","🎹","🎻","🎤","🎼",
            "🌲","🌺","🌊","⛅","🌧","🌙","☀️","🌈",
            "❤️","🔥","✨","🌟","🍌","🌼","🐾","🦋"
        };

        // ── результат для викликаючого вікна ───────────────────────────
        /// <summary>
        /// Назначається, якщо користувач натиснув "Грати плейлист". Основне вікно фільтрує за цим списком.
        /// </summary>
        public List<MainWindow.AudioTrack> SelectedPlaylistTracks { get; private set; }

        // ───────────────────────────────────────────────────────────────

        public PlaylistWindow(
            string username,
            string dataPath,
            List<MainWindow.AudioTrack> allTracks,
            List<MainWindow.CategoryItem> allCategories)
        {
            InitializeComponent();
            _username      = username;
            _dataPath      = dataPath;
            _allTracks     = allTracks;
            _allCategories = allCategories;

            LoadPlaylists();
            PlaylistListBox.ItemsSource = _playlists;
            ClearEditor();
        }

        // ── Збереження / завантаження ─────────────────────────────────────

        private string PlaylistsFilePath =>
            Path.Combine(_dataPath, $"playlists_{_username}.json");

        private void LoadPlaylists()
        {
            try
            {
                if (File.Exists(PlaylistsFilePath))
                {
                    var list = JsonConvert.DeserializeObject<List<Playlist>>(File.ReadAllText(PlaylistsFilePath));
                    if (list != null)
                        foreach (var p in list)
                            _playlists.Add(p);
                }
            }
            catch { /* дані пошкоджені — починаємо з порожнього списку */ }
        }

        private void SavePlaylists()
        {
            if (!Directory.Exists(_dataPath)) Directory.CreateDirectory(_dataPath);
            File.WriteAllText(PlaylistsFilePath,
                JsonConvert.SerializeObject(_playlists.ToList(), Formatting.Indented));
        }

        // ── Редактор ─────────────────────────────────────────────────────

        private void ClearEditor()
        {
            _current        = null;
            _isNewPlaylist  = false;
            TxtName.Text    = "";
            EmojiDisplay.Text = "🎵";
            EditorTitle.Text  = "Оберіть плейлист осліва або створіть новий";
            BtnDelete.Visibility = Visibility.Collapsed;
            BuildCategoryCheckboxes(new List<string>());
            BuildTrackCheckboxes(new List<string>(), "");
        }

        private void LoadIntoEditor(Playlist pl)
        {
            _current       = pl;
            _isNewPlaylist = false;
            TxtName.Text   = pl.Name;
            EmojiDisplay.Text = pl.Emoji;
            EditorTitle.Text  = $"Редагування: {pl.Emoji} {pl.Name}";
            BtnDelete.Visibility = Visibility.Visible;
            BuildCategoryCheckboxes(pl.Categories);
            BuildTrackCheckboxes(pl.TrackFileNames, TrackFilter.Text);
        }

        // ── Побудова списків checkbox ───────────────────────────────────

        private void BuildCategoryCheckboxes(List<string> selected)
        {
            var items = _allCategories.Select(c => new CheckItem
            {
                Label     = c.Name,
                IsChecked = selected.Contains(c.Name)
            }).ToList();
            CategoriesPanel.ItemsSource = items;
        }

        private void BuildTrackCheckboxes(List<string> selectedFileNames, string filter)
        {
            var filtered = string.IsNullOrWhiteSpace(filter)
                ? _allTracks
                : _allTracks.Where(t =>
                    t.Title.ToLower().Contains(filter.ToLower()) ||
                    (t.Category != null && t.Category.ToLower().Contains(filter.ToLower()))).ToList();

            var items = filtered.Select(t => new CheckItem
            {
                Label     = $"{t.Title}  —  {t.Category}",
                Tag       = t.FileName,
                IsChecked = selectedFileNames.Contains(t.FileName)
            }).ToList();
            TracksPanel.ItemsSource = items;
        }

        // ── Події UI ────────────────────────────────────────────────────────

        private void BtnNewPlaylist_Click(object sender, RoutedEventArgs e)
        {
            _current       = new Playlist();
            _isNewPlaylist = true;
            PlaylistListBox.SelectedItem = null;
            TxtName.Text   = "Новий плейлист";
            EmojiDisplay.Text = "🎵";
            EditorTitle.Text  = "Новий плейлист";
            BtnDelete.Visibility = Visibility.Collapsed;
            BuildCategoryCheckboxes(new List<string>());
            BuildTrackCheckboxes(new List<string>(), "");
            TxtName.Focus();
            TxtName.SelectAll();
        }

        private void PlaylistListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var pl = PlaylistListBox.SelectedItem as Playlist;
            if (pl != null) LoadIntoEditor(pl);
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null) return;

            string name = TxtName.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Введіть назву плейлиста.", "Очікуйте",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _current.Name  = name;
            _current.Emoji = EmojiDisplay.Text;

            // Зібрати обрані категорії
            _current.Categories = (CategoriesPanel.ItemsSource as IEnumerable<CheckItem>)
                ?.Where(ci => ci.IsChecked)
                .Select(ci => ci.Label)
                .ToList() ?? new List<string>();

            // Зібрати обрані треки
            var checkedTracks = (TracksPanel.ItemsSource as IEnumerable<CheckItem>)
                ?.Where(ci => ci.IsChecked)
                .Select(ci => ci.Tag)
                .ToList() ?? new List<string>();

            // Також автоматично додаємо всі треки з обраних категорій
            foreach (var cat in _current.Categories)
            {
                string catName = cat.Contains(" ") ? cat.Substring(cat.IndexOf(' ') + 1).Trim().ToLower() : cat.Trim().ToLower();
                foreach (var t in _allTracks)
                {
                    if (t.Category != null &&
                        t.Category.Split(',').Select(c => c.Trim().ToLower()).Contains(catName) &&
                        !checkedTracks.Contains(t.FileName))
                    {
                        checkedTracks.Add(t.FileName);
                    }
                }
            }

            _current.TrackFileNames = checkedTracks.Distinct().ToList();

            if (_isNewPlaylist)
            {
                _playlists.Add(_current);
                _isNewPlaylist = false;
            }

            SavePlaylists();
            PlaylistListBox.ItemsSource = null;
            PlaylistListBox.ItemsSource = _playlists;
            PlaylistListBox.SelectedItem = _current;
            EditorTitle.Text = $"Редагування: {_current.Emoji} {_current.Name}";
            BtnDelete.Visibility = Visibility.Visible;
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null) return;
            var res = MessageBox.Show(
                $"Видалити плейлист «{_current.Name}»?",
                "Підтвердження", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res != MessageBoxResult.Yes) return;

            _playlists.Remove(_current);
            SavePlaylists();
            PlaylistListBox.ItemsSource = null;
            PlaylistListBox.ItemsSource = _playlists;
            ClearEditor();
        }

        private void BtnPlayThis_Click(object sender, RoutedEventArgs e)
        {
            var pl = PlaylistListBox.SelectedItem as Playlist;
            if (pl == null)
            {
                MessageBox.Show("Спочатку оберіть плейлист.",
                    "Очікуйте", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SelectedPlaylistTracks = _allTracks
                .Where(t => pl.TrackFileNames.Contains(t.FileName))
                .ToList();

            if (SelectedPlaylistTracks.Count == 0)
            {
                MessageBox.Show("У плейлисті немає треків. Спочатку додайте іх.",
                    "Порожній плейлист", MessageBoxButton.OK, MessageBoxImage.Information);
                SelectedPlaylistTracks = null;
                return;
            }

            this.DialogResult = true;
            this.Close();
        }

        // ── Емоджі ────────────────────────────────────────────────────────────

        private void BtnEmoji_Click(object sender, RoutedEventArgs e)
        {
            // Простий пікер емоджі (Popup з кнопками)
            var picker = new Window
            {
                Title       = "Оберіть емоджі",
                Width       = 340,
                Height      = 210,
                Background  = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1E1E1E")),
                ResizeMode  = ResizeMode.NoResize,
                Owner       = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            var wrap = new WrapPanel { Margin = new Thickness(12) };
            foreach (var em in EmojiPalette)
            {
                var btn = new Button
                {
                    Content         = em,
                    FontSize        = 26,
                    Width           = 46,
                    Height          = 46,
                    Background      = System.Windows.Media.Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Cursor          = System.Windows.Input.Cursors.Hand,
                    Tag             = em
                };
                btn.Click += (s, ev) =>
                {
                    EmojiDisplay.Text = (string)((Button)s).Tag;
                    picker.Close();
                };
                wrap.Children.Add(btn);
            }
            picker.Content = wrap;
            picker.ShowDialog();
        }

        // ── Події фільтра / перемікачів ────────────────────────────────

        private void TrackFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_current == null) return;
            BuildTrackCheckboxes(_current.TrackFileNames, TrackFilter.Text);
        }

        private void CategoryChecked(object sender, RoutedEventArgs e) { /* зміни зберігаються при BtnSave */ }
        private void TrackChecked(object sender, RoutedEventArgs e)    { /* зміни зберігаються при BtnSave */ }
    }

    // ── Допоміжна модель для перемікачів ────────────────────────────────────────

    /// <summary>
    /// Елемент для CheckBox-списків категорій та треків.
    /// </summary>
    public class CheckItem : System.ComponentModel.INotifyPropertyChanged
    {
        public string Label { get; set; }
        /// <summary>FileName (для треків) або Name (для категорій).</summary>
        public string Tag   { get; set; }

        private bool _checked;
        public bool IsChecked
        {
            get => _checked;
            set
            {
                _checked = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsChecked)));
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    }
}

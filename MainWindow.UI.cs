using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Newtonsoft.Json;

namespace аудіобібліотека_голоси_природи
{
    public partial class MainWindow
    {
        // ── Теми ───────────────────────────────────────────────────────────────────

        private void DayButton_Click(object sender, RoutedEventArgs e) =>
            SetTheme("#141F16", "#1A281D", "#E2ECE4", true);

        private void NightButton_Click(object sender, RoutedEventArgs e) =>
            SetTheme("#121212", "#181818", "#FFFFFF", false);

        private void SetTheme(string bg, string side, string txt, bool isDay)
        {
            var b = Brush(bg);
            var s = Brush(side);
            var t = Brush(txt);
            var cardBg = isDay ? Brush("#233327") : Brush("#282828");
            var playerBg = isDay ? Brush("#1A281D") : Brush("#181818");
            var searchBg = isDay ? Brush("#233327") : Brush("#282828");

            RootWindow.Background = b;
            SidePanel.Background = s;
            MainTitle.Foreground = t;
            LogoText.Foreground = t;
            PlayingNow.Foreground = t;
            NowPlayingLabel.Foreground = isDay ? Brush("#A0B2A5") : Brush("#B3B3B3");
            PlayerBar.Background = playerBg;
            SearchBorder.Background = searchBg;
            this.Resources["CardBackground"] = cardBg;
            this.Resources["PrimaryText"] = t;
            ThemePanel.Background = isDay ? Brush("#1A281D") : Brush("#282828");
            DayBtn.Background = isDay ? Brush("#1DB954") : Brushes.Transparent;
            NightBtn.Background = !isDay ? Brush("#1DB954") : Brushes.Transparent;
            DayBtnText.Foreground = isDay ? Brushes.White : Brush("#B3B3B3");
            NightBtnText.Foreground = !isDay ? Brushes.White : Brush("#B3B3B3");
        }

        private static SolidColorBrush Brush(string hex) =>
            new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));

        // ── Фільтрація / пошук ─────────────────────────────────────────────────

        private void Filter_Click(object sender, RoutedEventArgs e)
        {
            CategoryListBox.SelectedItem = null;
            BtnHome.Background = Brush("#C8E6C8");
            BtnFavorites.Background = Brushes.Transparent;
            BtnPlaylists.Background = Brushes.Transparent;
            SoundsList.ItemsSource = allTracks;
        }

        private void CategoryListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = CategoryListBox.SelectedItem as CategoryItem;
            if (selected == null) return;

            BtnHome.Background = Brushes.Transparent;
            BtnFavorites.Background = Brushes.Transparent;
            BtnPlaylists.Background = Brushes.Transparent;

            string s = selected.Name;
            if (s.Contains(" ")) s = s.Substring(s.IndexOf(' ') + 1);
            string cat = s.Trim().ToLower();

            SoundsList.ItemsSource = new ObservableCollection<AudioTrack>(
                allTracks.Where(t =>
                    !string.IsNullOrEmpty(t.Category) &&
                    t.Category.Split(',').Select(c => c.Trim().ToLower()).Contains(cat)));
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string s = SearchBox.Text.ToLower();
            SoundsList.ItemsSource = string.IsNullOrWhiteSpace(s)
                ? allTracks
                : (System.Collections.IEnumerable)new ObservableCollection<AudioTrack>(
                    allTracks.Where(x =>
                        x.Title.ToLower().Contains(s) ||
                        (x.Category != null && x.Category.ToLower().Contains(s))));
        }

        // ── Улюблені ───────────────────────────────────────────────────────────

        private void Favorites_Click(object sender, RoutedEventArgs e)
        {
            CategoryListBox.SelectedItem = null;
            BtnHome.Background = Brushes.Transparent;
            BtnFavorites.Background = Brush("#C8E6C8");
            BtnPlaylists.Background = Brushes.Transparent;

            string usersPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "users.json");
            if (File.Exists(usersPath) && !string.IsNullOrEmpty(currentUsername))
            {
                var users = JsonConvert.DeserializeObject<List<LoginWindow.UserData>>(File.ReadAllText(usersPath));
                var user = users?.Find(u => u.Username == currentUsername);
                if (user?.FavoriteTracks != null)
                {
                    SoundsList.ItemsSource = new ObservableCollection<AudioTrack>(
                        allTracks.Where(t => user.FavoriteTracks.Contains(t.Title)));
                    return;
                }
            }
            SoundsList.ItemsSource = new ObservableCollection<AudioTrack>();
        }

        private void Favorite_Click(object sender, RoutedEventArgs e)
        {
            var t = (sender as System.Windows.Controls.Button)?.DataContext as AudioTrack;
            if (t == null || string.IsNullOrEmpty(currentUsername)) return;

            string usersPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "users.json");
            if (!File.Exists(usersPath)) return;

            var users = JsonConvert.DeserializeObject<List<LoginWindow.UserData>>(File.ReadAllText(usersPath))
                        ?? new List<LoginWindow.UserData>();
            var user = users.Find(u => u.Username == currentUsername);
            if (user == null) return;
            if (user.FavoriteTracks == null) user.FavoriteTracks = new List<string>();

            if (user.FavoriteTracks.Contains(t.Title))
            { user.FavoriteTracks.Remove(t.Title); t.FavoriteIcon = "♡"; }
            else
            { user.FavoriteTracks.Add(t.Title); t.FavoriteIcon = "♥"; }

            File.WriteAllText(usersPath, JsonConvert.SerializeObject(users, Formatting.Indented));

            var button = sender as System.Windows.Controls.Button;
            if (button != null)
            {
                var scale = new ScaleTransform(1.0, 1.0);
                button.RenderTransform = scale;
                button.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
                var anim = new DoubleAnimation { From = 1.0, To = 1.4, Duration = TimeSpan.FromMilliseconds(150), AutoReverse = true };
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
            }
        }

        // ── Плейлисти ──────────────────────────────────────────────────────────

        private void BtnPlaylists_Click(object sender, RoutedEventArgs e)
        {
            CategoryListBox.SelectedItem = null;
            BtnHome.Background = Brushes.Transparent;
            BtnFavorites.Background = Brushes.Transparent;
            BtnPlaylists.Background = Brush("#C8E6C8");

            var pw = new PlaylistWindow(
                currentUsername,
                DataPath,
                allTracks.ToList(),
                categories.ToList())
            {
                Owner = this
            };

            pw.ShowDialog();

            if (pw.SelectedPlaylistTracks != null)
            {
                SoundsList.ItemsSource = new ObservableCollection<AudioTrack>(pw.SelectedPlaylistTracks);
            }
            else
            {
                BtnPlaylists.Background = Brushes.Transparent;
                BtnHome.Background = Brush("#C8E6C8");
                SoundsList.ItemsSource = allTracks;
            }
        }
    }
}
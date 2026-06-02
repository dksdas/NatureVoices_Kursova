using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.ComponentModel;
using Newtonsoft.Json;
using System.Windows.Media.Animation;
using Microsoft.VisualBasic;
using System.Net.Http;
using System.Threading.Tasks;

namespace аудіобібліотека_голоси_природи
{
    /// <summary>
    /// Головне вікно додатку, що реалізує логіку медіаплеєра, фільтрації, синхронізації та адміністрування аудіобібліотеки.
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private MediaPlayer mediaPlayer = new MediaPlayer();
        private DispatcherTimer timer = new DispatcherTimer();
        private ObservableCollection<AudioTrack> allTracks = new ObservableCollection<AudioTrack>();
        private ObservableCollection<CategoryItem> categories = new ObservableCollection<CategoryItem>();
        private int currentTrackIndex = -1;
        private bool isPlaying = false;
        private string userRole;
        private string currentUsername;
        private DispatcherTimer sleepTimer;
        private int timeLeftInSeconds;
        private List<MediaPlayer> durationLoaders = new List<MediaPlayer>();
        private static readonly HttpClient httpClient = new HttpClient();
        private const string ServerBase = "https://github.com/ctoizacem807-create/files/releases/download/111/";
        private Visibility _adminControlsVisibility = Visibility.Collapsed;

        /// <summary>
        /// Властивість для керування видимістю елементів керування адміністратора з підтримкою сповіщення про зміну стану.
        /// </summary>
        public Visibility AdminControlsVisibility
        {
            get { return _adminControlsVisibility; }
            set { _adminControlsVisibility = value; OnPropertyChanged("AdminControlsVisibility"); }
        }

        private string DataPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
        private string JsonPath => Path.Combine(DataPath, "tracks.json");

        /// <summary>
        /// Модель даних для опису окремого аудіотреку з динамічним оновленням інтерфейсу користувача.
        /// </summary>
        public class AudioTrack : INotifyPropertyChanged
        {
            public string Title { get; set; }
            public string Category { get; set; }
            public string FileName { get; set; }
            private string _dur = "--:--";
            public string Duration { get { return _dur; } set { _dur = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Duration")); } }
            private string _fav = "♡";
            public string FavoriteIcon { get { return _fav; } set { _fav = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("FavoriteIcon")); } }
            public event PropertyChangedEventHandler PropertyChanged;
        }

        /// <summary>
        /// Елемент категорії для класифікації та групування аудіофайлів.
        /// </summary>
        public class CategoryItem
        {
            public string Name { get; set; }
        }

        /// <summary>
        /// Контейнер для збереження та серіалізації повної структури даних додатку в JSON формат.
        /// </summary>
        public class AppData
        {
            public List<AudioTrack> Tracks { get; set; }
            public List<CategoryItem> Categories { get; set; }
        }

        /// <summary>
        /// Базовий конструктор за замовчуванням для ініціалізації компонентів XAML та контексту даних.
        /// </summary>
        public MainWindow() { InitializeComponent(); this.DataContext = this; }

        /// <summary>
        /// Конструктор головного вікна з передачею параметрів авторизованої сесії користувача та ініціалізацією служб.
        /// </summary>
        public MainWindow(string username, string role) : this()
        {
            this.currentUsername = username;
            this.userRole = role;
            if (userRole == "Admin")
            {
                MainTitle.Text += " (Admin)";
                AdminPanel.Visibility = Visibility.Visible;
                AdminControlsVisibility = Visibility.Visible;
            }
            LoadData();
            SyncFromServer();
            timer.Interval = TimeSpan.FromMilliseconds(200);
            timer.Tick += Timer_Tick;
            sleepTimer = new DispatcherTimer();
            sleepTimer.Interval = TimeSpan.FromSeconds(1);
            sleepTimer.Tick += SleepTimer_Tick;
            NightButton_Click(null, null);
        }

        /// <summary>
        /// Завантажує локальні конфігурації, списки треків, категорії та відновлює прапорці улюбленого аудіо для користувача.
        /// </summary>
        private void LoadData()
        {
            if (!Directory.Exists(DataPath)) Directory.CreateDirectory(DataPath);
            try
            {
                if (File.Exists(JsonPath))
                {
                    var data = JsonConvert.DeserializeObject<AppData>(File.ReadAllText(JsonPath));
                    if (data != null)
                    {
                        allTracks = new ObservableCollection<AudioTrack>(data.Tracks ?? new List<AudioTrack>());
                        categories = new ObservableCollection<CategoryItem>(data.Categories ?? new List<CategoryItem>());
                    }
                }
            }
            catch
            {
                allTracks = new ObservableCollection<AudioTrack>();
                categories = new ObservableCollection<CategoryItem>();
            }

            if (categories == null) categories = new ObservableCollection<CategoryItem>();
            if (allTracks == null) allTracks = new ObservableCollection<AudioTrack>();

            if (categories.Count == 0)
            {
                categories.Add(new CategoryItem { Name = "🌲 Ліс" });
                categories.Add(new CategoryItem { Name = "🌊 Вода" });
            }

            string usersPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "users.json");
            if (File.Exists(usersPath) && !string.IsNullOrEmpty(currentUsername))
            {
                var users = JsonConvert.DeserializeObject<List<LoginWindow.UserData>>(File.ReadAllText(usersPath));
                var user = users?.Find(u => u.Username == currentUsername);
                if (user != null && user.FavoriteTracks != null)
                {
                    foreach (var t in allTracks)
                    {
                        if (user.FavoriteTracks.Contains(t.Title)) t.FavoriteIcon = "♥";
                        else t.FavoriteIcon = "♡";
                    }
                }
            }

            SoundsList.ItemsSource = allTracks;
            CategoryListBox.ItemsSource = categories;
            foreach (var t in allTracks) UpdateTrackDuration(t);
        }

        /// <summary>
        /// Виконує збереження поточного стану медіатеки та категорій у локальне JSON-сховище.
        /// </summary>
        private void SaveData()
        {
            File.WriteAllText(JsonPath, JsonConvert.SerializeObject(new AppData { Tracks = allTracks.ToList(), Categories = categories.ToList() }, Formatting.Indented));
        }

        /// <summary>
        /// Асинхронно синхронізує списки медіафайлів із віддаленим сервером релізів та завантажує відсутній контент.
        /// </summary>
        private async void SyncFromServer()
        {
            try
            {
                string serverJson = await httpClient.GetStringAsync(ServerBase + "tracks.json");
                var serverData = JsonConvert.DeserializeObject<AppData>(serverJson);
                if (serverData == null) return;

                bool changed = false;

                if (serverData.Categories != null)
                {
                    foreach (var cat in serverData.Categories)
                    {
                        if (!categories.Any(c => c.Name == cat.Name))
                        {
                            categories.Add(cat);
                            changed = true;
                        }
                    }
                }

                if (serverData.Tracks != null)
                {
                    foreach (var track in serverData.Tracks)
                    {
                        if (!allTracks.Any(t => t.FileName == track.FileName))
                        {
                            allTracks.Add(track);
                            changed = true;
                        }
                    }
                }

                if (changed) SaveData();

                foreach (var track in allTracks.ToList())
                {
                    string localPath = Path.Combine(DataPath, track.FileName);
                    if (!File.Exists(localPath))
                    {
                        try
                        {
                            byte[] fileData = await httpClient.GetByteArrayAsync(ServerBase + Uri.EscapeDataString(track.FileName));
                            File.WriteAllBytes(localPath, fileData);
                            UpdateTrackDuration(track);
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Обробник події для додавання нового аудіофайлу адміністратором системи через діалогове вікно.
        /// </summary>
        private void AddTrack_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NewTrackTitle.Text)) return;
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Audio files (*.mp3)|*.mp3" };
            if (dlg.ShowDialog() == true)
            {
                string fn = Path.GetFileName(dlg.FileName);
                string dest = Path.Combine(DataPath, fn);
                if (!File.Exists(dest)) File.Copy(dlg.FileName, dest);
                var t = new AudioTrack { Title = NewTrackTitle.Text, Category = NewTrackCategory.Text, FileName = fn };
                allTracks.Add(t);
                SaveData();
                UpdateTrackDuration(t);
                NewTrackTitle.Clear(); NewTrackCategory.Clear();
            }
        }

        /// <summary>
        /// Обробник події створення нової категорії звуків за допомогою діалогового вікна введення.
        /// </summary>
        private void AddCategory_Click(object sender, RoutedEventArgs e)
        {
            string input = Interaction.InputBox("Введіть іконку та назву:", "Нова категорія", "📁 Нова категорія");
            if (!string.IsNullOrWhiteSpace(input)) { categories.Add(new CategoryItem { Name = input }); SaveData(); }
        }

        /// <summary>
        /// Обробник контекстного меню для видалення існуючої категорії з медіатеки додатку.
        /// </summary>
        private void DeleteCategory_Click(object sender, RoutedEventArgs e)
        {
            var item = (sender as MenuItem).DataContext as CategoryItem;
            if (item != null) { categories.Remove(item); SaveData(); }
        }

        /// <summary>
        /// Обробник події видалення аудіозапису із загального списку з підтвердженням операції.
        /// </summary>
        private void DeleteTrack_Click(object sender, RoutedEventArgs e)
        {
            var t = (sender as Button).DataContext as AudioTrack;
            if (t != null && MessageBox.Show($"Видалити {t.Title}?", "Видалення", MessageBoxButton.YesNo) == MessageBoxResult.Yes) { allTracks.Remove(t); SaveData(); }
        }

        /// <summary>
        /// Скидає активні фільтри категорій і повертає відображення повного списку аудіозаписів на головному екрані.
        /// </summary>
        private void Filter_Click(object sender, RoutedEventArgs e)
        {
            CategoryListBox.SelectedItem = null;
            BtnHome.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C8E6C8"));
            BtnFavorites.Background = Brushes.Transparent;
            SoundsList.ItemsSource = allTracks;
        }

        /// <summary>
        /// Реалізує фільтрацію списку звуків за обраною категорією з урахуванням множинних тегів через кому.
        /// </summary>
        private void CategoryListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = CategoryListBox.SelectedItem as CategoryItem;
            if (selected != null)
            {
                BtnHome.Background = Brushes.Transparent;
                BtnFavorites.Background = Brushes.Transparent;

                string s = selected.Name;
                if (s.Contains(" "))
                {
                    int i = s.IndexOf(' ');
                    s = s.Substring(i + 1);
                }
                string selectedCatName = s.Trim().ToLower();

                SoundsList.ItemsSource = new ObservableCollection<AudioTrack>(
                    allTracks.Where(t => {
                        if (string.IsNullOrEmpty(t.Category)) return false;
                        var trackCategories = t.Category.Split(',')
                                                      .Select(c => c.Trim().ToLower())
                                                      .ToList();

                        return trackCategories.Contains(selectedCatName);
                    })
                );
            }
        }

        /// <summary>
        /// Виконує динамічний контекстний пошук та фільтрацію аудіотреків за назвою або категорією при введенні тексту.
        /// </summary>
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string s = SearchBox.Text.ToLower();
            if (string.IsNullOrWhiteSpace(s))
            {
                SoundsList.ItemsSource = allTracks;
                return;
            }

            SoundsList.ItemsSource = new ObservableCollection<AudioTrack>(
                allTracks.Where(x =>
                    x.Title.ToLower().Contains(s) ||
                    (x.Category != null && x.Category.ToLower().Contains(s))
                )
            );
        }

        /// <summary>
        /// Здійснює відкриття файлу, запуск відтворення обраного треку та оновлення статусних елементів інтерфейсу.
        /// </summary>
        private void PlayTrack(AudioTrack t)
        {
            if (t == null) return;
            string p = Path.Combine(DataPath, t.FileName);
            if (File.Exists(p))
            {
                mediaPlayer.Open(new Uri(p, UriKind.Absolute));
                mediaPlayer.Play();
                PlayingNow.Text = t.Title;
                PlayPauseIcon.Text = "⏸";
                isPlaying = true;
                timer.Start();
                currentTrackIndex = allTracks.IndexOf(t);
            }
        }

        /// <summary>
        /// Подія таймера для періодичного оновлення позиції повзунка таймлайну та текстового лічильника часу.
        /// </summary>
        private void Timer_Tick(object sender, EventArgs e)
        {
            if (mediaPlayer.NaturalDuration.HasTimeSpan)
            {
                TimelineSlider.Maximum = mediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
                if (!TimelineSlider.IsMouseCaptureWithin)
                {
                    TimelineSlider.Value = mediaPlayer.Position.TotalSeconds;
                }
                TimeStatus.Text = $"{mediaPlayer.Position:m\\:ss} / {mediaPlayer.NaturalDuration.TimeSpan:m\\:ss}";
            }
        }

        /// <summary>
        /// Обробник події для глобальної кнопки відтворення та призупинення поточного аудіофайлу.
        /// </summary>
        private void PlayPause_Click(object sender, RoutedEventArgs e)
        {
            if (currentTrackIndex == -1 && allTracks.Count > 0) { PlayTrack(allTracks[0]); return; }
            if (isPlaying) { mediaPlayer.Pause(); PlayPauseIcon.Text = "▶"; } else { mediaPlayer.Play(); PlayPauseIcon.Text = "⏸"; }
            isPlaying = !isPlaying;
        }

        /// <summary>
        /// Оновлює поточну позицію плеєра при зміні значення повзунка користувачем за допомогою миші.
        /// </summary>
        private void TimelineSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TimelineSlider.IsMouseCaptureWithin && mediaPlayer.NaturalDuration.HasTimeSpan)
            {
                mediaPlayer.Position = TimeSpan.FromSeconds(TimelineSlider.Value);
            }
        }

        /// <summary>
        /// Забезпечує миттєве перемотування аудіо в точку кліку миші по повзунку треку.
        /// </summary>
        private void TimelineSlider_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var slider = sender as Slider;
            if (slider == null || !mediaPlayer.NaturalDuration.HasTimeSpan) return;
            double x = e.GetPosition(slider).X;
            double percent = x / slider.ActualWidth;
            double val = percent * slider.Maximum;
            slider.Value = val;
            mediaPlayer.Position = TimeSpan.FromSeconds(val);
        }

        /// <summary>
        /// Обробник події натискання на кнопку старту конкретного треку зі списку.
        /// </summary>
        private void PlayButton_Click(object sender, RoutedEventArgs e) => PlayTrack((sender as Button).DataContext as AudioTrack);

        /// <summary>
        /// Перемикає медіаплеєр на наступний трек у списку з можливістю циклічного переходу на початок.
        /// </summary>
        private void Next_Click(object sender, RoutedEventArgs e)
        {
            if (allTracks.Count == 0) return;
            if (currentTrackIndex >= allTracks.Count - 1)
            {
                PlayTrack(allTracks[0]);
            }
            else
            {
                PlayTrack(allTracks[currentTrackIndex + 1]);
            }
        }

        /// <summary>
        /// Перемикає медіаплеєр на попередній трек у списку з можливістю циклічного переходу в кінець.
        /// </summary>
        private void Prev_Click(object sender, RoutedEventArgs e)
        {
            if (allTracks.Count == 0) return;
            if (currentTrackIndex <= 0)
            {
                PlayTrack(allTracks[allTracks.Count - 1]);
            }
            else
            {
                PlayTrack(allTracks[currentTrackIndex - 1]);
            }
        }

        /// <summary>
        /// Обробник події для синхронізації рівня звуку медіаплеєра зі значенням повзунка гучності.
        /// </summary>
        private void Volume_Changed(object sender, RoutedPropertyChangedEventArgs<double> e) => mediaPlayer.Volume = e.NewValue;

        /// <summary>
        /// Фоново відкриває медіафайл для зчитування його точної тривалості та оновлення моделі даних у списку.
        /// </summary>
        private void UpdateTrackDuration(AudioTrack t)
        {
            string p = Path.Combine(DataPath, t.FileName);
            if (File.Exists(p))
            {
                MediaPlayer mp = new MediaPlayer();
                durationLoaders.Add(mp);
                mp.Open(new Uri(p, UriKind.Absolute));
                mp.MediaOpened += (s, ev) => {
                    if (mp.NaturalDuration.HasTimeSpan)
                    {
                        t.Duration = mp.NaturalDuration.TimeSpan.ToString(@"m\:ss");
                    }
                    mp.Close();
                    durationLoaders.Remove(mp);
                };
                mp.MediaFailed += (s, ev) => {
                    durationLoaders.Remove(mp);
                };
            }
        }

        /// <summary>
        /// Встановлює параметри стилізації для зеленої лісової (денної) теми оформлення додатку.
        /// </summary>
        private void DayButton_Click(object sender, RoutedEventArgs e) => SetTheme("#141F16", "#1A281D", "#E2ECE4", true);

        /// <summary>
        /// Встановлює параметри стилізації для класичної темної (нічної) теми оформлення додатку.
        /// </summary>
        private void NightButton_Click(object sender, RoutedEventArgs e) => SetTheme("#121212", "#181818", "#FFFFFF", false);

        /// <summary>
        /// Здійснює динамічний перерахунок і каскадне перепідключення кольорових ресурсів вікна для зміни тем оформлення.
        /// </summary>
        private void SetTheme(string bg, string side, string txt, bool isDay)
        {
            var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bg));
            var s = new SolidColorBrush((Color)ColorConverter.ConvertFromString(side));
            var t = new SolidColorBrush((Color)ColorConverter.ConvertFromString(txt));

            var cardBg = isDay ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#233327")) : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#282828"));
            var playerBg = isDay ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A281D")) : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#181818"));
            var searchBg = isDay ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#233327")) : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#282828"));

            RootWindow.Background = b;
            SidePanel.Background = s;
            MainTitle.Foreground = t;
            LogoText.Foreground = t;
            PlayingNow.Foreground = t;
            NowPlayingLabel.Foreground = isDay ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A0B2A5")) : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B3B3B3"));

            PlayerBar.Background = playerBg;
            SearchBorder.Background = searchBg;

            this.Resources["CardBackground"] = cardBg;
            this.Resources["PrimaryText"] = t;

            ThemePanel.Background = isDay ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A281D")) : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#282828"));
            DayBtn.Background = isDay ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1DB954")) : Brushes.Transparent;
            NightBtn.Background = !isDay ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1DB954")) : Brushes.Transparent;
            DayBtnText.Foreground = isDay ? Brushes.White : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B3B3B3"));
            NightBtnText.Foreground = !isDay ? Brushes.White : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B3B3B3"));
        }

        /// <summary>
        /// Обирає випадковий трек із поточної колекції та запускає його відтворення за допомогою генератора чисел.
        /// </summary>
        private void Random_Click(object sender, RoutedEventArgs e)
        {
            if (allTracks.Count == 0) return;
            Random r = new Random();
            int index = r.Next(allTracks.Count);
            PlayTrack(allTracks[index]);
        }

        /// <summary>
        /// Конфігурує та запускає таймер автоматичного вимкнення відтворення на основі вибору користувача з комбобоксу.
        /// </summary>
        private void SleepTimerBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sleepTimer == null) return;
            var item = SleepTimerBox.SelectedItem as ComboBoxItem;
            if (item == null || item.Content.ToString() == "Вимкнено")
            {
                sleepTimer.Stop();
                return;
            }

            string text = item.Content.ToString();
            if (text.Contains("15")) timeLeftInSeconds = 15 * 60;
            else if (text.Contains("30")) timeLeftInSeconds = 30 * 60;
            else if (text.Contains("60")) timeLeftInSeconds = 60 * 60;

            sleepTimer.Start();
        }

        /// <summary>
        /// Керує зворотним відліком таймера сну та зупиняє аудіопотік після завершення встановленого часу.
        /// </summary>
        private void SleepTimer_Tick(object sender, EventArgs e)
        {
            if (timeLeftInSeconds > 0)
            {
                timeLeftInSeconds--;
                if (timeLeftInSeconds == 0)
                {
                    mediaPlayer.Pause();
                    PlayPauseIcon.Text = "▶";
                    isPlaying = false;
                    sleepTimer.Stop();
                    SleepTimerBox.SelectedIndex = 0;
                }
            }
        }

        /// <summary>
        /// Додає або видаляє трек із масиву обраного конкретного користувача з викликом WPF-анімації пульсації кнопки.
        /// </summary>
        private void Favorite_Click(object sender, RoutedEventArgs e)
        {
            var t = (sender as Button).DataContext as AudioTrack;
            if (t == null || string.IsNullOrEmpty(currentUsername)) return;

            string usersPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "users.json");
            if (!File.Exists(usersPath)) return;

            var users = JsonConvert.DeserializeObject<List<LoginWindow.UserData>>(File.ReadAllText(usersPath)) ?? new List<LoginWindow.UserData>();
            var user = users.Find(u => u.Username == currentUsername);
            if (user == null) return;

            if (user.FavoriteTracks == null) user.FavoriteTracks = new List<string>();

            if (user.FavoriteTracks.Contains(t.Title))
            {
                user.FavoriteTracks.Remove(t.Title);
                t.FavoriteIcon = "♡";
            }
            else
            {
                user.FavoriteTracks.Add(t.Title);
                t.FavoriteIcon = "♥";
            }

            File.WriteAllText(usersPath, JsonConvert.SerializeObject(users, Formatting.Indented));

            var button = sender as Button;
            if (button != null)
            {
                var scale = new ScaleTransform(1.0, 1.0);
                button.RenderTransform = scale;
                button.RenderTransformOrigin = new Point(0.5, 0.5);
                var animation = new DoubleAnimation
                {
                    From = 1.0,
                    To = 1.4,
                    Duration = TimeSpan.FromMilliseconds(150),
                    AutoReverse = true
                };
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
            }
        }

        /// <summary>
        /// Відображає в головній стрічці лише ті аудіозаписи, які користувач додав до свого персонального списку обраного.
        /// </summary>
        private void Favorites_Click(object sender, RoutedEventArgs e)
        {
            CategoryListBox.SelectedItem = null;
            BtnHome.Background = Brushes.Transparent;
            BtnFavorites.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C8E6C8"));

            string usersPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "users.json");
            if (File.Exists(usersPath) && !string.IsNullOrEmpty(currentUsername))
            {
                var users = JsonConvert.DeserializeObject<List<LoginWindow.UserData>>(File.ReadAllText(usersPath));
                var user = users?.Find(u => u.Username == currentUsername);
                if (user != null && user.FavoriteTracks != null)
                {
                    var favTracks = new ObservableCollection<AudioTrack>(
                        allTracks.Where(t => user.FavoriteTracks.Contains(t.Title))
                    );
                    SoundsList.ItemsSource = favTracks;
                    return;
                }
            }
            SoundsList.ItemsSource = new ObservableCollection<AudioTrack>();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Допоміжний метод для виклику події оновлення прив'язаних властивостей інтерфейсу.
        /// </summary>
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
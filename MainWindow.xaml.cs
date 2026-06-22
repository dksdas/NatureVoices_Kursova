using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
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
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private MediaPlayer mediaPlayer = new MediaPlayer();
        private DispatcherTimer timer = new DispatcherTimer();
        private ObservableCollection<AudioTrack> allTracks = new ObservableCollection<AudioTrack>();
        private ObservableCollection<CategoryItem> categories = new ObservableCollection<CategoryItem>();
        private int currentTrackIndex = -1;
        private bool isPlaying = false;

        private bool isShuffleOn = false;
        private Random rng = new Random();

        private string userRole;
        private string currentUsername;
        private DispatcherTimer sleepTimer;
        private int timeLeftInSeconds;
        private List<MediaPlayer> durationLoaders = new List<MediaPlayer>();
        private static readonly HttpClient httpClient = new HttpClient();
        private const string ServerBase = "https://github.com/ctoizacem807-create/files/releases/download/111/";
        private Visibility _adminControlsVisibility = Visibility.Collapsed;

        public Visibility AdminControlsVisibility
        {
            get { return _adminControlsVisibility; }
            set { _adminControlsVisibility = value; OnPropertyChanged("AdminControlsVisibility"); }
        }

        private string DataPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
        private string JsonPath => Path.Combine(DataPath, "tracks.json");

        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = this;
            // SMTC requires HWND — initialize after window handle is created
            this.SourceInitialized += (s, e) => InitSmtc();
        }

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

            mediaPlayer.MediaEnded += (s, ev) => Dispatcher.Invoke(() => PlayNext());

            NightButton_Click(null, null);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

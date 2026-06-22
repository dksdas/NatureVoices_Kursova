using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using Newtonsoft.Json;

namespace аудіобібліотека_голоси_природи
{
    /// <summary>
    /// Логіка взаємодії для вікна авторизації та реєстрації користувачів комплексу.
    /// </summary>
    public partial class LoginWindow : Window
    {
        private string usersPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "users.json");
        private bool isLoginMode = true;

        /// <summary>
        /// Модель даних, що описує профіль та персональні налаштування користувача системи.
        /// </summary>
        public class UserData
        {
            public string Username { get; set; }
            public string PasswordHash { get; set; }
            public string Role { get; set; }
            public List<string> FavoriteTracks { get; set; } = new List<string>();
        }

        /// <summary>
        /// Ініціалізує компоненти вікна авторизації та перевіряє наявність локальних директорій сховища.
        /// </summary>
        public LoginWindow()
        {
            InitializeComponent();
            string dir = Path.GetDirectoryName(usersPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        }

        /// <summary>
        /// Перемикає режим відображення інтерфейсу між автентифікацією та реєстрацією нового облікового запису.
        /// </summary>
        private void SwitchMode_Click(object sender, RoutedEventArgs e)
        {
            isLoginMode = !isLoginMode;
            if (isLoginMode)
            {
                TitleTxt.Text = "Увійти";
                MainActionBtn.Content = "УВІЙТИ";
                SwitchModeBtn.Content = "Немає акаунту? Зареєструватися";
            }
            else
            {
                TitleTxt.Text = "Реєстрація";
                MainActionBtn.Content = "СТВОРИТИ АКАУНТ";
                SwitchModeBtn.Content = "Вже є акаунт? Увійти";
            }
        }

        /// <summary>
        /// Відображає красиве мінімалістичне спливаюче повідомлення.
        /// </summary>
        private async void ShowNotification(string message, string icon = "🌿", string type = "info")
        {
            NotificationText.Text = message;
            NotificationIcon.Text = icon;

            if (type == "success")
            {
                NotificationBorder.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1DB954"));
                NotificationText.Foreground = System.Windows.Media.Brushes.Black;
            }
            else if (type == "error")
            {
                NotificationBorder.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E22134"));
                NotificationText.Foreground = System.Windows.Media.Brushes.White;
            }
            else // warning / info
            {
                NotificationBorder.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF9800"));
                NotificationText.Foreground = System.Windows.Media.Brushes.Black;
            }

            var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(250));
            var slideDown = new System.Windows.Media.Animation.ThicknessAnimation(new Thickness(0, -40, 0, 0), new Thickness(0, 10, 0, 0), TimeSpan.FromMilliseconds(250));

            NotificationBorder.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            NotificationBorder.BeginAnimation(FrameworkElement.MarginProperty, slideDown);

            await System.Threading.Tasks.Task.Delay(2500);

            var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(250));
            var slideUp = new System.Windows.Media.Animation.ThicknessAnimation(new Thickness(0, 10, 0, 0), new Thickness(0, -40, 0, 0), TimeSpan.FromMilliseconds(250));

            NotificationBorder.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            NotificationBorder.BeginAnimation(FrameworkElement.MarginProperty, slideUp);
        }

        /// <summary>
        /// Керує основним потоком авторизації або реєстрації залежно від активного стану форми.
        /// </summary>
        private void MainAction_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtUsername.Text) || string.IsNullOrWhiteSpace(TxtPassword.Password))
            {
                ShowNotification("Заповніть логін і пароль!", "⚠️", "warning");
                return;
            }
            var users = LoadUsers();
            string hash = HashPassword(TxtPassword.Password);

            if (isLoginMode)
            {
                var user = users.Find(u => u.Username == TxtUsername.Text && u.PasswordHash == hash);
                if (user != null)
                {
                    new MainWindow(user.Username, user.Role).Show();
                    this.Close();
                }
                else ShowNotification("Невірний логін або пароль!", "❌", "error");
            }
            else
            {
                if (users.Exists(u => u.Username == TxtUsername.Text)) { ShowNotification("Цей логін уже зайнятий!", "⚠️", "warning"); return; }
                users.Add(new UserData
                {
                    Username = TxtUsername.Text,
                    PasswordHash = hash,
                    Role = TxtUsername.Text.ToLower().Contains("admin") ? "Admin" : "User"
                });
                File.WriteAllText(usersPath, JsonConvert.SerializeObject(users));
                try
                {
                    string sourcePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "data", "users.json");
                    if (Directory.Exists(Path.GetDirectoryName(sourcePath)))
                    {
                        File.WriteAllText(sourcePath, JsonConvert.SerializeObject(users));
                    }
                }
                catch { }
                ShowNotification("Акаунт створено! Увійдіть.", "✅", "success");
                SwitchMode_Click(null, null);
            }
        }

        /// <summary>
        /// Здійснює обчислення однонаправленого криптографічного хеш-коду пароля за стандартом SHA-256.
        /// </summary>
        private string HashPassword(string p)
        {
            using (SHA256 s = SHA256.Create()) return Convert.ToBase64String(s.ComputeHash(Encoding.UTF8.GetBytes(p)));
        }

        /// <summary>
        /// Завантажує та десеріалізує список зареєстрованих користувачів із локального файлу конфігурації JSON.
        /// </summary>
        private List<UserData> LoadUsers()
        {
            if (!File.Exists(usersPath)) return new List<UserData>();
            return JsonConvert.DeserializeObject<List<UserData>>(File.ReadAllText(usersPath)) ?? new List<UserData>();
        }

        /// <summary>
        /// Завершує виконання додатку та звільняє всі виділені системні ресурси процесу.
        /// </summary>
        private void Exit_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
    }
}
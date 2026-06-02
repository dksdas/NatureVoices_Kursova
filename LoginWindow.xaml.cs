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
            {
                TitleTxt.Text = "Реєстрація";
                MainActionBtn.Content = "СТВОРИТИ АКАУНТ";
                SwitchModeBtn.Content = "Вже є акаунт? Увійти";
            }
        }

        /// <summary>
        /// Керує основним потоком авторизації або реєстрації залежно від активного стану форми.
        /// </summary>
        private void MainAction_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtUsername.Text) || string.IsNullOrWhiteSpace(TxtPassword.Password))
            {
                MessageBox.Show("Будь ласка, заповніть логін і пароль.", "Увага", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                else MessageBox.Show("Невірний логін або пароль");
            }
            else
            {
                if (users.Exists(u => u.Username == TxtUsername.Text)) { MessageBox.Show("Логін зайнятий"); return; }
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
                MessageBox.Show("Готово! Тепер увійдіть.");
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
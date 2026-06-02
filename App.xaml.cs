using System.Windows;

namespace аудіобібліотека_голоси_природи
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            new LoginWindow().Show();
        }
    }
}
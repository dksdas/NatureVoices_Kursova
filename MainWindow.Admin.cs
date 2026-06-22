using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualBasic;

namespace аудіобібліотека_голоси_природи
{
    public partial class MainWindow
    {
        /// <summary>
        /// Додає новий аудіофайл через діалог вибору файлу (тільки для адміна).
        /// </summary>
        private void AddTrack_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NewTrackTitle.Text)) return;
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Audio files (*.mp3)|*.mp3" };
            if (dlg.ShowDialog() == true)
            {
                string fn   = Path.GetFileName(dlg.FileName);
                string dest = Path.Combine(DataPath, fn);
                if (!File.Exists(dest)) File.Copy(dlg.FileName, dest);
                var t = new AudioTrack { Title = NewTrackTitle.Text, Category = NewTrackCategory.Text, FileName = fn };
                allTracks.Add(t);
                SaveData();
                UpdateTrackDuration(t);
                NewTrackTitle.Clear();
                NewTrackCategory.Clear();
            }
        }

        /// <summary>
        /// Створює нову категорію через діалог введення (тільки для адміна).
        /// </summary>
        private void AddCategory_Click(object sender, RoutedEventArgs e)
        {
            string input = Interaction.InputBox("Введіть іконку та назву:", "Нова категорія", "📁 Нова категорія");
            if (!string.IsNullOrWhiteSpace(input))
            {
                categories.Add(new CategoryItem { Name = input });
                SaveData();
            }
        }

        /// <summary>
        /// Видаляє категорію через контекстне меню (тільки для адміна).
        /// </summary>
        private void DeleteCategory_Click(object sender, RoutedEventArgs e)
        {
            var item = (sender as MenuItem)?.DataContext as CategoryItem;
            if (item != null) { categories.Remove(item); SaveData(); }
        }

        /// <summary>
        /// Видаляє трек зі списку після підтвердження (тільки для адміна).
        /// </summary>
        private void DeleteTrack_Click(object sender, RoutedEventArgs e)
        {
            var t = (sender as Button)?.DataContext as AudioTrack;
            if (t != null && MessageBox.Show($"Видалити {t.Title}?", "Видалення", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                allTracks.Remove(t);
                SaveData();
            }
        }
    }
}

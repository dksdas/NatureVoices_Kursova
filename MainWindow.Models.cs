using System.Collections.Generic;
using System.ComponentModel;

namespace аудіобібліотека_голоси_природи
{
    public partial class MainWindow
    {
        /// <summary>
        /// Модель окремого аудіотреку з підтримкою INotifyPropertyChanged.
        /// </summary>
        public class AudioTrack : INotifyPropertyChanged
        {
            public string Title { get; set; }
            public string Category { get; set; }
            public string FileName { get; set; }

            private string _dur = "--:--";
            public string Duration
            {
                get { return _dur; }
                set { _dur = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Duration")); }
            }

            private string _fav = "♡";
            public string FavoriteIcon
            {
                get { return _fav; }
                set { _fav = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("FavoriteIcon")); }
            }

            public event PropertyChangedEventHandler PropertyChanged;
        }

        /// <summary>
        /// Елемент категорії для бокового меню.
        /// </summary>
        public class CategoryItem
        {
            public string Name { get; set; }
        }

        /// <summary>
        /// Контейнер для серіалізації даних у tracks.json.
        /// </summary>
        public class AppData
        {
            public List<AudioTrack> Tracks { get; set; }
            public List<CategoryItem> Categories { get; set; }
        }
    }

    // ── Playlist models (top-level, used by PlaylistWindow) ─────────────────

    /// <summary>
    /// Плейлист користувача зі списком категорій та треків.
    /// </summary>
    public class Playlist
    {
        public string Id { get; set; } = System.Guid.NewGuid().ToString();
        public string Name { get; set; } = "Новий плейлист";
        public string Emoji { get; set; } = "🎵";
        /// <summary>Назви категорій, прикріплених до плейлиста.</summary>
        public List<string> Categories { get; set; } = new List<string>();
        /// <summary>FileName треків, що входять до плейлиста.</summary>
        public List<string> TrackFileNames { get; set; } = new List<string>();
    }
}

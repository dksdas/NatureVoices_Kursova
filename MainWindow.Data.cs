using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;

namespace аудіобібліотека_голоси_природи
{
    public partial class MainWindow
    {
        /// <summary>
        /// Завантажує треки, категорії та прапорці улюбленого з локального JSON.
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
                        t.FavoriteIcon = user.FavoriteTracks.Contains(t.Title) ? "♥" : "♡";
                }
            }

            SoundsList.ItemsSource = allTracks;
            CategoryListBox.ItemsSource = categories;
            foreach (var t in allTracks) UpdateTrackDuration(t);
        }

        /// <summary>
        /// Зберігає поточний стан треків та категорій у tracks.json.
        /// </summary>
        private void SaveData()
        {
            File.WriteAllText(JsonPath, JsonConvert.SerializeObject(
                new AppData { Tracks = allTracks.ToList(), Categories = categories.ToList() },
                Formatting.Indented));
        }

        /// <summary>
        /// Асинхронно синхронізує дані з віддаленого сервера та завантажує відсутні файли.
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
                            byte[] fileData = await httpClient.GetByteArrayAsync(
                                ServerBase + Uri.EscapeDataString(track.FileName));
                            File.WriteAllBytes(localPath, fileData);
                            UpdateTrackDuration(track);
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }
    }
}

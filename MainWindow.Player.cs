using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace аудіобібліотека_голоси_природи
{
    public partial class MainWindow
    {
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
                UpdateSmtcInfo(t);
            }
        }

        private void PlayNext()
        {
            if (allTracks.Count == 0) return;

            AudioTrack next;
            if (isShuffleOn)
            {
                int idx = allTracks.Count > 1 ? rng.Next(allTracks.Count - 1) : 0;
                if (allTracks.Count > 1 && idx >= currentTrackIndex) idx++;
                next = allTracks[idx];
            }
            else
            {
                next = currentTrackIndex >= allTracks.Count - 1
                    ? allTracks[0]
                    : allTracks[currentTrackIndex + 1];
            }
            PlayTrack(next);
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (mediaPlayer.NaturalDuration.HasTimeSpan)
            {
                TimelineSlider.Maximum = mediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
                if (!TimelineSlider.IsMouseCaptureWithin)
                    TimelineSlider.Value = mediaPlayer.Position.TotalSeconds;
                TimeStatus.Text = $"{mediaPlayer.Position:m\\:ss} / {mediaPlayer.NaturalDuration.TimeSpan:m\\:ss}";
            }
        }

        private void PlayPause_Click(object sender, RoutedEventArgs e)
        {
            if (currentTrackIndex == -1 && allTracks.Count > 0) { PlayTrack(allTracks[0]); return; }
            if (isPlaying) { mediaPlayer.Pause(); PlayPauseIcon.Text = "▶"; }
            else           { mediaPlayer.Play();  PlayPauseIcon.Text = "⏸"; }
            isPlaying = !isPlaying;
            UpdateSmtcInfo(currentTrackIndex >= 0 ? allTracks[currentTrackIndex] : null);
        }

        private void TimelineSlider_ValueChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
        {
            if (TimelineSlider.IsMouseCaptureWithin && mediaPlayer.NaturalDuration.HasTimeSpan)
                mediaPlayer.Position = TimeSpan.FromSeconds(TimelineSlider.Value);
        }

        private void TimelineSlider_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var slider = sender as Slider;
            if (slider == null || !mediaPlayer.NaturalDuration.HasTimeSpan) return;
            double percent = e.GetPosition(slider).X / slider.ActualWidth;
            double val = percent * slider.Maximum;
            slider.Value = val;
            mediaPlayer.Position = TimeSpan.FromSeconds(val);
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e) =>
            PlayTrack((sender as System.Windows.Controls.Button).DataContext as AudioTrack);

        private void Next_Click(object sender, RoutedEventArgs e) => PlayNext();

        private void Prev_Click(object sender, RoutedEventArgs e)
        {
            if (allTracks.Count == 0) return;
            PlayTrack(currentTrackIndex <= 0 ? allTracks[allTracks.Count - 1] : allTracks[currentTrackIndex - 1]);
        }

        private void Volume_Changed(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e) =>
            mediaPlayer.Volume = e.NewValue;

        private void UpdateTrackDuration(AudioTrack t)
        {
            string p = Path.Combine(DataPath, t.FileName);
            if (!File.Exists(p)) return;
            var mp = new MediaPlayer();
            durationLoaders.Add(mp);
            mp.Open(new Uri(p, UriKind.Absolute));
            mp.MediaOpened += (s, ev) =>
            {
                if (mp.NaturalDuration.HasTimeSpan)
                    t.Duration = mp.NaturalDuration.TimeSpan.ToString(@"m\:ss");
                mp.Close();
                durationLoaders.Remove(mp);
            };
            mp.MediaFailed += (s, ev) => durationLoaders.Remove(mp);
        }

        private void Random_Click(object sender, RoutedEventArgs e)
        {
            isShuffleOn = !isShuffleOn;
            var btn = sender as System.Windows.Controls.Button;
            if (btn != null)
            {
                btn.Foreground = isShuffleOn
                    ? new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1DB954"))
                    : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White);
                btn.ToolTip = isShuffleOn ? "Перемішування увімкнено" : "Перемішування вимкнено";
            }
        }

        private void SleepTimerBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (sleepTimer == null) return;
            var item = SleepTimerBox.SelectedItem as System.Windows.Controls.ComboBoxItem;
            if (item == null || item.Content.ToString() == "Вимкнено") { sleepTimer.Stop(); return; }
            string text = item.Content.ToString();
            if (text.Contains("15")) timeLeftInSeconds = 15 * 60;
            else if (text.Contains("30")) timeLeftInSeconds = 30 * 60;
            else if (text.Contains("60")) timeLeftInSeconds = 60 * 60;
            sleepTimer.Start();
        }

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
    }
}

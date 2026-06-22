using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace аудіобібліотека_голоси_природи
{
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Системне повідомлення Windows для перехоплення мультимедійних команд (навушники, клавіатура).
        /// </summary>
        private const int WM_APPCOMMAND = 0x0319;

        // Коди мультимедійних команд Windows (молодші 12 біт після зсуву)
        private const int APPCOMMAND_MEDIA_NEXTTRACK = 11;
        private const int APPCOMMAND_MEDIA_PREVIOUSTRACK = 12;
        private const int APPCOMMAND_MEDIA_STOP = 13;
        private const int APPCOMMAND_MEDIA_PLAY_PAUSE = 14;

        /// <summary>
        /// Ініціалізує інтеграцію з системними медіа-клавішами (SMTC) через хук вікна Windows.
        /// </summary>
        private void InitSmtc()
        {
            // Отримуємо дескриптор (Handle) поточного вікна WPF
            var handle = new WindowInteropHelper(this).Handle;

            if (handle == IntPtr.Zero)
            {
                // Якщо метод викликано занадто рано (до створення вікна), чекаємо на подію завантаження джерела
                this.SourceInitialized += (s, e) => { InitSmtc(); };
                return;
            }

            // Підключаємо хук для обробки системних повідомлень Win32 API
            var source = HwndSource.FromHwnd(handle);
            source?.AddHook(MediaKeyHook);
        }

        /// <summary>
        /// Обробник системних повідомлень вікна (WndProc). Перехоплює натискання кнопок на навушниках або клавіатурі.
        /// </summary>
        /// <param name="hwnd">Дескриптор вікна.</param>
        /// <param name="msg">Ідентифікатор системного повідомлення.</param>
        /// <param name="wParam">Додаткові параметри повідомлення.</param>
        /// <param name="lParam">Дані повідомлення, які містять код мультимедійної команди та прапори пристрою.</param>
        /// <param name="handled">Встановлюється в true, щоб повідомити Windows, що команда успішно оброблена.</param>
        /// <returns>Завжди повертає IntPtr.Zero для стандартних повідомлень.</returns>
        private IntPtr MediaKeyHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_APPCOMMAND)
            {
                // Отримуємо команду зі старшого слова (біти 16-31) 
                // та очищуємо її маскою & 0xFFF від додаткових прапорів пристрою (миша, bluetooth тощо)
                int cmd = ((int)((lParam.ToInt64() >> 16) & 0xFFFF)) & 0xFFF;

                switch (cmd)
                {
                    case APPCOMMAND_MEDIA_PLAY_PAUSE:
                        PlayPause_Click(null, null);
                        handled = true;
                        break;

                    case APPCOMMAND_MEDIA_NEXTTRACK:
                        PlayNext();
                        handled = true;
                        break;

                    case APPCOMMAND_MEDIA_PREVIOUSTRACK:
                        Prev_Click(null, null);
                        handled = true;
                        break;

                    case APPCOMMAND_MEDIA_STOP:
                        if (isPlaying)
                        {
                            mediaPlayer.Pause();
                            PlayPauseIcon.Text = "▶";
                            isPlaying = false;
                        }
                        handled = true;
                        break;
                }
            }
            return IntPtr.Zero;
        }

        /// <summary>
        /// Оновлює інформацію про поточний аудіотрек у системному оверлеї Windows.
        /// </summary>
        /// <param name="track">Об'єкт аудіотреку з метаданими.</param>
        internal void UpdateSmtcInfo(AudioTrack track)
        {
            // Чистий WM_APPCOMMAND вміє лише ловити натискання кнопок у фоні.
            // Виводити обкладинку та назву звуку в чорне віконце Windows без WinRT SDK неможливо,
            // тому метод залишено порожнім для зворотної сумісності вашого проекту.
        }
    }
}

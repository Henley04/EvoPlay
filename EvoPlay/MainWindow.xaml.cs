using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Timers;
using Windows.Storage.Pickers;
using Windows.Storage;
using TagLib;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace EvoPlay
{
    public sealed partial class MainWindow : Window
    {
        private DispatcherTimer _timer;
        private string[] _lyricsLines;
        private double[] _lyricsTimes;
        private string _audioFilePath;

        public MainWindow()
        {
            this.InitializeComponent();

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(200);
            _timer.Tick += Timer_Tick;

            // 订阅播放状态变化事件
            Player.MediaPlayer.PlaybackSession.PlaybackStateChanged += PlaybackSession_PlaybackStateChanged;
            Player.MediaPlayer.MediaEnded += PlayEnded;
        }
        private void PlayEnded(MediaPlayer sender, object args)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                PlayPauseButton.Content = "播放";
            });
        }

        // 播放状态变化事件处理
        private void PlaybackSession_PlaybackStateChanged(Windows.Media.Playback.MediaPlaybackSession sender, object args)
        {
            //暂时弃用。
            // 播放结束时，状态会变为 None 或 Stopped
            if (sender.PlaybackState == Windows.Media.Playback.MediaPlaybackState.None)
            {
                // UI 线程更新
                // 替换原有的 await DispatcherQueue.EnqueueAsync(() => { ... });
                // DispatcherQueue 没有 EnqueueAsync 方法，需用 DispatcherQueue.TryEnqueue

                DispatcherQueue.TryEnqueue(() =>
                {
                    PlayPauseButton.Content = "播放";
                    _timer.Stop();
                });
                if (sender.PlaybackState == Windows.Media.Playback.MediaPlaybackState.Playing)
                {
                    // UI 线程更新
                    // 替换原有的 await DispatcherQueue.EnqueueAsync(() => { ... });
                    // DispatcherQueue 没有 EnqueueAsync 方法，需用 DispatcherQueue.TryEnqueue

                    DispatcherQueue.TryEnqueue(() =>
                    {
                        PlayPauseButton.Content = "暂停";
                        _timer.Stop();
                    });
                }
                if (sender.PlaybackState == Windows.Media.Playback.MediaPlaybackState.Paused)
                {
                    // UI 线程更新
                    // 替换原有的 await DispatcherQueue.EnqueueAsync(() => { ... });
                    // DispatcherQueue 没有 EnqueueAsync 方法，需用 DispatcherQueue.TryEnqueue

                    DispatcherQueue.TryEnqueue(() =>
                    {
                        PlayPauseButton.Content = "播放";
                        _timer.Stop();
                    });
                }
            }
        }



        private async void SelectMusic_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            picker.FileTypeFilter.Add(".mp3");
            picker.FileTypeFilter.Add(".flac");
            picker.FileTypeFilter.Add(".wav");
            picker.FileTypeFilter.Add(".m4a");

            StorageFile file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                _audioFilePath = file.Path;
                SongNameText.Text = file.Name;
                LoadLyrics(_audioFilePath);
                Player.Source = MediaSource.CreateFromUri(new Uri(file.Path));
                ProgressSlider.Value = 0;
                CurrentTimeText.Text = "00:00";

                // 自动播放
                Play_Click(null, null);
                PlayPauseButton.Content = "暂停";
            }
        }
        private void PlayPause_Click(object sender, RoutedEventArgs e)
        {
            if (Player.MediaPlayer.PlaybackSession.PlaybackState == Windows.Media.Playback.MediaPlaybackState.Playing)
            {
                Player.MediaPlayer.Pause();
                _timer.Stop();
                PlayPauseButton.Content = "播放";
            }
            else
            {
                if (!string.IsNullOrEmpty(_audioFilePath))
                {
                    Player.MediaPlayer.Play();
                    _timer.Start();
                    PlayPauseButton.Content = "暂停";
                }
            }
        }

        private void Play_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_audioFilePath))
            {
                Player.MediaPlayer.Play();
                _timer.Start();
            }
        }

        private void Pause_Click(object sender, RoutedEventArgs e)
        {
            Player.MediaPlayer.Pause();
            _timer.Stop();
        }

        private void Timer_Tick(object sender, object e)
        {
            var pos = Player.MediaPlayer.Position;
            ProgressSlider.Value = pos.TotalSeconds;
            CurrentTimeText.Text = pos.ToString(@"mm\:ss");
            UpdateLyrics(pos.TotalSeconds);

            var totalSeconds = Player.MediaPlayer.NaturalDuration.TotalSeconds;
            if (totalSeconds > 0)
            {
                ProgressSlider.Maximum = totalSeconds;
            }
        }

        private void ProgressSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (Math.Abs(Player.MediaPlayer.Position.TotalSeconds - ProgressSlider.Value) > 1)
            {
                Player.MediaPlayer.Position = TimeSpan.FromSeconds(ProgressSlider.Value);
            }
        }

        private void LoadLyrics(string filePath)
        {
            try
            {
                var file = TagLib.File.Create(filePath);
                var lyrics = file.Tag.Lyrics;

                // 优先内嵌歌词
                if (!string.IsNullOrWhiteSpace(lyrics))
                {
                    if (IsLrcFormat(lyrics))
                    {
                        ParseLyrics(lyrics);
                    }
                    else
                    {
                        // 非LRC格式直接显示
                        LyricsText.Text = lyrics;
                        _lyricsLines = null;
                        _lyricsTimes = null;
                    }
                    return;
                }

                // 查找同名LRC文件
                var lrcPath = Path.ChangeExtension(filePath, ".lrc");

                // Update the ambiguous reference to explicitly use System.IO.File
                if (System.IO.File.Exists(lrcPath))
                {
                    string lrcContent = null;
                    try
                    {
                        // 优先尝试UTF-8
                        lrcContent = System.IO.File.ReadAllText(lrcPath, System.Text.Encoding.UTF8);
                    }
                    catch
                    {
                        // 若UTF-8失败，尝试GBK
                        lrcContent = System.IO.File.ReadAllText(lrcPath, System.Text.Encoding.GetEncoding("GBK"));
                    }

                    if (!string.IsNullOrWhiteSpace(lrcContent))
                    {
                        ParseLyrics(lrcContent);
                        return;
                    }
                }


                // 都没有歌词
                LyricsText.Text = "未找到歌词";
                _lyricsLines = null;
                _lyricsTimes = null;
            }
            catch (Exception ex)
            {
                LyricsText.Text = "歌词加载失败";
                System.Diagnostics.Debug.WriteLine("歌词加载异常: " + ex.Message);
            }
        }

        // 判断是否为LRC格式
        private bool IsLrcFormat(string lyrics)
        {
            // 简单判断：是否包含 [mm:ss] 这样的时间标签
            return lyrics.Contains("[") && lyrics.Contains("]");
        }

        // 解析LRC格式歌词
        private void ParseLyrics(string lyrics)
        {
            var lines = lyrics.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var timeList = new List<double>();
            var textList = new List<string>();

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim(); // 去除前后空白
                int idx = 0;
                bool foundTag = false;
                string lyricText = null;

                while (idx < line.Length && line[idx] == '[')
                {
                    int endIdx = line.IndexOf(']', idx);
                    if (endIdx > idx)
                    {
                        var timeStr = line.Substring(idx + 1, endIdx - idx - 1);
                        TimeSpan ts;
                        // 支持 [mm:ss], [mm:ss.ff], [hh:mm:ss], [mm:ss:fff]
                        if (TimeSpan.TryParseExact(timeStr, @"mm\:ss\.ff", null, out ts) ||
                            TimeSpan.TryParseExact(timeStr, @"mm\:ss\.fff", null, out ts) ||
                            TimeSpan.TryParseExact(timeStr, @"mm\:ss", null, out ts) ||
                            TimeSpan.TryParseExact(timeStr, @"hh\:mm\:ss", null, out ts))
                        {
                            foundTag = true;
                            lyricText = line.Substring(endIdx + 1).Trim();
                            timeList.Add(ts.TotalSeconds);
                            textList.Add(lyricText);
                        }
                        idx = endIdx + 1;
                    }
                    else
                    {
                        break;
                    }
                }
                // 兼容没有时间标签的行
                if (!foundTag && !string.IsNullOrWhiteSpace(line))
                {
                    timeList.Add(0);
                    textList.Add(line);
                }
            }
            _lyricsTimes = timeList.ToArray();
            _lyricsLines = textList.ToArray();

            System.Diagnostics.Debug.WriteLine($"歌词解析完成，行数：{_lyricsLines.Length}");
        }

        private void UpdateLyrics(double currentSeconds)
        {
            if (_lyricsTimes == null || _lyricsLines == null) return;
            for (int i = _lyricsTimes.Length - 1; i >= 0; i--)
            {
                if (currentSeconds >= _lyricsTimes[i])
                {
                    LyricsText.Text = _lyricsLines[i];
                    return;
                }
            }
            LyricsText.Text = "";
        }
    }
}

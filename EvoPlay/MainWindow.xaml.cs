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

            // ���Ĳ���״̬�仯�¼�
            Player.MediaPlayer.PlaybackSession.PlaybackStateChanged += PlaybackSession_PlaybackStateChanged;
            Player.MediaPlayer.MediaEnded += PlayEnded;
        }
        private void PlayEnded(MediaPlayer sender, object args)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                PlayPauseButton.Content = "����";
            });
        }

        // ����״̬�仯�¼�����
        private void PlaybackSession_PlaybackStateChanged(Windows.Media.Playback.MediaPlaybackSession sender, object args)
        {
            //��ʱ���á�
            // ���Ž���ʱ��״̬���Ϊ None �� Stopped
            if (sender.PlaybackState == Windows.Media.Playback.MediaPlaybackState.None)
            {
                // UI �̸߳���
                // �滻ԭ�е� await DispatcherQueue.EnqueueAsync(() => { ... });
                // DispatcherQueue û�� EnqueueAsync ���������� DispatcherQueue.TryEnqueue

                DispatcherQueue.TryEnqueue(() =>
                {
                    PlayPauseButton.Content = "����";
                    _timer.Stop();
                });
                if (sender.PlaybackState == Windows.Media.Playback.MediaPlaybackState.Playing)
                {
                    // UI �̸߳���
                    // �滻ԭ�е� await DispatcherQueue.EnqueueAsync(() => { ... });
                    // DispatcherQueue û�� EnqueueAsync ���������� DispatcherQueue.TryEnqueue

                    DispatcherQueue.TryEnqueue(() =>
                    {
                        PlayPauseButton.Content = "��ͣ";
                        _timer.Stop();
                    });
                }
                if (sender.PlaybackState == Windows.Media.Playback.MediaPlaybackState.Paused)
                {
                    // UI �̸߳���
                    // �滻ԭ�е� await DispatcherQueue.EnqueueAsync(() => { ... });
                    // DispatcherQueue û�� EnqueueAsync ���������� DispatcherQueue.TryEnqueue

                    DispatcherQueue.TryEnqueue(() =>
                    {
                        PlayPauseButton.Content = "����";
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

                // �Զ�����
                Play_Click(null, null);
                PlayPauseButton.Content = "��ͣ";
            }
        }
        private void PlayPause_Click(object sender, RoutedEventArgs e)
        {
            if (Player.MediaPlayer.PlaybackSession.PlaybackState == Windows.Media.Playback.MediaPlaybackState.Playing)
            {
                Player.MediaPlayer.Pause();
                _timer.Stop();
                PlayPauseButton.Content = "����";
            }
            else
            {
                if (!string.IsNullOrEmpty(_audioFilePath))
                {
                    Player.MediaPlayer.Play();
                    _timer.Start();
                    PlayPauseButton.Content = "��ͣ";
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

                // ������Ƕ���
                if (!string.IsNullOrWhiteSpace(lyrics))
                {
                    if (IsLrcFormat(lyrics))
                    {
                        ParseLyrics(lyrics);
                    }
                    else
                    {
                        // ��LRC��ʽֱ����ʾ
                        LyricsText.Text = lyrics;
                        _lyricsLines = null;
                        _lyricsTimes = null;
                    }
                    return;
                }

                // ����ͬ��LRC�ļ�
                var lrcPath = Path.ChangeExtension(filePath, ".lrc");

                // Update the ambiguous reference to explicitly use System.IO.File
                if (System.IO.File.Exists(lrcPath))
                {
                    string lrcContent = null;
                    try
                    {
                        // ���ȳ���UTF-8
                        lrcContent = System.IO.File.ReadAllText(lrcPath, System.Text.Encoding.UTF8);
                    }
                    catch
                    {
                        // ��UTF-8ʧ�ܣ�����GBK
                        lrcContent = System.IO.File.ReadAllText(lrcPath, System.Text.Encoding.GetEncoding("GBK"));
                    }

                    if (!string.IsNullOrWhiteSpace(lrcContent))
                    {
                        ParseLyrics(lrcContent);
                        return;
                    }
                }


                // ��û�и��
                LyricsText.Text = "δ�ҵ����";
                _lyricsLines = null;
                _lyricsTimes = null;
            }
            catch (Exception ex)
            {
                LyricsText.Text = "��ʼ���ʧ��";
                System.Diagnostics.Debug.WriteLine("��ʼ����쳣: " + ex.Message);
            }
        }

        // �ж��Ƿ�ΪLRC��ʽ
        private bool IsLrcFormat(string lyrics)
        {
            // ���жϣ��Ƿ���� [mm:ss] ������ʱ���ǩ
            return lyrics.Contains("[") && lyrics.Contains("]");
        }

        // ����LRC��ʽ���
        private void ParseLyrics(string lyrics)
        {
            var lines = lyrics.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var timeList = new List<double>();
            var textList = new List<string>();

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim(); // ȥ��ǰ��հ�
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
                        // ֧�� [mm:ss], [mm:ss.ff], [hh:mm:ss], [mm:ss:fff]
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
                // ����û��ʱ���ǩ����
                if (!foundTag && !string.IsNullOrWhiteSpace(line))
                {
                    timeList.Add(0);
                    textList.Add(line);
                }
            }
            _lyricsTimes = timeList.ToArray();
            _lyricsLines = textList.ToArray();

            System.Diagnostics.Debug.WriteLine($"��ʽ�����ɣ�������{_lyricsLines.Length}");
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

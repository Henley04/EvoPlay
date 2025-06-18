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
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;


namespace EvoPlay
{
    public sealed partial class MainWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private DispatcherTimer _timer;
        private string[] _lyricsLines;
        private double[] _lyricsTimes;
        private string _audioFilePath;
        private double _lyricsFontSize = 28;
        
        // 音乐库数据
        public ObservableCollection<MusicInfo> MusicLibrary { get; set; } = new ObservableCollection<MusicInfo>();
        private int _currentLibraryIndex = -1;

        // 音乐库路径
        private string _musicLibraryPath = "";

        public double LyricsFontSize
        {
            get => _lyricsFontSize;
            set
            {
                if (_lyricsFontSize != value)
                {
                    _lyricsFontSize = value;
                    OnPropertyChanged();
                }
            }
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public MainWindow()
        {
            this.InitializeComponent();

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(200);
            _timer.Tick += Timer_Tick;

            
            Player.MediaPlayer.PlaybackSession.PlaybackStateChanged += PlaybackSession_PlaybackStateChanged;
            Player.MediaPlayer.MediaEnded += PlayEnded;

            // 默认加载音乐库
            LoadMusicLibrary();
        }

        // 音乐信息类
        public class MusicInfo
        {
            public string FilePath { get; set; }
            public string Name { get; set; }
            public string Artist { get; set; }
            public string Album { get; set; }
        }

        // 设置按钮事件
        private async void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new SettingsDialog(_musicLibraryPath, LyricsFontSize);
                dlg.XamlRoot = this.Content.XamlRoot; // 将对话框的 XamlRoot 绑定到主窗口的 XamlRoot
                var result = await dlg.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    _musicLibraryPath = dlg.SelectedMusicLibraryPath;
                    LyricsFontSize = dlg.SelectedLyricsFontSize;
                    LyricsText.FontSize = LyricsFontSize;
                    LoadMusicLibrary();
                }
            }
            catch (Exception ex)
            {
                ContentDialog errorDialog = new ContentDialog
                {
                    Title = "错误",
                    Content = ex.Message,
                    CloseButtonText = "确定"
                };
                await errorDialog.ShowAsync();
            }
        }


        // 加载音乐库
        private void LoadMusicLibrary()
        {
            MusicLibrary.Clear();
            string path = string.IsNullOrEmpty(_musicLibraryPath)
                ? Environment.GetFolderPath(Environment.SpecialFolder.MyMusic)
                : _musicLibraryPath;
            if (!Directory.Exists(path)) return;
            var files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)
                         || f.EndsWith(".flac", StringComparison.OrdinalIgnoreCase)
                         || f.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)
                         || f.EndsWith(".m4a", StringComparison.OrdinalIgnoreCase));
            foreach (var file in files)
            {
                try
                {
                    var tagFile = TagLib.File.Create(file);
                    MusicLibrary.Add(new MusicInfo
                    {
                        FilePath = file,
                        Name = tagFile.Tag.Title ?? Path.GetFileName(file),
                        Artist = tagFile.Tag.FirstPerformer ?? "未知艺术家",
                        Album = tagFile.Tag.Album ?? "未知专辑"
                    });
                }
                catch
                {
                    MusicLibrary.Add(new MusicInfo
                    {
                        FilePath = file,
                        Name = Path.GetFileName(file),
                        Artist = "错误",
                        Album = "错误"
                    });
                }
            }
        }


        // 音乐库栏目播放按钮事件
        private void MusicItemPlay_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is MusicInfo info)
            {
                int idx = MusicLibrary.IndexOf(info);
                if (idx >= 0)
                {
                    PlayMusicFromLibrary(idx);
                }
            }
        }

        // 从指定索引开始顺序播放音乐库
        private void PlayMusicFromLibrary(int startIndex)
        {
            if (startIndex < 0 || startIndex >= MusicLibrary.Count) return;
            _currentLibraryIndex = startIndex;
            PlayLibraryCurrent();
        }

        // 播放当前索引音乐
        private void PlayLibraryCurrent()
        {
            if (_currentLibraryIndex < 0 || _currentLibraryIndex >= MusicLibrary.Count) return;
            var info = MusicLibrary[_currentLibraryIndex];
            _audioFilePath = info.FilePath;
            SongNameText.Text = info.Name;
            LoadLyrics(_audioFilePath);
            Player.Source = MediaSource.CreateFromUri(new Uri(_audioFilePath));
            ProgressSlider.Value = 0;
            CurrentTimeText.Text = "00:00";
            Player.MediaPlayer.Play();
            _timer.Start();
            PlayPauseButton.Content = "暂停";
        }

        // 播放结束自动播放下一个
        private void PlayEnded(MediaPlayer sender, object args)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (_currentLibraryIndex >= 0 && _currentLibraryIndex < MusicLibrary.Count - 1)
                {
                    _currentLibraryIndex++;
                    PlayLibraryCurrent();
                }
                else
                {
                    PlayPauseButton.Content = "播放";
                }
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
                    PlayPauseButton.Content = "播放";
                    _timer.Stop();
                });
                if (sender.PlaybackState == Windows.Media.Playback.MediaPlaybackState.Playing)
                {
                    // UI �̸߳���
                    // �滻ԭ�е� await DispatcherQueue.EnqueueAsync(() => { ... });
                    // DispatcherQueue û�� EnqueueAsync ���������� DispatcherQueue.TryEnqueue

                    DispatcherQueue.TryEnqueue(() =>
                    {
                        PlayPauseButton.Content = "暂停";
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

                // �Զ�����
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
                LyricsText.Text = "未找到歌词文件或内嵌歌词";
                _lyricsLines = null;
                _lyricsTimes = null;
            }
            catch (Exception ex)
            {
                LyricsText.Text = "加载错误";
                System.Diagnostics.Debug.WriteLine("Error when loading lrc: " + ex.Message);
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

        // 歌词字号同步
        private void UpdateLyrics(double currentSeconds)
        {
            if (_lyricsTimes == null || _lyricsLines == null) return;
            for (int i = _lyricsTimes.Length - 1; i >= 0; i--)
            {
                if (currentSeconds >= _lyricsTimes[i])
                {
                    LyricsText.Text = _lyricsLines[i];
                    LyricsText.FontSize = LyricsFontSize;
                    return;
                }
            }
            LyricsText.Text = "";
        }
    }
}

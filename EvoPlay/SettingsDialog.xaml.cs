using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using Windows.Storage;
using System;
using WinRT.Interop; // 确保引入这个命名空间
using Microsoft.UI; // 用于 WindowNative

namespace EvoPlay
{
    public sealed partial class SettingsDialog : ContentDialog
    {
        public string SelectedMusicLibraryPath { get; set; }
        public double SelectedLyricsFontSize { get; set; }

        public SettingsDialog(string musicLibraryPath, double lyricsFontSize)
        {
            this.InitializeComponent();
            SelectedMusicLibraryPath = musicLibraryPath;
            SelectedLyricsFontSize = lyricsFontSize;
        }

        private async void BrowseMusicLibrary_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FolderPicker();

            // 正确地将 FolderPicker 绑定到当前窗口  
            var window = App.MainWindow; // 假设 App.MainWindow 是你的主窗口实例  
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);

            InitializeWithWindow.Initialize(picker, hwnd);

            picker.FileTypeFilter.Add("*");
            StorageFolder folder = await picker.PickSingleFolderAsync();
            if (folder != null)
            {
                SelectedMusicLibraryPath = folder.Path;
                MusicLibraryPathBox.Text = folder.Path;
            }
        }

    }
}
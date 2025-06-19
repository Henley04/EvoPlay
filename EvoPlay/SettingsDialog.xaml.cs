using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using Windows.Storage;
using System;
using WinRT.Interop; // ȷ��������������ռ�
using Microsoft.UI; // ���� WindowNative

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

            // ��ȷ�ؽ� FolderPicker �󶨵���ǰ����  
            var window = App.MainWindow; // ���� App.MainWindow �����������ʵ��  
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
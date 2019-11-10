using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using LibVLCSharp.Shared;

namespace LibVLCSharp.Avalonia.Sample
{
    public class Example1 : Window
    {
        private VideoView VideoView;
        private LibVLC _libVLC;
        private MediaPlayer _mediaPlayer;

        public Example1()
        {
            this.InitializeComponent();
            //Renderer.DrawFps = true;

            VideoView = this.Get<VideoView>("VideoView");

            _libVLC = new LibVLC();
            _mediaPlayer = new MediaPlayer(_libVLC);

            VideoView.MediaPlayer = _mediaPlayer;
#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            if (VideoView.MediaPlayer.IsPlaying)
            {
                VideoView.MediaPlayer.Stop();
            }
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (!VideoView.MediaPlayer.IsPlaying)
            {
                VideoView.MediaPlayer.Play(new Media(_libVLC,
                    "http://commondatastorage.googleapis.com/gtv-videos-bucket/sample/BigBuckBunny.mp4", FromType.FromLocation));
            }
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            VideoView.MediaPlayer.Pause();
        }
    }
}
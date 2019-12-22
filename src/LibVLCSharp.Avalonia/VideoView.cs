using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using LibVLCSharp.Shared;
using System;
using System.Reactive.Linq;

namespace LibVLCSharp.Avalonia
{
    public class VideoView : ContentControl, IVideoView
    {
        static VideoView()
        {
            MediaPlayerProperty.Changed.AddClassHandler<VideoView>((v, e) => v.InitMediaPlayer());
        }

        private VlcVideoSourceProvider _provider = new VlcVideoSourceProvider();
        private Image PART_Image;

        public static readonly DirectProperty<VideoView, MediaPlayer> MediaPlayerProperty =
            AvaloniaProperty.RegisterDirect<VideoView, MediaPlayer>(nameof(MediaPlayer), v => v.MediaPlayer, (s, v) => s.MediaPlayer = v);

        private MediaPlayer _mediaPlayer;

        public MediaPlayer MediaPlayer
        {
            get => _mediaPlayer;
            set => SetAndRaise(MediaPlayerProperty, ref _mediaPlayer, value);
        }

        public static readonly DirectProperty<VideoView, bool> DisplayRenderStatsProperty =
             AvaloniaProperty.RegisterDirect<VideoView, bool>(nameof(DisplayRenderStats), v => v.DisplayRenderStats, (s, v) => s.DisplayRenderStats = v);

        private bool _displayRenderStats;

        public bool DisplayRenderStats
        {
            get => _displayRenderStats;
            set => SetAndRaise(DisplayRenderStatsProperty, ref _displayRenderStats, value);
        }

        protected override void OnTemplateApplied(TemplateAppliedEventArgs e)
        {
            base.OnTemplateApplied(e);

            PART_Image = e.NameScope.Get<Image>("PART_RenderImage");

            if (PART_Image is VLCImageRenderer vb)
            {
                vb.SourceProvider = _provider;
            }
            else
            {
                PART_Image.Bind(Image.SourceProperty, _provider.Display);
            }
        }

        private IDisposable _playerEvents;

        private void InitMediaPlayer()
        {
            if (!Design.IsDesignMode)
            {
                _playerEvents?.Dispose();
                _playerEvents = null;

                _provider.Init(MediaPlayer);
                _playerEvents = Observable.FromEventPattern(MediaPlayer, nameof(MediaPlayer.Playing))
                    .ObserveOn(AvaloniaScheduler.Instance)
                    .Subscribe(_ =>
                    {
                        if (PART_Image is VLCImageRenderer vb)
                        {
                            vb.ResetStats();
                        }
                    });
            }
        }
    }
}
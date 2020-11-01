using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using LibVLCSharp.Shared;
using System;
using System.Reactive.Linq;
using System.Runtime.InteropServices;

namespace LibVLCSharp.Avalonia
{
    public class VideoView : ContentControl, IVideoView
    {
        static VideoView()
        {
            MediaPlayerProperty.Changed.AddClassHandler<VideoView>((v, e) => v.InitMediaPlayer());
        }

        public VideoView()
        {
            VlcRenderingOptions = LibVLCAvaloniaOptions.RenderingOptions;
        }

        private VlcVideoSourceProvider _provider = new VlcVideoSourceProvider();
        private Image PART_Image;
        private NativeVideoPresenter PART_NativeHost;
        private bool _templateApplied;

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

        public static readonly StyledProperty<LibVLCAvaloniaRenderingOptions> VlcRenderingOptionsProperty =
                AvaloniaProperty.Register<VideoView, LibVLCAvaloniaRenderingOptions>(nameof(VlcRenderingOptions));

        public LibVLCAvaloniaRenderingOptions VlcRenderingOptions
        {
            get => GetValue(VlcRenderingOptionsProperty);
            set => SetValue(VlcRenderingOptionsProperty, value);
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            PART_Image = e.NameScope.Find<Image>("PART_RenderImage");
            PART_NativeHost = e.NameScope.Find<NativeVideoPresenter>("PART_NativeHost");

            _templateApplied = true;

            if (VlcRenderingOptions != LibVLCAvaloniaRenderingOptions.VlcNative)
            {
                if (PART_Image is VLCImageRenderer vb)
                {
                    vb.SourceProvider = _provider;
                    vb.UseCustomDrawingOperation = VlcRenderingOptions == LibVLCAvaloniaRenderingOptions.AvaloniaCustomDrawingOperation;
                }
                else
                {
                    PART_Image.Bind(Image.SourceProperty, _provider.Display);
                }
            }

            InitMediaPlayer();
        }

        private IDisposable _playerEvents;

        private void InitMediaPlayer()
        {
            if (!Design.IsDesignMode && _templateApplied)
            {
                _playerEvents?.Dispose();
                _playerEvents = null;

                if (VlcRenderingOptions != LibVLCAvaloniaRenderingOptions.VlcNative)
                {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                        MediaPlayer.Hwnd = IntPtr.Zero;
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                        MediaPlayer.NsObject = IntPtr.Zero;
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                        MediaPlayer.XWindow = 0;

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
                else
                {
                    PART_NativeHost?.UpdatePlayerHandle(MediaPlayer);
                }
            }
        }
    }
}
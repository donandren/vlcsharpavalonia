using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using LibVLCSharp.Shared;

namespace LibVLCSharp.Avalonia
{
    public class VideoView : ContentControl, IVideoView
    {
        static VideoView()
        {
            MediaPlayerProperty.Changed.AddClassHandler<VideoView>((v, e) => v.Init());
        }

        public static readonly AvaloniaProperty<MediaPlayer> MediaPlayerProperty =
            AvaloniaProperty.RegisterDirect<VideoView, MediaPlayer>(nameof(MediaPlayer), v => v.MediaPlayer, (s, v) => s.MediaPlayer = v);

        private MediaPlayer _mediaPlayer;
        private VlcVideoSourceProvider _provider = new VlcVideoSourceProvider();
        private Image PART_Image;

        public MediaPlayer MediaPlayer
        {
            get => _mediaPlayer;
            set => SetAndRaise(MediaPlayerProperty, ref _mediaPlayer, value);
        }

        protected override void OnTemplateApplied(TemplateAppliedEventArgs e)
        {
            base.OnTemplateApplied(e);

            PART_Image = e.NameScope.Get<Image>("PART_RenderImage");
            PART_Image.Bind(Image.SourceProperty, new Binding(nameof(_provider.VideoSource)) { Source = _provider });
        }

        private void Init()
        {
            if (!Design.IsDesignMode)
            {
                _provider.Init(MediaPlayer, () => PART_Image?.InvalidateVisual());
            }
        }
    }
}

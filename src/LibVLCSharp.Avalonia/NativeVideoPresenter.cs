using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Platform;
using Avalonia.VisualTree;
using LibVLCSharp.Shared;
using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.InteropServices;

namespace LibVLCSharp.Avalonia
{
    public class NativeVideoPresenter : NativeControlHost
    {
        static NativeVideoPresenter()
        {
            ContentProperty.Changed.AddClassHandler<NativeVideoPresenter>((s, e) => s.InitializeNativeOverlay());
            IsVisibleProperty.Changed.AddClassHandler<NativeVideoPresenter>((s, e) => s.ShowNativeOverlay(s.IsVisible));
        }

        public static readonly StyledProperty<object> ContentProperty =
            ContentControl.ContentProperty.AddOwner<NativeVideoPresenter>();

        [Content]
        public object Content
        {
            get => GetValue(ContentProperty);
            set => SetValue(ContentProperty, value);
        }

        private IPlatformHandle _handle;
        private bool _attached;
        private Window _floatingContent;
        private IDisposable _disposables;

        protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
        {
            return _handle = base.CreateNativeControlCore(parent);
        }

        internal void UpdatePlayerHandle(MediaPlayer mediaPlayer)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                mediaPlayer.Hwnd = _handle.Handle;
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                mediaPlayer.NsObject = _handle.Handle;
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                mediaPlayer.XWindow = (uint)_handle.Handle;
            else
                throw new NotImplementedException();
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            _attached = true;

            InitializeNativeOverlay();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);

            ShowNativeOverlay(false);

            _attached = false;
        }

        protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromLogicalTree(e);

            _disposables?.Dispose();
            _disposables = null;
            _floatingContent?.Close();
            _floatingContent = null;
        }

        private void InitializeNativeOverlay()
        {
            if (!((IVisual)this).IsAttachedToVisualTree) return;

            if (_floatingContent == null && Content != null)
            {
                _floatingContent = new Window()
                {
                    SystemDecorations = SystemDecorations.None,
                    TransparencyLevelHint = WindowTransparencyLevel.Transparent,
                    Background = Brushes.Transparent,
                    SizeToContent = SizeToContent.WidthAndHeight,
                };

                _disposables = new CompositeDisposable()
                {
                    _floatingContent.Bind(Window.ContentProperty, this.GetObservable(ContentProperty)),
                    this.GetObservable(ContentProperty).Skip(1).Subscribe(_=> UpdateOverlayPosition()),
                    this.GetObservable(BoundsProperty).Skip(1).Subscribe(_ => UpdateOverlayPosition()),
                    Observable.FromEventPattern(VisualRoot, nameof(Window.PositionChanged))
                    .Subscribe(_ => UpdateOverlayPosition()),
                };
            }

            ShowNativeOverlay(IsVisible);
        }

        private void ShowNativeOverlay(bool show)
        {
            if (show && _attached)
                _floatingContent?.Show(VisualRoot as Window);
            else
                _floatingContent?.Hide();
        }

        private void UpdateOverlayPosition()
        {
            if (_floatingContent == null) return;

            bool forceSetWidth = false, forceSetHeight = false;

            var topLeft = new Point();

            var child = _floatingContent.Presenter?.Child;

            if (child?.IsArrangeValid == true)
            {
                switch (child.HorizontalAlignment)
                {
                    case HorizontalAlignment.Right:
                        topLeft = topLeft.WithX(Bounds.Width - _floatingContent.Bounds.Width);
                        break;

                    case HorizontalAlignment.Center:
                        topLeft = topLeft.WithX((Bounds.Width - _floatingContent.Bounds.Width) / 2);
                        break;

                    case HorizontalAlignment.Stretch:
                        forceSetWidth = true;
                        break;
                }

                switch (child.VerticalAlignment)
                {
                    case VerticalAlignment.Bottom:
                        topLeft = topLeft.WithY(Bounds.Height - _floatingContent.Bounds.Height);
                        break;

                    case VerticalAlignment.Center:
                        topLeft = topLeft.WithY((Bounds.Height - _floatingContent.Bounds.Height) / 2);
                        break;

                    case VerticalAlignment.Stretch:
                        forceSetHeight = true;
                        break;
                }
            }

            if (forceSetWidth && forceSetHeight)
                _floatingContent.SizeToContent = SizeToContent.Manual;
            else if (forceSetHeight)
                _floatingContent.SizeToContent = SizeToContent.Width;
            else if (forceSetWidth)
                _floatingContent.SizeToContent = SizeToContent.Height;
            else
                _floatingContent.SizeToContent = SizeToContent.Manual;

            _floatingContent.Width = forceSetWidth ? Bounds.Width : double.NaN;
            _floatingContent.Height = forceSetHeight ? Bounds.Height : double.NaN;

            _floatingContent.MaxWidth = Bounds.Width;
            _floatingContent.MaxHeight = Bounds.Height;

            var newPosition = this.PointToScreen(topLeft);

            if (newPosition != _floatingContent.Position)
            {
                _floatingContent.Position = newPosition;
            }
        }
    }
}
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Utilities;
using Avalonia.Visuals.Media.Imaging;

namespace LibVLCSharp.Avalonia
{
    public class VLCImageRenderer : Image
    {
        private struct CustomOp : ICustomDrawOperation
        {
            private IRef<IBitmapImpl> _source;
            private Rect _sourceRect;
            private Rect _destRect;
            private BitmapInterpolationMode _interpolationMode;

            public Rect Bounds => _destRect;

            public CustomOp(IRef<IBitmapImpl> source, Rect sourceRect, Rect destRect, BitmapInterpolationMode interpolationMode)
            {
                _source = source;
                _sourceRect = sourceRect;
                _destRect = destRect;
                _interpolationMode = interpolationMode;
            }

            public void Dispose()
            {
                _source.Dispose();
            }

            public bool Equals(ICustomDrawOperation other) => false;

            public bool HitTest(Point p) => false;

            public void Render(IDrawingContextImpl context)
            {
                context.DrawImage(_source, 1, _sourceRect, _destRect, _interpolationMode);
            }
        }

        public override void Render(DrawingContext context)
        {
            if (LibVLCAvaloniaOptions.UseCustomDrawOperationRendering)
            {
                var source = Source;
                if (source != null)
                {
                    Rect viewPort = new Rect(Bounds.Size);
                    Size sourceSize = new Size(source.PixelSize.Width, source.PixelSize.Height);
                    Vector scale = Stretch.CalculateScaling(Bounds.Size, sourceSize);
                    Size scaledSize = sourceSize * scale;
                    Rect destRect = viewPort
                        .CenterRect(new Rect(scaledSize))
                        .Intersect(viewPort);
                    Rect sourceRect = new Rect(sourceSize)
                        .CenterRect(new Rect(destRect.Size / scale));

                    var interpolationMode = RenderOptions.GetBitmapInterpolationMode(this);

                    context.Custom(new CustomOp(source.PlatformImpl.Clone(), sourceRect, destRect, interpolationMode));
                }
            }
            else
            {
                base.Render(context);
            }
        }
    }
}
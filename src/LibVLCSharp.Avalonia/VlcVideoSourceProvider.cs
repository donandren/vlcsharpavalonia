using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using vlc = LibVLCSharp.Shared;

namespace LibVLCSharp.Avalonia
{
    /// <summary>
    /// The class that can provide a Avalonia Image Source to display the video.
    /// </summary>
    public class VlcVideoSourceProvider : INotifyPropertyChanged, IDisposable
    {
        private IntPtr _buffer;
        private object[] _callbacks;
        private PixelSize _formatSize;
        private Action _invalidate;
        private PixelFormat _pixelFormat;
        private uint _pixelFormatPixelSize;
        private WriteableBitmap _videoSource;
        private WriteableBitmap _currentVideoSource;
        private PixelSize _videoSourcePixelSize;
        private readonly object _videoSourceLock = new object();

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// The vlc media player instance.
        /// </summary>
        public vlc.MediaPlayer MediaPlayer { get; private set; }

        /// <summary>
        /// The Image source that represents the video.
        /// </summary>
        public IBitmap VideoSource
        {
            get => _videoSource;

            private set
            {
                if (_videoSource != value)
                {
                    var old = _videoSource;
                    _videoSource = (WriteableBitmap)value;
                    OnPropertyChanged(nameof(VideoSource));
                    old?.Dispose();
                }
            }
        }

        private static void ToFourCC(string fourCCString, IntPtr destination)
        {
            if (fourCCString.Length != 4)
            {
                throw new ArgumentException("4CC codes must be 4 characters long", nameof(fourCCString));
            }

            var bytes = Encoding.ASCII.GetBytes(fourCCString);

            for (var i = 0; i < 4; i++)
            {
                Marshal.WriteByte(destination, i, bytes[i]);
            }
        }

        public void Init(vlc.MediaPlayer player, Action invalidate)
        {
            MediaPlayer = player;
            _invalidate = invalidate;

            var c = new vlc.MediaPlayer.LibVLCVideoCleanupCb(CleanupCallback);
            var f = new vlc.MediaPlayer.LibVLCVideoFormatCb(VideoFormatCallback);
            MediaPlayer.SetVideoFormatCallbacks(f, c);

            var lv = new vlc.MediaPlayer.LibVLCVideoLockCb(LockVideo);
            var uv = new vlc.MediaPlayer.LibVLCVideoUnlockCb(UnlockVideo);
            var d = new vlc.MediaPlayer.LibVLCVideoDisplayCb(DisplayVideo);
            MediaPlayer.SetVideoCallbacks(lv, uv, d);

            //we need GC not collect delegates
            _callbacks = new object[] { c, f, lv, uv, d };
        }

        protected virtual void OnPropertyChanged(string propertyName)
                    => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        /// <summary>
        /// Removes the video Source
        /// </summary>
        private void CleanUp()
        {
            lock (_videoSourceLock)
            {
                VideoSource = _currentVideoSource;
                VideoSource = null;
                _currentVideoSource = null;
            }
        }

        /// <summary>
        /// Aligns dimension to the next multiple of mod
        /// </summary>
        /// <param name="dimension">The dimension to be aligned</param>
        /// <param name="mod">The modulus</param>
        /// <returns>The aligned dimension</returns>
        private uint GetAlignedDimension(uint dimension, uint mod)
        {
            var modResult = dimension % mod;
            if (modResult == 0)
            {
                return dimension;
            }

            return dimension + mod - (dimension % mod);
        }

        #region Vlc video callbacks

        /// <summary>
        /// Called by Vlc when it requires a cleanup
        /// </summary>
        /// <param name="opaque">The parameter is not used</param>
        private void CleanupCallback(ref IntPtr opaque)
        {
            Marshal.FreeHGlobal(_buffer);

            if (!_disposed)
            {
                Dispatcher.UIThread.InvokeAsync(() => CleanUp());
            }
        }

        //
        // Summary:
        //     Callback prototype to display a picture.
        //
        // Parameters:
        //   opaque:
        //     private pointer as passed to libvlc_video_set_callbacks() [IN]
        //
        //   picture:
        //     private pointer returned from the
        //
        // Remarks:
        //     When the video frame needs to be shown, as determined by the media playback
        //     clock, the display callback is invoked.
        //     callback [IN]
        private void DisplayVideo(IntPtr opaque, IntPtr picture)
        {
            WriteableBitmap wb;
            lock (_videoSourceLock)
            {
                wb = _currentVideoSource;

                if (wb == null || _videoSourcePixelSize != _formatSize)
                {
                    _currentVideoSource = wb = new WriteableBitmap(_formatSize, new Vector(96, 96), _pixelFormat);
                    _videoSourcePixelSize = _formatSize;
                }

                using (var fb = wb.Lock())
                {
                    unsafe
                    {
                        long size = fb.Size.Width * fb.Size.Height * 4;
                        Buffer.MemoryCopy((void*)opaque, (void*)fb.Address, size, size);
                    }
                }
            }

            Dispatcher.UIThread.InvokeAsync(() =>
            {
                lock (_videoSourceLock)
                {
                    VideoSource = _currentVideoSource;
                }

                _invalidate();
            });
        }

        /// <summary>Callback prototype to allocate and lock a picture buffer.</summary>
        /// <param name="opaque">private pointer as passed to libvlc_video_set_callbacks() [IN]</param>
        /// <param name="planes">
        /// <para>start address of the pixel planes (LibVLC allocates the array</para>
        /// <para>of void pointers, this callback must initialize the array) [OUT]</para>
        /// </param>
        /// <returns>
        /// <para>a private pointer for the display and unlock callbacks to identify</para>
        /// <para>the picture buffers</para>
        /// </returns>
        /// <remarks>
        /// <para>Whenever a new video frame needs to be decoded, the lock callback is</para>
        /// <para>invoked. Depending on the video chroma, one or three pixel planes of</para>
        /// <para>adequate dimensions must be returned via the second parameter. Those</para>
        /// <para>planes must be aligned on 32-bytes boundaries.</para>
        /// </remarks>
        private IntPtr LockVideo(IntPtr opaque, IntPtr planes)
        {
            Marshal.WriteIntPtr(planes, opaque);
            return opaque;
        }

        /// <summary>Callback prototype to unlock a picture buffer.</summary>
        /// <param name="opaque">private pointer as passed to libvlc_video_set_callbacks() [IN]</param>
        /// <param name="picture">private pointer returned from the</param>
        /// <param name="planes">pixel planes as defined by the</param>
        /// <remarks>
        /// <para>When the video frame decoding is complete, the unlock callback is invoked.</para>
        /// <para>This callback might not be needed at all. It is only an indication that the</para>
        /// <para>application can now read the pixel values if it needs to.</para>
        /// <para>A picture buffer is unlocked after the picture is decoded,</para>
        /// <para>but before the picture is displayed.</para>
        /// <para>callback [IN]</para>
        /// <para>callback (this parameter is only for convenience) [IN]</para>
        /// </remarks>
        private void UnlockVideo(IntPtr opaque, IntPtr picture, IntPtr planes)
        {
        }

        /// <summary>
        /// <para>Callback prototype to configure picture buffers format.</para>
        /// <para>This callback gets the format of the video as output by the video decoder</para>
        /// <para>and the chain of video filters (if any). It can opt to change any parameter</para>
        /// <para>as it needs. In that case, LibVLC will attempt to convert the video format</para>
        /// <para>(rescaling and chroma conversion) but these operations can be CPU intensive.</para>
        /// </summary>
        /// <param name="opaque">
        /// <para>pointer to the private pointer passed to</para>
        /// <para>libvlc_video_set_callbacks() [IN/OUT]</para>
        /// </param>
        /// <param name="chroma">pointer to the 4 bytes video format identifier [IN/OUT]</param>
        /// <param name="width">pointer to the pixel width [IN/OUT]</param>
        /// <param name="height">pointer to the pixel height [IN/OUT]</param>
        /// <param name="pitches">
        /// <para>table of scanline pitches in bytes for each pixel plane</para>
        /// <para>(the table is allocated by LibVLC) [OUT]</para>
        /// </param>
        /// <param name="lines">table of scanlines count for each plane [OUT]</param>
        /// <returns>the number of picture buffers allocated, 0 indicates failure</returns>
        /// <remarks>
        /// <para>For each pixels plane, the scanline pitch must be bigger than or equal to</para>
        /// <para>the number of bytes per pixel multiplied by the pixel width.</para>
        /// <para>Similarly, the number of scanlines must be bigger than of equal to</para>
        /// <para>the pixel height.</para>
        /// <para>Furthermore, we recommend that pitches and lines be multiple of 32</para>
        /// <para>to not break assumptions that might be held by optimized code</para>
        /// <para>in the video decoders, video filters and/or video converters.</para>
        /// </remarks>
        private uint VideoFormatCallback(ref IntPtr opaque, IntPtr chroma, ref uint width, ref uint height, ref uint pitches, ref uint lines)
        {
            _pixelFormat = PixelFormat.Bgra8888;
            _pixelFormatPixelSize = 4;
            _formatSize = new PixelSize((int)width, (int)height);

            ToFourCC("BGRA", chroma);
            //or ToFourCC("RV32", chroma);

            pitches = GetAlignedDimension(width * _pixelFormatPixelSize, 32);
            lines = GetAlignedDimension(height, 32);

            var size = pitches * lines;

            opaque = _buffer = Marshal.AllocHGlobal((int)size);
            return 1;
        }

        #endregion Vlc video callbacks

        #region IDisposable Support

        private bool _disposed = false;

        /// <summary>
        /// The destructor
        /// </summary>
        ~VlcVideoSourceProvider()
        {
            Dispose(false);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the control.
        /// </summary>
        /// <param name="disposing">The parameter is not used.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;
                MediaPlayer = null;
                CleanUp();
            }
        }

        #endregion IDisposable Support
    }
}
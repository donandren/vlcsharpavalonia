namespace LibVLCSharp.Avalonia
{
    public enum LibVLCAvaloniaRenderingOptions
    {
        /// <summary>
        /// uses native handle passed to vlc, best possible performance
        /// </summary>
        VlcNative,

        /// <summary>
        /// uses default avalonia rendering with image
        /// </summary>
        Avalonia,

        /// <summary>
        /// uses default avalonia rendering with custom drawing operation, expected better performace with avalonia
        /// </summary>
        AvaloniaCustomDrawingOperation
    }

    internal class LibVLCAvaloniaOptions
    {
        public static LibVLCAvaloniaRenderingOptions RenderingOptions { get; set; } = LibVLCAvaloniaRenderingOptions.VlcNative;
    }
}
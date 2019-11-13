using Avalonia.Controls;
using LibVLCSharp.Shared;

namespace LibVLCSharp.Avalonia
{
    public static class AppBuilderExtensions
    {
        public static T UseVLCSharp<T>(this AppBuilderBase<T> b, bool? useCutomDrawOperationRendering = null, string libvlcDirectoryPath = null)
            where T : AppBuilderBase<T>, new()
        {
            if (useCutomDrawOperationRendering != null)
            {
                LibVLCAvaloniaOptions.UseCustomDrawOperationRendering = useCutomDrawOperationRendering.Value;
            }

            return b.AfterSetup(_ => Core.Initialize(libvlcDirectoryPath));
        }
    }
}
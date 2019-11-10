using Avalonia.Controls;
using LibVLCSharp.Shared;

namespace LibVLCSharp.Avalonia
{
    public static class AppBuilderExtensions
    {
        public static T UseVLCSharp<T>(this AppBuilderBase<T> b, string libvlcDirectoryPath = null)
            where T : AppBuilderBase<T>, new()
        {
            return b.AfterSetup(_ => Core.Initialize(libvlcDirectoryPath));
        }
    }
}

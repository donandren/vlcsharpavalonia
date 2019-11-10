using Avalonia;
using Avalonia.Markup.Xaml;
using LibVLCSharp.Shared;

namespace LibVLCSharp.Avalonia.Sample
{
    public class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}

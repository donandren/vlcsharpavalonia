using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;

namespace LibVLCSharp.Avalonia.Sample
{
    public class Example2 : Window
    {
        public Example2()
        {
            this.InitializeComponent();
            //Renderer.DrawFps = true;

            DataContext = new Example2ViewModel(this);
#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            (DataContext as IDisposable)?.Dispose();
        }
    }
}
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace LibVLCSharp.Avalonia.Sample
{
    public class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            this.Get<Button>("example1").Click += (s, e) =>
            {
                var w = new Example1();
                w.Show();
            };

#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
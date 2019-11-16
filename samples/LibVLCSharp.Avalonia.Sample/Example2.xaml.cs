using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;
using System.Reactive.Linq;

namespace LibVLCSharp.Avalonia.Sample
{
    public class Example2 : Window
    {
        public Example2()
        {
            this.InitializeComponent();
            //Renderer.DrawFps = true;

            DataContext = new Example2ViewModel(this);

            //it's open when we set text a bug in autocomplete?
            var autoComplete = this.Get<AutoCompleteBox>("mediaUrl");
            autoComplete.GetObservable(AutoCompleteBox.IsDropDownOpenProperty)
                            .Skip(1).Take(1)
                            .Subscribe(_ => autoComplete.IsDropDownOpen = false);

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
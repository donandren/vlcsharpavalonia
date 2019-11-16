using Avalonia;
using Avalonia.Controls;
using Avalonia.Logging;
using Avalonia.Rendering;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Threading;

namespace LibVLCSharp.Avalonia.Sample
{
    public class RenderLoop60fps : IRenderLoop
    {
        private readonly IDispatcher _dispatcher;
        private List<IRenderLoopTask> _items = new List<IRenderLoopTask>();
        private IRenderTimer _timer;
        private int _inTick;
        private int _inUpdate;

        /// <summary>
        /// Initializes a new instance of the <see cref="RenderLoop"/> class.
        /// </summary>
        public RenderLoop60fps()
        {
            _dispatcher = Dispatcher.UIThread;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RenderLoop"/> class.
        /// </summary>
        /// <param name="timer">The render timer.</param>
        /// <param name="dispatcher">The UI thread dispatcher.</param>
        public RenderLoop60fps(IRenderTimer timer, IDispatcher dispatcher)
        {
            _timer = timer;
            _dispatcher = dispatcher;
        }

        /// <summary>
        /// Gets the render timer.
        /// </summary>
        protected IRenderTimer Timer
        {
            get
            {
                if (_timer == null)
                {
                    _timer = AvaloniaLocator.Current.GetService<IRenderTimer>();
                }

                return _timer;
            }
        }

        /// <inheritdoc/>
        public void Add(IRenderLoopTask i)
        {
            Contract.Requires<ArgumentNullException>(i != null);
            Dispatcher.UIThread.VerifyAccess();

            _items.Add(i);

            if (_items.Count == 1)
            {
                Timer.Tick += TimerTick;
            }
        }

        /// <inheritdoc/>
        public void Remove(IRenderLoopTask i)
        {
            Contract.Requires<ArgumentNullException>(i != null);
            Dispatcher.UIThread.VerifyAccess();

            _items.Remove(i);

            if (_items.Count == 0)
            {
                Timer.Tick -= TimerTick;
            }
        }

        //let's ensure we at worst case have 30 fps
        private readonly TimeSpan _updateFrameTimeout = TimeSpan.FromMilliseconds(1000.0 / 60);

        private void TimerTick(TimeSpan time)
        {
            if (Interlocked.CompareExchange(ref _inTick, 1, 0) == 0)
            {
                bool needsUpdate = false;
                try
                {
                    foreach (IRenderLoopTask item in _items)
                    {
                        if (item.NeedsUpdate)
                        {
                            needsUpdate = true;

                            break;
                        }
                    }

                    if (needsUpdate &&
                        Interlocked.CompareExchange(ref _inUpdate, 1, 0) == 0)
                    {
                        _dispatcher.InvokeAsync(() =>
                        {
                            for (var i = 0; i < _items.Count; ++i)
                            {
                                var item = _items[i];

                                if (item.NeedsUpdate)
                                {
                                    try
                                    {
                                        item.Update(time);
                                    }
                                    catch (Exception ex)
                                    {
                                        Logger.TryGet(LogEventLevel.Error)?.Log(LogArea.Visual, this, "Exception in render update: {Error}", ex);
                                    }
                                }
                            }

                            Interlocked.Exchange(ref _inUpdate, 0);
                        }, DispatcherPriority.Render).Wait(_updateFrameTimeout);
                    }

                    for (int i = 0; i < _items.Count; i++)
                    {
                        _items[i].Render();
                    }
                }
                catch (Exception ex)
                {
                    Logger.TryGet(LogEventLevel.Error)?.Log(LogArea.Visual, this, "Exception in render loop: {Error}", ex);
                }
                finally
                {
                    Interlocked.Exchange(ref _inTick, 0);
                }
            }
        }
    }

    public static class AppBuilderExtensions
    {
        public static T Use60fpsRendering<T>(this T builder) where T : AppBuilderBase<T>, new()
        {
            return builder.AfterSetup(_ => AvaloniaLocator.CurrentMutable.Bind<IRenderLoop>().ToConstant(new RenderLoop60fps()));
        }
    }
}
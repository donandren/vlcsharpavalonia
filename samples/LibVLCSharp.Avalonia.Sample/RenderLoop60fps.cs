using Avalonia;
using Avalonia.Controls;
using Avalonia.Logging;
using Avalonia.Platform;
using Avalonia.Rendering;
using Avalonia.Threading;
using Avalonia.VisualTree;
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

    public class UIRenderLoop60fps : IRenderLoop
    {
        private readonly IDispatcher _dispatcher;
        private List<IRenderLoopTask> _items = new List<IRenderLoopTask>();
        private IRenderTimer _timer;
        private int _inTick;
        private int _inUpdate;
        private IPlatformThreadingInterface _platform;
        private IDisposable _timerSubscription;

        private IPlatformThreadingInterface Platform => _platform ?? (_platform = AvaloniaLocator.Current.GetService<IPlatformThreadingInterface>());

        /// <summary>
        /// Initializes a new instance of the <see cref="RenderLoop"/> class.
        /// </summary>
        public UIRenderLoop60fps()
        {
            _dispatcher = Dispatcher.UIThread;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RenderLoop"/> class.
        /// </summary>
        /// <param name="timer">The render timer.</param>
        /// <param name="dispatcher">The UI thread dispatcher.</param>
        public UIRenderLoop60fps(IRenderTimer timer, IDispatcher dispatcher)
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
                _timerSubscription = Platform.StartTimer(DispatcherPriority.Render,
                    _updateFrameTimeout,
                    () => TimerTick(TimeSpan.FromMilliseconds(Environment.TickCount)));
                //Timer.Tick += TimerTick;
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
                _timerSubscription?.Dispose();
                _timerSubscription = null;
                //Timer.Tick -= TimerTick;
            }
        }

        //let's ensure we at worst case have 30 fps
        private readonly TimeSpan _updateFrameTimeout = TimeSpan.FromMilliseconds(1000.0 / 65);

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
                        // _dispatcher.InvokeAsync(() =>
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
                        }//, DispatcherPriority.Render).Wait(_updateFrameTimeout);
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

    public class DummyRenderLoop : IRenderLoop
    {
        public void Add(IRenderLoopTask i)
        {
        }

        public void Remove(IRenderLoopTask i)
        {
        }
    }

    public static class AppBuilderExtensions
    {
        public static T Use60fpsRendering<T>(this T builder) where T : AppBuilderBase<T>, new()
        {
            return builder.AfterSetup(_ => AvaloniaLocator.CurrentMutable.Bind<IRenderLoop>().ToConstant(new RenderLoop60fps()));
        }

        public static T UseUIThreadRendering<T>(this T builder) where T : AppBuilderBase<T>, new()
        {
            return builder.AfterSetup(_ => AvaloniaLocator.CurrentMutable.Bind<IRenderLoop>().ToConstant(new UIRenderLoop60fps()));
        }

        public static T UseCustomRenderer<T>(this T builder, bool usePaint = true) where T : AppBuilderBase<T>, new()
        {
            return builder.AfterSetup(_ => AvaloniaLocator.CurrentMutable.Bind<IRendererFactory>().ToConstant(new RendererFactory(usePaint)));
        }
    }

    public class RendererFactory : IRendererFactory
    {
        private bool _usePaint;

        public RendererFactory(bool usePaint)
        {
            _usePaint = usePaint;
        }

        public IRenderer Create(IRenderRoot root, IRenderLoop renderLoop)
        {
            return new CustomDeferredRenderer(_usePaint, root, new DeferredRenderer(root, !_usePaint ? renderLoop : new DummyRenderLoop()));
        }
    }

    public class CustomDeferredRenderer : IRenderer
    {
        private IRenderer _renderer;
        private IRenderRoot _root;
        private bool _usePaint;

        public CustomDeferredRenderer(bool usePaint, IRenderRoot root, IRenderer renderer)
        {
            _renderer = renderer;
            _root = root;
            _usePaint = usePaint;
        }

        public bool DrawFps { get => _renderer.DrawFps; set => _renderer.DrawFps = value; }
        public bool DrawDirtyRects { get => _renderer.DrawDirtyRects; set => _renderer.DrawDirtyRects = value; }

        public void AddDirty(IVisual visual)
        {
            _renderer.AddDirty(visual);
            if (_usePaint)
            {
                _root.Invalidate(new Rect(_root.Bounds.Size));
            }
        }

        public event EventHandler<SceneInvalidatedEventArgs> SceneInvalidated
        {
            add
            {
                _renderer.SceneInvalidated += value;
            }

            remove
            {
                _renderer.SceneInvalidated -= value;
            }
        }

        public void Dispose() => _renderer.Dispose();

        public IEnumerable<IVisual> HitTest(Point p, IVisual root, Func<IVisual, bool> filter)
            => _renderer.HitTest(p, root, filter);

        public void Paint(Rect rect) => _renderer.Paint(rect);

        public void RecalculateChildren(IVisual visual) => _renderer.RecalculateChildren(visual);

        public void Resized(Size size) => _renderer.Resized(size);

        public void Start() => _renderer.Start();

        public void Stop() => _renderer.Stop();
    }
}
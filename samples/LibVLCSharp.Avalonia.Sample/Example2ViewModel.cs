using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Threading;
using LibVLCSharp.Shared;
using ReactiveUI;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Windows.Input;

namespace LibVLCSharp.Avalonia.Sample
{
    public class Example2ViewModel : ReactiveObject, IDisposable
    {
        static Example2ViewModel()
        {
            LoadPlayed();
        }

        private static void LoadPlayed()
        {
            try
            {
                if (File.Exists("playhistory.txt"))
                    _played.AddRange(File.ReadAllLines("playhistory.txt"));
            }
            catch { }
        }

        private static void SavePlayed()
        {
            File.WriteAllLines("playhistory.txt", _played.ToArray());
        }

        private LibVLC _libVLC;
        private CompositeDisposable _subscriptions;
        private static AvaloniaList<string> _played = new AvaloniaList<string>();

        public Example2ViewModel(Window window)
        {
            _libVLC = new LibVLC();
            MediaPlayer = new MediaPlayer(_libVLC);
            bool operationActive = false;
            var refresh = new Subject<Unit>();

            //disable events while some operations active, as sometimes causing deadlocks
            IObservable<Unit> Wrap(IObservable<Unit> obs)
                => obs.Where(_ => !operationActive).Merge(refresh).ObserveOn(AvaloniaScheduler.Instance);

            IObservable<Unit> VLCEvent(string name)
                => Observable.FromEventPattern(MediaPlayer, name).Select(_ => Unit.Default);

            void Op(Action action)
            {
                operationActive = true;
                action();
                operationActive = false;
                refresh.OnNext(Unit.Default);
            };

            var positionChanged = VLCEvent(nameof(MediaPlayer.PositionChanged));
            var playingChanged = VLCEvent(nameof(MediaPlayer.Playing));
            var stoppedChanged = VLCEvent(nameof(MediaPlayer.Stopped));
            var timeChanged = VLCEvent(nameof(MediaPlayer.TimeChanged));
            var lengthChanged = VLCEvent(nameof(MediaPlayer.LengthChanged));
            var muteChanged = VLCEvent(nameof(MediaPlayer.Muted))
                                .Merge(VLCEvent(nameof(MediaPlayer.Unmuted)));
            var endReachedChanged = VLCEvent(nameof(MediaPlayer.EndReached));
            var pausedChanged = VLCEvent(nameof(MediaPlayer.Paused));
            var volumeChanged = VLCEvent(nameof(MediaPlayer.VolumeChanged));
            var stateChanged = Observable.Merge(playingChanged, stoppedChanged, endReachedChanged, pausedChanged);
            var hasMediaObservable = this.WhenAnyValue(v => v.MediaUrl, v => !string.IsNullOrEmpty(v));
            var fullState = Observable.Merge(
                                stateChanged,
                                VLCEvent(nameof(MediaPlayer.NothingSpecial)),
                                VLCEvent(nameof(MediaPlayer.Buffering)),
                                VLCEvent(nameof(MediaPlayer.EncounteredError))
                                );

            _subscriptions = new CompositeDisposable
            {
                Wrap(positionChanged).DistinctUntilChanged(_ => Position).Subscribe(_ => this.RaisePropertyChanged(nameof(Position))),
                Wrap(timeChanged).DistinctUntilChanged(_ => CurrentTime).Subscribe(_ => this.RaisePropertyChanged(nameof(CurrentTime))),
                Wrap(lengthChanged).DistinctUntilChanged(_ => Duration).Subscribe(_ => this.RaisePropertyChanged(nameof(Duration))),
                Wrap(muteChanged).DistinctUntilChanged(_ => IsMuted).Subscribe(_ => this.RaisePropertyChanged(nameof(IsMuted))),
                Wrap(fullState).DistinctUntilChanged(_ => State).Subscribe(_ => this.RaisePropertyChanged(nameof(State))),
                Wrap(volumeChanged).DistinctUntilChanged(_ => Volume).Subscribe(_ => this.RaisePropertyChanged(nameof(Volume))),
                Wrap(fullState).DistinctUntilChanged(_ => Information).Subscribe(_ => this.RaisePropertyChanged(nameof(Information)))
            };

            bool active() => _subscriptions == null ? false : MediaPlayer.IsPlaying || MediaPlayer.CanPause;

            stateChanged = Wrap(stateChanged);

            PlayCommand = ReactiveCommand.Create(
               () => Op(() =>
               {
                   MediaPlayer.Media = new Media(_libVLC, new Uri(MediaUrl).AbsoluteUri, FromType.FromLocation);
                   MediaPlayer.Play();
               }),
               hasMediaObservable);

            StopCommand = ReactiveCommand.Create(
                () => Op(() => MediaPlayer.Stop()),
                stateChanged.Select(_ => active()));

            PauseCommand = ReactiveCommand.Create(
                () => MediaPlayer.Pause(),
                 stateChanged.Select(_ => active()));

            ForwardCommand = ReactiveCommand.Create(
                () => MediaPlayer.Time += 1000,
                stateChanged.Select(_ => active()));

            BackwardCommand = ReactiveCommand.Create(
                () => MediaPlayer.Time -= 1000,
                stateChanged.Select(_ => active()));

            NextFrameCommand = ReactiveCommand.Create(
                () => MediaPlayer.NextFrame(),
                stateChanged.Select(_ => active()));

            OpenCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                var fd = new OpenFileDialog() { AllowMultiple = false };
                var res = await fd.ShowAsync(window);
                if (res.Any())
                {
                    MediaUrl = res.FirstOrDefault();
                    PlayCommand.Execute(null);
                }
            });

            MediaUrl = "http://commondatastorage.googleapis.com/gtv-videos-bucket/sample/BigBuckBunny.mp4";

            Wrap(playingChanged).Subscribe(_ =>
            {
                if (!_played.Contains(MediaUrl))
                {
                    _played.Add(MediaUrl);
                    SavePlayed();
                }
            });
        }

        public MediaPlayer MediaPlayer { get; }

        private string _MediaUrl;

        public string MediaUrl
        {
            get => _MediaUrl;
            set => this.RaiseAndSetIfChanged(ref _MediaUrl, value);
        }

        public bool IsMuted
        {
            get => MediaPlayer.Mute;
            set => MediaPlayer.Mute = value;
        }

        public TimeSpan CurrentTime => TimeSpan.FromMilliseconds(MediaPlayer.Time > -1 ? MediaPlayer.Time : 0);
        public TimeSpan Duration => TimeSpan.FromMilliseconds(MediaPlayer.Length > -1 ? MediaPlayer.Length : 0);
        public VLCState State => MediaPlayer.State;

        public string MediaInfo
        {
            get
            {
                var m = MediaPlayer.Media;

                if (m == null)
                    return "";

                var vt = m.Tracks.FirstOrDefault(t => t.TrackType == TrackType.Video);
                var at = m.Tracks.FirstOrDefault(t => t.TrackType == TrackType.Audio);
                var videoCodec = m.CodecDescription(TrackType.Video, vt.Codec);
                var audioCodec = m.CodecDescription(TrackType.Audio, at.Codec);

                return $"{vt.Data.Video.Width}x{vt.Data.Video.Height} {vt.Description}video: {videoCodec} audio: {audioCodec}";
            }
        }

        public string Information => $"FPS:{MediaPlayer.Fps} {MediaInfo}";

        public float Position
        {
            get => MediaPlayer.Position * 100.0f;
            set
            {
                if (MediaPlayer.Position != value / 100.0f)
                {
                    MediaPlayer.Position = value / 100.0f;
                }
            }
        }

        public int Volume
        {
            get => MediaPlayer.Volume;
            set => MediaPlayer.Volume = value;
        }

        public ICommand PlayCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand PauseCommand { get; }
        public ICommand ForwardCommand { get; }
        public ICommand BackwardCommand { get; }
        public ICommand NextFrameCommand { get; }
        public ICommand OpenCommand { get; }

        public IEnumerable Played => _played;

        public void Dispose()
        {
            _subscriptions.Dispose();
            _subscriptions = null;
            MediaPlayer.Dispose();
        }
    }
}
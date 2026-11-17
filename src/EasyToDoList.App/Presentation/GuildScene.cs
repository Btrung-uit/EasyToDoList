using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace EasyToDoList.App.Presentation;

// Cached, frozen PNGs keep decoding and allocation out of animation ticks.
public sealed class GuildScene : FrameworkElement
{
    public static readonly DependencyProperty MotionEnabledProperty = DependencyProperty.Register(
        nameof(MotionEnabled), typeof(bool), typeof(GuildScene), new FrameworkPropertyMetadata(true, OnMotionChanged));
    public static readonly DependencyProperty MascotStateProperty = DependencyProperty.Register(
        nameof(MascotState), typeof(string), typeof(GuildScene), new FrameworkPropertyMetadata("idle", FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty MascotOnlyProperty = DependencyProperty.Register(
        nameof(MascotOnly), typeof(bool), typeof(GuildScene), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly Dictionary<string, BitmapSource> Images = new();
    private static readonly string[] Clouds = ["small", "medium", "large"];
    private readonly Stopwatch _clock = new();
    private readonly DispatcherTimer _timer;
    private double? _celebration;
    private readonly double _grassOffset = Random.Shared.NextDouble();

    public GuildScene()
    {
        IsHitTestVisible = false;
        ClipToBounds = true;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
        _timer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = TimeSpan.FromSeconds(1d / 24) };
        _timer.Tick += (_, _) => InvalidateVisual();
        Loaded += (_, _) => UpdatePlayback();
        Unloaded += (_, _) => { _timer.Stop(); _clock.Stop(); };
        IsVisibleChanged += (_, _) => UpdatePlayback();
    }

    public bool MotionEnabled { get => (bool)GetValue(MotionEnabledProperty); set => SetValue(MotionEnabledProperty, value); }
    public string MascotState { get => (string)GetValue(MascotStateProperty); set => SetValue(MascotStateProperty, value); }
    public bool MascotOnly { get => (bool)GetValue(MascotOnlyProperty); set => SetValue(MascotOnlyProperty, value); }
    internal bool IsAnimating => _timer.IsEnabled;

    public void Celebrate()
    {
        _celebration = MotionEnabled && IsVisible ? _clock.Elapsed.TotalSeconds : null;
        InvalidateVisual();
    }

    private static void OnMotionChanged(DependencyObject owner, DependencyPropertyChangedEventArgs e) => ((GuildScene)owner).UpdatePlayback();

    private void UpdatePlayback()
    {
        if (IsLoaded && IsVisible && MotionEnabled) { _clock.Start(); _timer.Start(); }
        else { _timer.Stop(); _clock.Stop(); _celebration = null; }
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext context)
    {
        var seconds = _clock.Elapsed.TotalSeconds;
        if (MascotOnly)
        {
            var (state, frame) = SceneAnimation.Mascot(MascotState, seconds, MotionEnabled, _celebration);
            var size = Math.Min(ActualWidth, ActualHeight);
            Draw(context, $"Mascots/{state}/{state}-{frame:00}.png", (ActualWidth - size) / 2, 0, size, size);
            return;
        }

        var time = SceneAnimation.MotionTime(seconds, MotionEnabled);
        for (var i = 0; i < Clouds.Length; i++)
        {
            var x = (ActualWidth * (0.15 + i * 0.35) + time * (8 + i * 4)) % (ActualWidth + 200) - 100;
            Draw(context, $"Ambient/clouds/cloud-{Clouds[i]}.png", x, 8 + i * 16, 160, 80);
        }
        Draw(context, $"Ambient/flag/flag-{SceneAnimation.Frame(seconds, 6, MotionEnabled):00}.png", 0, ActualHeight - 160, 160, 160);
        Draw(context, $"Ambient/grass/grass-{SceneAnimation.Frame(seconds + _grassOffset, 4, MotionEnabled):00}.png", ActualWidth - 180, ActualHeight - 160, 160, 160);
        if (!MotionEnabled) return;
        for (var i = 0; i < 2; i++)
        {
            var progress = ((time + i * 8) % 18) / 18;
            Draw(context, $"Ambient/leaves/leaf-{Math.Min(7, (int)(progress * 8)):00}.png",
                ActualWidth * (0.15 + i * 0.6) + progress * 70, progress * (ActualHeight + 20) - 20, 14, 14);
        }
    }

    private static void Draw(DrawingContext context, string path, double x, double y, double width, double height)
    {
        if (!Images.TryGetValue(path, out var image))
        {
            image = new BitmapImage(new Uri($"pack://application:,,,/EasyToDoList.App;component/Assets/{path}"));
            image.Freeze();
            Images.Add(path, image);
        }
        context.DrawImage(image, new Rect(x, y, width, height));
    }
}

using System.Windows;
using System.Windows.Media;

namespace EasyToDoList.App.Features.Today;

public sealed class ComboRing : FrameworkElement
{
    public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
        nameof(Progress), typeof(double), typeof(ComboRing),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
    public double Progress { get => (double)GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        var radius = Math.Max(0, Math.Min(ActualWidth, ActualHeight) / 2 - 3);
        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        var pen = new Pen((Brush?)TryFindResource("GuildGoldBrush") ?? Brushes.Gold, 3);
        if (Progress >= 1) { dc.DrawEllipse(null, pen, center, radius, radius); return; }
        if (Progress <= 0) return;
        var angle = Progress * Math.PI * 2;
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(new Point(center.X, center.Y - radius), false, false);
            context.ArcTo(new Point(center.X + Math.Sin(angle) * radius, center.Y - Math.Cos(angle) * radius),
                new Size(radius, radius), 0, Progress > 0.5, SweepDirection.Clockwise, true, false);
        }
        dc.DrawGeometry(null, pen, geometry);
    }
}

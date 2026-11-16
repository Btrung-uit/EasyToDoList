using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EasyToDoList.App.Features.Today;

// Rendering consumes committed presentation state; it never changes a Todo or its reward.
public sealed class CompletionSurface : Decorator
{
    public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
        nameof(Progress), typeof(double), typeof(CompletionSurface),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender, OnProgressChanged));
    public double Progress { get => (double)GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }

    private static void OnProgressChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var surface = (CompletionSurface)sender;
        if (surface.Child is not UIElement child) return;
        var progress = (double)e.NewValue;
        child.Opacity = progress < 0.5 ? 1 : Math.Max(0, 2 - progress * 2);
        child.RenderTransformOrigin = new Point(0.5, 0.5);
        child.RenderTransform = new ScaleTransform(1, progress < 0.75 ? 1 : Math.Max(0, (1 - progress) * 4));
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var p = Progress;
        if (p <= 0 || p >= 1) return;
        var gold = (Brush?)TryFindResource("GuildGoldBrush") ?? Brushes.Gold;
        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        if (p < 0.5)
        {
            if (p >= 0.2)
            {
                dc.PushOpacity(0.3 + (p - 0.2));
                dc.DrawRoundedRectangle(gold, null, new Rect(RenderSize), 4, 4);
                dc.Pop();
            }
            var pen = new Pen(gold, 2);
            dc.DrawLine(pen, new Point(center.X - 12, 0), center);
            dc.DrawLine(pen, center, new Point(center.X - 8, ActualHeight));
            dc.DrawLine(pen, center, new Point(center.X + 20, center.Y - 8));
        }
        else
        {
            dc.PushOpacity((1 - p) * 2);
            for (var i = 0; i < 12; i++)
            {
                var angle = i * Math.PI / 6;
                var distance = 12 + (p - 0.5) * 85;
                dc.DrawRectangle(gold, null, new Rect(center.X + Math.Cos(angle) * distance,
                    center.Y + Math.Sin(angle) * distance, 4, 4));
            }
            dc.Pop();
        }
    }
}

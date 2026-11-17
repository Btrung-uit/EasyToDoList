using System.Windows.Controls;
using System.Windows;

namespace EasyToDoList.App.Features.Today;

public partial class CompletionFeedbackView : UserControl
{
    public static readonly DependencyProperty IsCompactProperty = DependencyProperty.Register(
        nameof(IsCompact), typeof(bool), typeof(CompletionFeedbackView), new PropertyMetadata(false));

    public bool IsCompact { get => (bool)GetValue(IsCompactProperty); set => SetValue(IsCompactProperty, value); }

    public CompletionFeedbackView() => InitializeComponent();
}

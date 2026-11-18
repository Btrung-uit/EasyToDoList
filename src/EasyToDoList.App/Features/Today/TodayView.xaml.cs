using System.Windows;
using System.Windows.Controls;
namespace EasyToDoList.App.Features.Today;
public partial class TodayView : UserControl
{
    public TodayView() => InitializeComponent();
    private void OnFocusTitle(object sender, RoutedEventArgs e) => TitleInput.Focus();
    private void OnCancelTask(object sender, RoutedEventArgs e)
    {
        if (DataContext is TodayViewModel vm && sender is Button { DataContext: TodoRowViewModel row }
            && MessageBox.Show(Window.GetWindow(this), $"Hủy nhiệm vụ “{row.Title}”? Nhiệm vụ vẫn được giữ trong Lịch sử.",
                "Hủy nhiệm vụ", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
            vm.CancelCommand.Execute(row);
    }
}

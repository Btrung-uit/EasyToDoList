using System.Windows;
using System.Windows.Controls;
namespace EasyToDoList.App.Features.Today;
public partial class TodayView : UserControl
{
    public TodayView() => InitializeComponent();
    private void OnFocusTitle(object sender, RoutedEventArgs e) => TitleInput.Focus();
    private void OnCompleteChecked(object sender, RoutedEventArgs e)
    {
        // UI Automation Toggle raises Checked without executing CheckBox.Command.
        if (DataContext is TodayViewModel vm && sender is CheckBox { DataContext: TodoRowViewModel row }
            && !row.IsCompleting && vm.CompleteCommand.CanExecute(row))
            vm.CompleteCommand.Execute(row);
    }
    private void OnCancelTask(object sender, RoutedEventArgs e)
    {
        if (DataContext is TodayViewModel vm && sender is Button { DataContext: TodoRowViewModel row }
            && MessageBox.Show(Window.GetWindow(this), $"Hủy nhiệm vụ “{row.Title}”? Nhiệm vụ vẫn được giữ trong Lịch sử.",
                "Hủy nhiệm vụ", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
            vm.CancelCommand.Execute(row);
    }
}

using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using EasyToDoList.App.Features.Calendar;
using EasyToDoList.App.Features.Dashboard;
using EasyToDoList.App.Features.Overlay;
using EasyToDoList.App.Features.Profile;
using EasyToDoList.App.Features.Settings;
using EasyToDoList.App.Features.Today;
using EasyToDoList.App.Shell;
using EasyToDoList.App.Windows;
using EasyToDoList.App.Presentation;
using EasyToDoList.Core.Common;
using EasyToDoList.Core.Economy;
using EasyToDoList.Core.Pomodoro;
using EasyToDoList.Core.Todos;
using EasyToDoList.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EasyToDoList.App.Tests.Features;

public sealed class CompletionWpfTests
{
    [Fact]
    public Task Window_renders_committed_feedback_switches_modes_and_undo_reverses_sqlite_once() =>
        RunOnDispatcherAsync(CheckWindowAsync);

    private static async Task CheckDashboardRoundTripsAsync(WindowState dashboardState)
    {
        var window = new Window { Content = new Border { Height = 180 } };
        try
        {
            var controller = new WindowModeController(window);
            controller.Apply(ShellDisplayMode.Dashboard);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            var area = WindowModeController.GetWorkArea(window);
            window.Width = Math.Min(1040, area.Width);
            window.Height = Math.Min(680, area.Height);
            window.Left = area.Left + (area.Width - window.Width) / 3;
            window.Top = area.Top + (area.Height - window.Height) / 3;
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            var original = window.RestoreBounds;
            window.WindowState = dashboardState;
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);

            for (var iteration = 0; iteration < 2; iteration++)
            {
                if (iteration == 1) controller.Apply(ShellDisplayMode.HiddenToTray);
                controller.Apply(ShellDisplayMode.Overlay, area.Right - 340, area.Top + 20);
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                Assert.Equal(WindowState.Normal, window.WindowState);
                Assert.True(window.ActualWidth <= 320);
                Assert.True(window.ActualHeight < 360);
                controller.Apply(ShellDisplayMode.Dashboard);
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                Assert.Equal(dashboardState, window.WindowState);
                AssertBounds(original, window.RestoreBounds);
            }

            controller.Apply(ShellDisplayMode.HiddenToTray);
            controller.Apply(ShellDisplayMode.Dashboard);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Assert.Equal(dashboardState, window.WindowState);
            AssertBounds(original, window.RestoreBounds);
            window.WindowState = WindowState.Normal;
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            AssertBounds(original, new Rect(window.Left, window.Top, window.ActualWidth, window.ActualHeight));
        }
        finally { window.Close(); }
    }

    private static async Task CheckDashboardFallbackAsync()
    {
        var window = new Window { Content = new Border { Height = 180 } };
        try
        {
            var controller = new WindowModeController(window);
            controller.Apply(ShellDisplayMode.Overlay);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            controller.Apply(ShellDisplayMode.Dashboard);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            var area = WindowModeController.GetWorkArea(window);
            Assert.InRange(Math.Abs(window.Left + window.ActualWidth / 2 - (area.Left + area.Width / 2)), 0, 1);
            Assert.InRange(Math.Abs(window.Top + window.ActualHeight / 2 - (area.Top + area.Height / 2)), 0, 1);
        }
        finally { window.Close(); }
    }

    private static async Task RunOnDispatcherAsync(Func<Task> check)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
            Dispatcher.CurrentDispatcher.InvokeAsync(async () =>
            {
                try { await check(); completion.SetResult(); }
                catch (Exception error) { completion.SetException(error); }
                finally { Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background); }
            });
            Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }

    private static void AssertBounds(Rect expected, Rect actual)
    {
        Assert.InRange(Math.Abs(expected.Left - actual.Left), 0, 1);
        Assert.InRange(Math.Abs(expected.Top - actual.Top), 0, 1);
        Assert.InRange(Math.Abs(expected.Width - actual.Width), 0, 1);
        Assert.InRange(Math.Abs(expected.Height - actual.Height), 0, 1);
    }

    private static async Task CheckWindowAsync()
    {
        // WPF cannot reopen modeless windows after Application.Shutdown in this test host.
        await CheckDashboardRoundTripsAsync(WindowState.Normal);
        await CheckDashboardRoundTripsAsync(WindowState.Maximized);
        await CheckDashboardFallbackAsync();
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        foreach (var theme in new[] { "Colors", "Spacing", "Typography", "Controls" })
            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri($"/EasyToDoList.App;component/Themes/{theme}.xaml", UriKind.Relative),
            });
        app.Resources.Add("BooleanToVisibilityConverter", new BooleanToVisibilityConverter());
        app.Resources.Add(new DataTemplateKey(typeof(TodayViewModel)), new DataTemplate(typeof(TodayViewModel))
        {
            VisualTree = new FrameworkElementFactory(typeof(TodayView)),
        });
        foreach (var (model, view) in new[]
        {
            (typeof(CalendarViewModel), typeof(CalendarView)),
            (typeof(SettingsViewModel), typeof(SettingsView)),
            (typeof(global::EasyToDoList.App.Features.Pomodoro.PomodoroViewModel), typeof(global::EasyToDoList.App.Features.Pomodoro.PomodoroView)),
        })
            app.Resources.Add(new DataTemplateKey(model), new DataTemplate(model) { VisualTree = new FrameworkElementFactory(view) });
        using var bindingLog = new StringWriter();
        using var listener = new TextWriterTraceListener(bindingLog);
        PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var clock = new Clock();
        var rewards = new SqliteRewardStore(db);
        var profiles = new SqliteProfileStore(db);
        var unit = new EfAppUnitOfWork(db);
        var service = new TodoService(new SqliteTodoStore(db), rewards, profiles, unit, new EconomyCalculator(), clock);
        await service.CreateAsync(new CreateTodoCommand("Ôn bài — runtime M09", clock.LocalToday, clock.LocalToday.AddDays(1)), CancellationToken.None);
        await service.CreateAsync(new CreateTodoCommand("Viết test tiếp theo", clock.LocalToday, clock.LocalToday), CancellationToken.None);
        var profile = new ProfileViewModel(new SqliteProfileQuery(db));
        var settings = new SettingsViewModel(new SqliteSettingsStore(db));
        var today = new TodayViewModel(service, clock, profile.LoadAsync,
            () => settings.AnimationsEnabled && !settings.ReducedMotionEnabled);
        var pomodoro = new PomodoroService(new SqlitePomodoroStore(db), rewards, profiles, unit, new EconomyCalculator(), clock);
        var pomodoroVm = new global::EasyToDoList.App.Features.Pomodoro.PomodoroViewModel();
        var dashboard = new DashboardViewModel(today, new CalendarViewModel(new SqliteTodoHistoryQuery(db), clock), pomodoroVm, profile, settings);
        await dashboard.InitializeAsync();
        settings.MasterVolume = 0;
        var shell = new ShellViewModel(pomodoro, dashboard, new PomodoroOverlayViewModel(pomodoro, pomodoroVm.TryBuildConfiguration));
        var window = new MainWindow(shell);
        using var tray = new SystemTrayService(
            () => shell.ShowOverlayCommand.Execute(null),
            () => shell.ShowDashboardCommand.Execute(null),
            () => shell.ExitCommand.Execute(null));
        try
        {
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            var dashboardBounds = window.RestoreBounds;
            shell.ShowOverlayCommand.Execute(OverlayMode.Todo);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            shell.ShowDashboardCommand.Execute(null);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            AssertBounds(dashboardBounds, window.RestoreBounds);
            window.WindowState = WindowState.Maximized;
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            shell.ShowOverlayCommand.Execute(OverlayMode.Todo);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Assert.Equal(WindowState.Normal, window.WindowState);
            Assert.True(window.ActualWidth <= 320 && window.ActualHeight < 360);
            SaveImage(window, "overlay-from-maximized.png");
            shell.ShowDashboardCommand.Execute(null);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Assert.Equal(WindowState.Maximized, window.WindowState);
            SaveImage(window, "dashboard-restored-maximized.png");
            window.WindowState = WindowState.Normal;
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            AssertBounds(dashboardBounds, new Rect(window.Left, window.Top, window.ActualWidth, window.ActualHeight));
            var scenes = Descendants<GuildScene>(window).ToArray();
            Assert.Equal(2, scenes.Length);
            Assert.All(scenes, scene => Assert.True(scene.IsAnimating));
            foreach (var state in new[] { "idle", "focus", "celebrate", "rest" })
                for (var frame = 0; frame < 6; frame++)
                {
                    var image = new BitmapImage(new Uri($"pack://application:,,,/EasyToDoList.App;component/Assets/Mascots/{state}/{state}-{frame:00}.png"));
                    Assert.Equal(320, image.PixelWidth);
                    Assert.Equal(320, image.PixelHeight);
                }
            var formFields = Descendants<TextBox>(window).Where(control => AutomationProperties.GetName(control) == "Tên nhiệm vụ")
                .Cast<FrameworkElement>().Concat(Descendants<DatePicker>(window))
                .Concat(Descendants<Button>(window).Where(button => Equals(button.Content, "Lưu"))).ToArray();
            Assert.Equal(4, formFields.Length);
            Assert.True(formFields.Max(field => field.ActualWidth) - formFields.Min(field => field.ActualWidth) <= 1,
                $"Editor widths differ: {string.Join(", ", formFields.Select(field => field.ActualWidth))}");
            Assert.All(formFields, field => Assert.Equal(40, field.ActualHeight));
            var dashboardScroll = Descendants<ScrollViewer>(window).Single(view => view.Name == "DashboardScroll");
            var viewportBeforeCompletion = dashboardScroll.ViewportHeight;
            var scrollBeforeCompletion = dashboardScroll.ScrollableHeight;
            var first = today.Items.Single(x => x.Title.StartsWith("Ôn bài"));
            today.EditCommand.Execute(first);
            await today.CompleteAsync(first);
            Assert.False(today.IsEditing);
            Assert.Equal(6, profile.Coins);
            Assert.Equal(13, Assert.Single(today.Feedback).Result.EarlyBonusExperience);
            clock.Advance(240);
            today.RefreshFeedback();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Assert.Equal(viewportBeforeCompletion, dashboardScroll.ViewportHeight);
            Assert.Equal(scrollBeforeCompletion, dashboardScroll.ScrollableHeight);
            var root = (FrameworkElement)window.Content;
            var toast = Assert.Single(Descendants<CompletionFeedbackView>(window), view => view.IsVisible);
            var toastBounds = toast.TransformToAncestor(root).TransformBounds(new Rect(toast.RenderSize));
            Assert.InRange(root.ActualWidth - toastBounds.Right, 15, 17);
            Assert.InRange(root.ActualHeight - toastBounds.Bottom, 15, 17);
            SaveImage(window, "m09-dashboard.png");
            SaveImage(window, "todo-completion-toast-dashboard.png");
            Assert.Contains(Descendants<CompletionSurface>(window), x => x.Progress > 0);
            var mascot = Descendants<Image>(window).Single(x => x.IsVisible && AutomationProperties.GetName(x).StartsWith("Hiệp sĩ"));
            Assert.NotNull(mascot.Source);

            shell.ShowOverlayCommand.Execute(OverlayMode.Todo);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Assert.True(window.Topmost);
            Assert.True(window.ActualWidth <= 320);
            Assert.True(window.ActualHeight < 360);
            Assert.DoesNotContain(Descendants<Button>(window), button => button.IsVisible && Equals(button.Content, "Thoát"));
            Assert.All(scenes, scene => Assert.False(scene.IsAnimating));
            var popup = Assert.IsType<System.Windows.Controls.Primitives.Popup>(window.FindName("OverlayFeedbackPopup"));
            Assert.True(popup.IsOpen);
            toast = Assert.IsType<CompletionFeedbackView>(popup.Child);
            Assert.True(toast.ActualHeight > 40);
            Assert.All(Descendants<TextBlock>(toast).Where(text => text.ToolTip is string),
                text => Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(text)));
            Assert.NotSame(PresentationSource.FromVisual(window), PresentationSource.FromVisual(toast));
            var overlayHeight = window.ActualHeight;
            await CheckOverlayOpacityAsync(window);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            var undo = Descendants<Button>(toast).Single(x => x.IsVisible && Equals(x.Content, "Hoàn tác"));
            Assert.True(undo.Focusable && undo.IsEnabled);
            Assert.NotEmpty(UIElementAutomationPeer.CreatePeerForElement(undo)!.GetName());
            window.Activate();
            toast.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window)!, Environment.TickCount, Key.Tab)
                { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            Keyboard.Focus(undo);
            Assert.Equal(undo, Keyboard.FocusedElement);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            AssertNativeAlpha(window, 255);
            var popupBounds = ScreenBounds(toast);
            var overlayBounds = WindowBounds(window);
            Assert.False(popupBounds.IntersectsWith(overlayBounds),
                $"Popup {popupBounds}, Overlay client {overlayBounds}, outer {window.Left},{window.Top},{window.ActualWidth},{window.ActualHeight}; render {window.RenderSize}");
            SaveImage(window, "m09-overlay.png");
            SaveImage(toast, "overlay-completion-popup.png");
            var oldLeft = window.Left;
            window.Left -= 100;
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            var movedBy = overlayBounds.Left - WindowBounds(window).Left;
            Assert.InRange(popupBounds.Left - ScreenBounds(toast).Left, movedBy - 1, movedBy + 1);
            window.Left = oldLeft;
            var oldTop = window.Top;
            window.Top = WindowModeController.GetWorkArea(window).Bottom - window.ActualHeight;
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Assert.True(ScreenBounds(toast).Bottom <= WindowBounds(window).Top,
                $"Popup {ScreenBounds(toast)}, Overlay {WindowBounds(window)}");
            window.Top = oldTop;
            var oldMinHeight = window.MinHeight;
            var area = WindowModeController.GetWorkArea(window);
            window.MinHeight = area.Height - 100;
            window.Top = area.Top + 50;
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Assert.False(ScreenBounds(toast).IntersectsWith(WindowBounds(window)),
                $"Popup must use a side when vertical space is insufficient: {ScreenBounds(toast)}, owner {WindowBounds(window)}");
            window.MinHeight = oldMinHeight;
            window.Top = oldTop;
            shell.HideToTray();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Assert.False(popup.IsOpen);
            shell.ShowOverlayCommand.Execute(OverlayMode.Todo);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Assert.True(popup.IsOpen);
            var feedback = Assert.Single(today.Feedback);
            await today.UndoAsync(feedback);
            await today.UndoAsync(feedback);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Assert.False(popup.IsOpen);
            Assert.Equal(0, profile.Coins);
            Assert.Equal(2, await db.RewardTransactions.CountAsync());
            Assert.Equal(2, today.Items.Count);
            Assert.All(today.Items, x => Assert.False(x.IsCompleting));

            settings.ReducedMotionEnabled = true;
            await today.CompleteAsync(today.Items[0]);
            Assert.Single(today.Items);
            Assert.Single(today.Feedback);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Assert.True(popup.IsOpen);
            Assert.True(window.ActualHeight <= overlayHeight);
            shell.ShowDashboardCommand.Execute(null);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Assert.False(popup.IsOpen);
            Assert.False(window.Topmost);
            AssertNativeAlpha(window, 255);
            var compactButton = Assert.Single(Descendants<Button>(window), button => Equals(button.Content, "Thu gọn thành Overlay"));
            Assert.Equal(OverlayMode.Pomodoro, compactButton.CommandParameter);
            Assert.DoesNotContain(Descendants<Button>(window), button => Equals(button.Content, "Thoát") || Equals(button.Content, "Mở đồng hồ"));
            Assert.DoesNotContain(Descendants<TextBlock>(window), text => text.IsVisible && text.Text is "Sẵn sàng" or "Tập trung" or "Đang chạy");
            Assert.All(scenes, scene => Assert.False(scene.IsAnimating));
            Assert.DoesNotContain(Descendants<Button>(window), button => Equals(button.Content, "Hồ sơ"));
            foreach (var page in new[] { "Today", "Calendar", "Pomodoro", "Settings" })
            {
                dashboard.NavigateCommand.Execute(page);
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                if (page == "Pomodoro")
                {
                    var advanced = Assert.Single(Descendants<Expander>(window));
                    Assert.False(advanced.IsExpanded);
                    Assert.DoesNotContain(Descendants<TextBox>(advanced), box => box.IsVisible);
                    var preset = Descendants<Button>(window).Single(button => Equals(button.Content, "45/10"));
                    preset.Command.Execute(preset.CommandParameter);
                    Assert.Equal(45, pomodoroVm.FocusMinutes);
                    Assert.Equal("Đang chọn", AutomationProperties.GetItemStatus(preset));
                    var unselected = Descendants<Button>(window).Single(button => Equals(button.Content, "25/5"));
                    Assert.NotEqual(unselected.Background, preset.Background);
                    Assert.Equal(new Thickness(3), preset.BorderThickness);
                    Assert.DoesNotContain(Descendants<TextBlock>(window), text => text.Text.StartsWith("Chọn nhịp") || text.Text.StartsWith("Đang chọn:"));
                    SaveImage(window, "pomodoro-presets.png");
                    advanced.IsExpanded = true;
                    await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                    Assert.Equal(4, Descendants<TextBox>(advanced).Count(box => box.IsVisible));
                    SaveImage(window, "pomodoro-advanced.png");
                    pomodoroVm.SelectPresetCommand.Execute("25/5");
                }
                foreach (var control in Descendants<Control>(window).Where(x => x.IsVisible && x.Focusable
                    && (x.GetType() == typeof(TextBox) || x is DatePicker or Slider or CheckBox or DataGrid or ListBox || x is Button { Content: not null })))
                    Assert.False(string.IsNullOrWhiteSpace(UIElementAutomationPeer.CreatePeerForElement(control)?.GetName()), $"Missing name: {page}/{control.GetType().Name}");
            }
            dashboard.NavigateCommand.Execute("Today");
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            foreach (var scale in new[] { 1d, 1.5, 2d }) SaveImage(window, $"m11-dashboard-{scale * 100:0}.png", scale);
            settings.ReducedMotionEnabled = false;
            Assert.All(scenes, scene => Assert.True(scene.IsAnimating));
            settings.AnimationsEnabled = false;
            Assert.All(scenes, scene => Assert.False(scene.IsAnimating));
            settings.AnimationsEnabled = true;
            shell.HideToTray();
            Assert.All(scenes, scene => Assert.False(scene.IsAnimating));
            shell.ShowDashboardCommand.Execute(null);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Assert.All(scenes, scene => Assert.True(scene.IsAnimating));
            var navigation = Descendants<Button>(window).Single(button => button.IsVisible && Equals(button.Content, "Hôm nay"));
            Keyboard.Focus(navigation);
            Assert.True(navigation.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)));
            Assert.Equal("Lịch sử", Assert.IsType<Button>(Keyboard.FocusedElement).Content);
            var mascotScene = scenes.Single(scene => scene.MascotOnly);
            shell.ShowOverlayCommand.Execute(OverlayMode.Pomodoro);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            AssertPomodoroActions(window, "Bắt đầu");
            clock.Advance(5001);
            today.RefreshFeedback();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            SaveImage(window, "compact-pomodoro-ready.png");
            var overlayFrame = (FrameworkElement)window.FindName("OverlayFrame");
            Assert.True(window.ActualHeight < 320, $"Ready height {window.ActualHeight}; frame {overlayFrame.ActualHeight}/{overlayFrame.DesiredSize.Height}; sizing {window.SizeToContent}");
            await shell.PomodoroOverlay!.StartAsync();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            AssertPomodoroActions(window, "Tạm dừng", "Bỏ qua");
            SaveImage(window, "compact-pomodoro-running.png");
            Assert.Equal("focus", mascotScene.MascotState);
            await shell.PomodoroOverlay.PauseAsync();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            AssertPomodoroActions(window, "Tiếp tục", "Dừng");
            SaveImage(window, "compact-pomodoro-paused.png");
            Assert.Equal("idle", mascotScene.MascotState);
            await shell.PomodoroOverlay.ResumeAsync();
            clock.Advance(25 * 60 * 1000);
            await shell.PomodoroOverlay.RefreshAsync();
            Assert.True(shell.PomodoroOverlay.IsAwaiting);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            AssertPomodoroActions(window, "Bắt đầu");
            await shell.PomodoroOverlay.StartAsync();
            Assert.Equal("rest", mascotScene.MascotState);
            await shell.PomodoroOverlay.ConfirmStopAsync();
            Assert.Equal("idle", mascotScene.MascotState);
            shell.ShowOverlayCommand.Execute(OverlayMode.Todo);
            await today.CompleteAsync(Assert.Single(today.Items, row => !row.IsCompleting));
            clock.Advance(5001);
            today.RefreshFeedback();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Assert.True(today.IsEmpty);
            Assert.True(window.ActualHeight < 260);
            Assert.True(WindowModeController.GetWorkArea(window).Contains(new Rect(window.Left, window.Top, window.ActualWidth, window.ActualHeight)));
            SaveImage(window, "compact-todo-empty.png");
            shell.ShowDashboardCommand.Execute(null);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            var longTitle = string.Concat(Enumerable.Repeat("Nhiệm vụ dài cần xuống dòng ", 6)).Trim();
            await service.CreateAsync(new CreateTodoCommand(longTitle, clock.LocalToday, clock.LocalToday), CancellationToken.None);
            for (var i = 0; i < 35; i++)
                await service.CreateAsync(new CreateTodoCommand($"Nhiệm vụ viewport {i}", clock.LocalToday, clock.LocalToday), CancellationToken.None);
            await today.LoadAsync();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            var taskList = Descendants<ListBox>(window).Single(list => list.IsVisible && AutomationProperties.GetName(list) == "Nhiệm vụ hôm nay");
            var longText = Descendants<TextBlock>(taskList).Single(text => text.Text == longTitle);
            Assert.True(longText.ActualWidth < 600, $"Long title escaped viewport: {longText.ActualWidth}");
            Assert.True(longText.ActualHeight > 30, "Long title should wrap onto multiple lines.");
            var listScroll = Descendants<ScrollViewer>(taskList).First();
            Assert.True(listScroll.ScrollableHeight > 0, "Task list must retain its own finite viewport.");
            shell.ShowOverlayCommand.Execute(OverlayMode.Todo);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Assert.True(window.ActualHeight < 360);
            SaveImage(window, "compact-todo-list.png");
            shell.ShowDashboardCommand.Execute(null);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            using var process = Process.GetCurrentProcess();
            var cpuStart = process.TotalProcessorTime;
            var elapsed = Stopwatch.StartNew();
            await Task.Delay(1100);
            elapsed.Stop();
            var cpu = process.TotalProcessorTime - cpuStart;
            settings.ReducedMotionEnabled = true;
            var stoppedCpuStart = process.TotalProcessorTime;
            await Task.Delay(1100);
            var stoppedCpu = process.TotalProcessorTime - stoppedCpuStart;
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "m11-runtime.txt"),
                $"Animation observation: {elapsed.Elapsed.TotalMilliseconds:F0} ms wall, {cpu.TotalMilliseconds:F0} ms process CPU; 2 scenes at 24 Hz.\n" +
                $"Reduced Motion baseline (1100 ms): {stoppedCpu.TotalMilliseconds:F0} ms process CPU; WPF rendering tier {RenderCapability.Tier >> 16}.\n" +
                "STA window; SQLite in-memory; muted. Reduced Motion/disabled/hidden/Overlay stop decorative timers.\n" +
                "RenderTargetBitmap 96/144/192 DPI is simulated; physical monitor DPI, Narrator and audible quality remain manual.\n");
            clock.Advance(5001);
            today.RefreshFeedback();
            Assert.Empty(today.Feedback);
            shell.ShowOverlayCommand.Execute(OverlayMode.Pomodoro);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            await CheckDesktopTransparencyAsync(window);
            listener.Flush();
            Assert.True(string.IsNullOrWhiteSpace(bindingLog.ToString()), bindingLog.ToString());
        }
        finally
        {
            await shell.ExitAsync();
            Assert.False(window.IsVisible);
            PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);
            app.Shutdown();
        }
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T value) yield return value;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }

    private static void AssertPomodoroActions(Window window, params string[] expected)
    {
        var actions = new[] { "Bắt đầu", "Tạm dừng", "Tiếp tục", "Bỏ qua", "Dừng" };
        var visible = Descendants<Button>(window).Where(button => button.IsVisible && button.Content is string text && actions.Contains(text)).ToArray();
        Assert.Equal(expected, visible.Select(button => (string)button.Content));
        Assert.All(visible, button => Assert.True(button.IsEnabled));
        foreach (var feedbackView in Descendants<CompletionFeedbackView>(window).Where(view => view.IsVisible))
        {
            var bounds = feedbackView.TransformToAncestor(window).TransformBounds(new Rect(feedbackView.RenderSize));
            Assert.All(visible, button => Assert.False(bounds.IntersectsWith(button.TransformToAncestor(window).TransformBounds(new Rect(button.RenderSize)))));
        }
        Assert.True(window.ActualWidth <= 280 && window.ActualHeight < 240,
            $"Compact overlay size {window.ActualWidth}x{window.ActualHeight}; frame {((FrameworkElement)window.FindName("OverlayFrame")).ActualHeight}; min {window.MinHeight}; sizing {window.SizeToContent}");
        var clockText = Descendants<TextBlock>(window).Single(text => text.IsVisible && text.FontSize == 30);
        var clockPeer = UIElementAutomationPeer.CreatePeerForElement(clockText)!;
        Assert.Contains(clockText.Text, clockPeer.GetName());
        Assert.DoesNotContain(Descendants<TextBlock>(window), text => text.IsVisible && text.Text is "Sẵn sàng" or "Tập trung" or "Đang chạy" or "Đã tạm dừng");
        if (visible.Length == 2)
            Assert.Equal(visible[0].TranslatePoint(new Point(), window).Y, visible[1].TranslatePoint(new Point(), window).Y);
    }

    private static async Task CheckOverlayOpacityAsync(Window window)
    {
        Assert.True(GetCursorPos(out var original));
        try
        {
            window.Activate();
            Keyboard.ClearFocus();
            var inside = window.PointToScreen(new Point(16, 16));
            Assert.True(SetCursorPos((int)inside.X, (int)inside.Y));
            await Task.Delay(75);
            Assert.True(window.IsMouseOver);
            AssertNativeAlpha(window, 255);
            Descendants<Button>(window).First(button => button.IsVisible).Focus();
            Assert.True(SetCursorPos((int)SystemParameters.WorkArea.Left + 1, (int)SystemParameters.WorkArea.Bottom - 1));
            await Task.Delay(75);
            Assert.False(window.IsMouseOver);
            AssertNativeAlpha(window, 128);
            var caption = window.PointToScreen(new Point(window.ActualWidth / 2, -15));
            Assert.True(SetCursorPos((int)caption.X, (int)caption.Y));
            await Task.Delay(75);
            AssertNativeAlpha(window, 255);
            Assert.True(SetCursorPos((int)SystemParameters.WorkArea.Left + 1, (int)SystemParameters.WorkArea.Bottom - 1));
            await Task.Delay(75);
            AssertNativeAlpha(window, 128);
        }
        finally { SetCursorPos(original.X, original.Y); }
    }

    private static async Task CheckDesktopTransparencyAsync(Window window)
    {
        Assert.True(GetCursorPos(out var original));
        try
        {
            window.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                { RoutedEvent = Mouse.PreviewMouseDownEvent });
            Keyboard.ClearFocus();
            Assert.True(SetCursorPos((int)SystemParameters.WorkArea.Left + 1, (int)SystemParameters.WorkArea.Bottom - 1));
            await Task.Delay(75);
            var backdrop = new Window
            {
                Left = window.Left, Top = window.Top, Width = window.ActualWidth, Height = window.ActualHeight,
                WindowStyle = WindowStyle.None, ShowInTaskbar = false, Background = Brushes.Red, Topmost = true,
            };
            var previousOwner = window.Owner;
            try
            {
                backdrop.Show();
                window.Owner = backdrop;
                window.Activate();
                await Task.Delay(100);
                var sample = window.PointToScreen(new Point(30, 60));
                AssertNativeAlpha(window, 128);
                Assert.True(WindowBounds(backdrop).Contains(sample), $"Backdrop {WindowBounds(backdrop)}, sample {sample}");
                var red = CapturePixel(sample);
                backdrop.Background = Brushes.Blue;
                await Task.Delay(100);
                AssertNativeAlpha(window, 128);
                var blue = CapturePixel(sample);
                Assert.True(red.R - blue.R >= 70 && blue.B - red.B >= 70,
                    $"Desktop behind the overlay must remain visible: red backdrop {red}, blue backdrop {blue}");
                var bounds = WindowBounds(window);
                using var bitmap = new System.Drawing.Bitmap((int)bounds.Width, (int)bounds.Height);
                using var graphics = System.Drawing.Graphics.FromImage(bitmap);
                graphics.CopyFromScreen((int)bounds.Left, (int)bounds.Top, 0, 0, bitmap.Size);
                bitmap.Save(Path.Combine(AppContext.BaseDirectory, "overlay-native-transparent-50.png"));
            }
            finally { window.Owner = previousOwner; backdrop.Close(); window.Activate(); }
        }
        finally { SetCursorPos(original.X, original.Y); }
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out CursorPoint point);

    private static System.Drawing.Color CapturePixel(Point point)
    {
        using var bitmap = new System.Drawing.Bitmap(1, 1);
        using var graphics = System.Drawing.Graphics.FromImage(bitmap);
        graphics.CopyFromScreen((int)point.X, (int)point.Y, 0, 0, bitmap.Size);
        return bitmap.GetPixel(0, 0);
    }

    private static void AssertNativeAlpha(Window window, byte expected)
    {
        Assert.True(GetLayeredWindowAttributes(new System.Windows.Interop.WindowInteropHelper(window).Handle,
            out _, out var alpha, out var flags));
        Assert.Equal(2u, flags & 2);
        Assert.Equal(expected, alpha);
        Assert.Equal(1, window.Opacity);
    }

    [DllImport("user32.dll")]
    private static extern bool GetLayeredWindowAttributes(IntPtr handle, out uint color, out byte alpha, out uint flags);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    private struct CursorPoint { public int X; public int Y; }

    private static Rect ScreenBounds(FrameworkElement element) => new(
        element.PointToScreen(new Point()), element.PointToScreen(new Point(element.ActualWidth, element.ActualHeight)));

    private static Rect WindowBounds(Window window)
    {
        Assert.True(GetWindowRect(new System.Windows.Interop.WindowInteropHelper(window).Handle, out var bounds));
        return new Rect(new Point(bounds.Left, bounds.Top), new Point(bounds.Right, bounds.Bottom));
    }

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr handle, out NativeRect bounds);

    private struct NativeRect { public int Left; public int Top; public int Right; public int Bottom; }

    private static void SaveImage(FrameworkElement window, string name, double scale = 1)
    {
        window.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)(window.ActualWidth * scale), (int)(window.ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(AppContext.BaseDirectory, name));
        encoder.Save(stream);
    }

    private sealed class Clock : IClock
    {
        public DateTimeOffset UtcNow { get; private set; } = new(2026, 11, 16, 8, 0, 0, TimeSpan.Zero);
        public DateOnly LocalToday => new(2026, 11, 16);
        public void Advance(int milliseconds) => UtcNow += TimeSpan.FromMilliseconds(milliseconds);
    }
}

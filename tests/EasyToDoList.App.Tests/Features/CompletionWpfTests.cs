using System.Diagnostics;
using System.IO;
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
    public async Task Window_renders_committed_feedback_switches_modes_and_undo_reverses_sqlite_once()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
            Dispatcher.CurrentDispatcher.InvokeAsync(async () =>
            {
                try { await CheckWindowAsync(); completion.SetResult(); }
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

    private static async Task CheckWindowAsync()
    {
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
        var profile = new ProfileViewModel(new SqliteProfileQuery(db), new SqliteRewardHistoryQuery(db));
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
            var first = today.Items.Single(x => x.Title.StartsWith("Ôn bài"));
            await today.CompleteAsync(first);
            Assert.Equal(6, profile.Coins);
            Assert.Equal(13, Assert.Single(today.Feedback).Result.EarlyBonusExperience);
            clock.Advance(240);
            today.RefreshFeedback();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            SaveImage(window, "m09-dashboard.png");
            Assert.Contains(Descendants<CompletionSurface>(window), x => x.Progress > 0);
            var mascot = Descendants<Image>(window).Single(x => AutomationProperties.GetName(x).StartsWith("Hiệp sĩ"));
            Assert.NotNull(mascot.Source);

            shell.ShowOverlayCommand.Execute(OverlayMode.Todo);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Assert.True(window.Topmost);
            var undo = Descendants<Button>(window).Single(x => x.IsVisible && Equals(x.Content, "Hoàn tác"));
            Assert.True(undo.Focusable && undo.IsEnabled);
            Assert.NotEmpty(UIElementAutomationPeer.CreatePeerForElement(undo)!.GetName());
            window.Activate();
            Keyboard.Focus(undo);
            Assert.Equal(undo, Keyboard.FocusedElement);
            SaveImage(window, "m09-overlay.png");
            var feedback = Assert.Single(today.Feedback);
            await today.UndoAsync(feedback);
            await today.UndoAsync(feedback);
            Assert.Equal(0, profile.Coins);
            Assert.Equal(2, await db.RewardTransactions.CountAsync());
            Assert.Equal(2, today.Items.Count);
            Assert.All(today.Items, x => Assert.False(x.IsCompleting));

            settings.ReducedMotionEnabled = true;
            await today.CompleteAsync(today.Items[0]);
            Assert.Single(today.Items);
            Assert.Single(today.Feedback);
            shell.ShowDashboardCommand.Execute(null);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Assert.False(window.Topmost);
            clock.Advance(5001);
            today.RefreshFeedback();
            Assert.Empty(today.Feedback);
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

    private static void SaveImage(Window window, string name)
    {
        window.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
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

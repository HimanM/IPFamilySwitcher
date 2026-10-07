using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using IPFamilySwitcher.Models;
using IPFamilySwitcher.Services;
using IPFamilySwitcher.ViewModels;

namespace IPFamilySwitcher.Tests;

public sealed class PresentationTests
{
    [Fact]
    public async Task SwitchingSelectionDuringApplyKeepsTheRequestedMode()
    {
        var directory = Path.Combine(Path.GetTempPath(), "IPFamilySwitcherTests", Guid.NewGuid().ToString("N"));
        var firewall = new DelayedFirewall();
        try
        {
            var configuration = new ConfigurationService(directory);
            var vm = new MainViewModel(configuration, firewall, new RuleReconciliationService(firewall), new ExecutableService());
            await vm.AddAsync(Environment.ProcessPath!);
            var application = vm.SelectedApplication!;
            var operation = vm.ChangeModeAsync(application, NetworkMode.IPv6Only);
            vm.SelectedApplication = null;
            Assert.Equal(NetworkMode.Default, application.PendingMode);
            firewall.Complete.SetResult();
            await operation;
            Assert.Equal(NetworkMode.IPv6Only, application.Mode);
            Assert.Equal(NetworkMode.IPv6Only, (await configuration.LoadAsync()).Applications.Single().Mode);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }

    private sealed class DelayedFirewall : IFirewallService
    {
        public TaskCompletionSource Complete { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task ApplyModeAsync(ManagedApplication application, CancellationToken cancellationToken = default) => Complete.Task;
        public Task RemoveManagedRuleAsync(Guid applicationId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SetRuleEnabledAsync(Guid applicationId, bool enabled, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<FirewallRuleInfo>> GetManagedRulesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<FirewallRuleInfo>>([]);
        public Task RemoveAllManagedRulesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    [Fact]
    public void ProcessStatusMatchesFullPathAndHandlesAbsentApplication()
    {
        var current = Environment.ProcessPath!;
        var absent = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".exe");
        var wrongPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), Path.GetFileName(current));
        var states = ProcessStatusService.Inspect([current, absent, wrongPath]);
        Assert.Equal("Running", states[current]);
        Assert.Equal("Not running", states[absent]);
        Assert.NotEqual("Running", states[wrongPath]);
    }

    [Fact]
    public void DisabledRuleDoesNotCountAsActive()
    {
        var model = new ManagedApplication { DisplayName = "Example", ExecutablePath = Environment.ProcessPath!, Mode = NetworkMode.IPv4Only, Enabled = false };
        var row = new ApplicationViewModel(model);
        row.UpdateStatus(new(model.Id, FirewallRuleState.Correct, "Disabled.", []));
        Assert.Equal("Disabled", row.Status);
        Assert.Equal(0, row.ActiveRuleCount);
        Assert.Equal("Enable", row.ToggleLabel);
        Assert.True(Version.TryParse(ReleaseInfo.Current.Version, out _));
        Assert.NotEmpty(ReleaseInfo.Current.ReleaseNotes);
    }

    [Fact]
    public void MainWindowLoadsAndLaysOutAtMinimumSize()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var window = new MainWindow();
                var vm = (MainViewModel)window.DataContext;
                foreach (var (name, mode, enabled, running) in new[] {
                    ("Brave Browser", NetworkMode.IPv4Only, true, "Running"),
                    ("Example application", NetworkMode.IPv6Only, false, "Not running"),
                    ("Windows Terminal", NetworkMode.Default, true, "Running"),
                    ("Music player", NetworkMode.IPv4Only, true, "Running"),
                    ("Code editor", NetworkMode.Default, true, "Not running"),
                    ("Download manager", NetworkMode.IPv6Only, true, "Not running") })
                {
                    var app = new ManagedApplication { DisplayName = name, ExecutablePath = @"C:\Program Files\" + name + @"\application.exe", Mode = mode, Enabled = enabled };
                    var row = new ApplicationViewModel(app);
                    row.UpdateStatus(new(app.Id, FirewallRuleState.Correct, enabled ? "Active." : "Disabled.", mode == NetworkMode.Default ? [] : [new FirewallRuleInfo("Preview", app.Id, app.ExecutablePath, "IPFamilySwitcher.ManagedRules", enabled, 2, 0, "::/1,8000::/1", 256, int.MaxValue)]));
                    row.UpdateRunningStatus(running);
                    vm.Applications.Add(row);
                }
                vm.SelectedApplication = vm.Applications[0];
                window.Width = window.MinWidth;
                window.Height = window.MinHeight;
                var content = (FrameworkElement)window.Content;
                content.DataContext = vm;
                content.Measure(new Size(window.Width, window.Height));
                content.Arrange(new Rect(0, 0, window.Width, window.Height));
                content.UpdateLayout();
                Assert.Equal(window.MinWidth, content.ActualWidth);
                var firstRow = Descendants<System.Windows.Controls.DataGridRow>(content).First();
                Assert.Empty(Descendants<System.Windows.Controls.Button>(firstRow));
                Assert.True(firstRow.IsSelected);
                Assert.All(Descendants<System.Windows.Controls.DataGridCell>(firstRow), cell =>
                    Assert.Equal(Color.FromRgb(37, 41, 35), ((SolidColorBrush)cell.Foreground).Color));
                var actionGrid = (FrameworkElement)window.FindName("RecordActions");
                var actions = Descendants<System.Windows.Controls.Button>(actionGrid).ToArray();
                Assert.Equal(4, actions.Length);
                Assert.Single(actions.Select(button => button.ActualWidth).Distinct());
                Assert.All(actions, button =>
                {
                    Assert.Equal(36, button.ActualHeight);
                    var bounds = button.TransformToAncestor(actionGrid).TransformBounds(new Rect(button.RenderSize));
                    Assert.True(bounds.Top >= 0 && bounds.Bottom <= actionGrid.ActualHeight);
                    var surface = (FrameworkElement)button.Template.FindName("Surface", button);
                    Assert.Equal(button.ActualHeight, surface.ActualHeight);
                    Assert.Same(vm.SelectedApplication, button.CommandParameter);
                });
                vm.SelectedApplication.PendingMode = NetworkMode.IPv6Only;
                vm.SelectedApplication = vm.Applications[1];
                Assert.Equal(vm.Applications[0].Mode, vm.Applications[0].PendingMode);
                content.UpdateLayout();
                Assert.All(actions, button => Assert.Same(vm.SelectedApplication, button.CommandParameter));
                vm.SearchText = "no-such-application";
                Assert.Null(vm.SelectedApplication);
                content.UpdateLayout();
                Assert.Equal(Visibility.Collapsed, ((FrameworkElement)window.FindName("SelectedActions")).Visibility);
                vm.SearchText = string.Empty;
                vm.SelectedApplication = vm.Applications[0];
                content.UpdateLayout();
                var font = (FontFamily)window.Resources["DisplayFont"];
                Assert.True(new Typeface(font, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal).TryGetGlyphTypeface(out var glyphs));
                Assert.Contains("Rajdhani", glyphs.FontUri.ToString(), StringComparison.OrdinalIgnoreCase);
                var panel = (FrameworkElement)window.FindName("ErrorPanel");
                var table = (FrameworkElement)window.FindName("ApplicationsGrid");
                var originalTableHeight = table.ActualHeight;
                foreach (var message in new[] { "Short error", string.Join('\n', Enumerable.Repeat(new string('X', 5000), 100)), string.Empty })
                {
                    vm.SetError(message);
                    content.Measure(new Size(window.Width, window.Height));
                    content.Arrange(new Rect(0, 0, window.Width, window.Height));
                    content.UpdateLayout();
                    Assert.Equal(78, panel.ActualHeight);
                    Assert.Equal(originalTableHeight, table.ActualHeight);
                    Assert.True(panel.ActualWidth <= content.ActualWidth);
                    Assert.Equal(message, ((System.Windows.Controls.TextBox)window.FindName("ErrorText")).Text);
                }
                // Export a representative desktop size after checking the minimum layout.
                window.Width = 1180;
                window.Height = 800;
                content.Measure(new Size(window.Width, window.Height));
                content.Arrange(new Rect(0, 0, window.Width, window.Height));
                content.UpdateLayout();
                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
                content.UpdateLayout();
                var tableViewport = Descendants<System.Windows.Controls.ScrollContentPresenter>(table).First();
                Assert.True(tableViewport.ActualHeight >= 5 * 56, $"Only {tableViewport.ActualHeight / 56} rows fit");
                var screenshot = Environment.GetEnvironmentVariable("IPFS_PREVIEW_PATH");
                if (!string.IsNullOrWhiteSpace(screenshot))
                {
                    var bitmap = new RenderTargetBitmap((int)window.Width, (int)window.Height, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(content);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var output = File.Create(screenshot);
                    encoder.Save(output);
                    vm.SetError("Illustrative error: Windows denied the firewall operation.\n" +
                        string.Join('\n', Enumerable.Repeat("Additional diagnostic details remain scrollable and can be copied in full.", 20)));
                    content.UpdateLayout();
                    var errorBitmap = new RenderTargetBitmap((int)window.Width, (int)window.Height, 96, 96, PixelFormats.Pbgra32);
                    errorBitmap.Render(content);
                    var errorEncoder = new PngBitmapEncoder();
                    errorEncoder.Frames.Add(BitmapFrame.Create(errorBitmap));
                    using var errorOutput = File.Create(Path.Combine(Path.GetDirectoryName(screenshot)!, "interface-error.png"));
                    errorEncoder.Save(errorOutput);
                }
                window.Close();
            }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        Assert.Null(failure);
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }

}

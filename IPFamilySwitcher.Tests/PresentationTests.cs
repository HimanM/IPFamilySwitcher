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
                    ("Windows Terminal", NetworkMode.Default, true, "Running") })
                {
                    var app = new ManagedApplication { DisplayName = name, ExecutablePath = @"C:\Program Files\" + name + @"\application.exe", Mode = mode, Enabled = enabled };
                    var row = new ApplicationViewModel(app);
                    row.UpdateStatus(new(app.Id, FirewallRuleState.Correct, enabled ? "Active." : "Disabled.", mode == NetworkMode.Default ? [] : [new FirewallRuleInfo("Preview", app.Id, app.ExecutablePath, "IPFamilySwitcher.ManagedRules", enabled, 2, 0, "::/1,8000::/1", 256, int.MaxValue)]));
                    row.UpdateRunningStatus(running);
                    vm.Applications.Add(row);
                }
                window.Width = window.MinWidth;
                window.Height = window.MinHeight;
                var content = (FrameworkElement)window.Content;
                content.DataContext = vm;
                content.Measure(new Size(window.Width, window.Height));
                content.Arrange(new Rect(0, 0, window.Width, window.Height));
                content.UpdateLayout();
                Assert.Equal(window.MinWidth, content.ActualWidth);
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
                window.Height = 820;
                content.Measure(new Size(window.Width, window.Height));
                content.Arrange(new Rect(0, 0, window.Width, window.Height));
                content.UpdateLayout();
                var screenshot = Environment.GetEnvironmentVariable("IPFS_PREVIEW_PATH");
                if (!string.IsNullOrWhiteSpace(screenshot))
                {
                    var bitmap = new RenderTargetBitmap((int)window.Width, (int)window.Height, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(content);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var output = File.Create(screenshot);
                    encoder.Save(output);
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
}

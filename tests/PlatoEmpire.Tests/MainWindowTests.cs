using System.Windows;

namespace PlatoEmpire.Tests;

/// <summary>ISS-007: the main window starts maximized and its normal size always fits the screen.</summary>
public class MainWindowTests
{
    [Fact]
    public void Main_window_starts_maximized() =>
        OnSta(() => Assert.Equal(WindowState.Maximized, new MainWindow().WindowState));

    [Fact]
    public void Normal_size_shrinks_to_fit_a_small_screen() => OnSta(() =>
    {
        var window = DesignedSize();
        MainWindow.FitTo(window, new Rect(0, 0, 1280, 680));

        Assert.True(window.Width <= 1280 && window.Height <= 680, $"size {window.Width}x{window.Height}");
        Assert.True(window.MinWidth <= 1280 && window.MinHeight <= 680, $"minimum {window.MinWidth}x{window.MinHeight}");
    });

    [Fact]
    public void Restored_window_sits_inside_the_work_area() => OnSta(() =>
    {
        var area = new Rect(0, 0, 1536, 816);   // 1920x1080 at 125 %, taskbar excluded
        var window = DesignedSize();
        MainWindow.FitTo(window, area);

        Assert.True(area.Contains(new Rect(window.Left, window.Top, window.Width, window.Height)),
            $"window at {window.Left},{window.Top} size {window.Width}x{window.Height}");
    });

    [Fact]
    public void Normal_size_is_kept_on_a_large_screen() => OnSta(() =>
    {
        var window = DesignedSize();
        MainWindow.FitTo(window, new Rect(0, 0, 2560, 1400));

        Assert.Equal((1440.0, 920.0), (window.Width, window.Height));
        Assert.Equal((1100.0, 720.0), (window.MinWidth, window.MinHeight));
    });

    /// <summary>A window with MainWindow.xaml's sizes, before they are fitted to this PC's screen.</summary>
    static Window DesignedSize() => new() { Width = 1440, Height = 920, MinWidth = 1100, MinHeight = 720 };

    /// <summary>WPF windows need an STA thread, and the window's styles come from the app's resources.</summary>
    static void OnSta(Action test)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                lock (AppLock)
                {
                    if (Application.Current is null) new App().InitializeComponent();
                }
                test();
            }
            catch (Exception e) { failure = e; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    static readonly object AppLock = new();
}

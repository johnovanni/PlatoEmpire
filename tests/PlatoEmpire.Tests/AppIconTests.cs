using System.Collections;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;

namespace PlatoEmpire.Tests;

/// <summary>ISS-006: the app ships with its own icon.</summary>
public class AppIconTests
{
    static readonly Assembly App = typeof(PlatoEmpire.MainWindow).Assembly;

    [Fact]
    public void Window_icon_is_embedded_with_every_size_from_16_to_256()
    {
        using var stream = App.GetManifestResourceStream("PlatoEmpire.g.resources");
        Assert.NotNull(stream);
        using var reader = new ResourceReader(stream);
        var icon = reader.Cast<DictionaryEntry>().SingleOrDefault(e => (string)e.Key == "assets/platoempire.ico");
        var bytes = Assert.IsAssignableFrom<Stream>(icon.Value);

        var decoder = new IconBitmapDecoder(bytes, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        var sizes = decoder.Frames.Select(f => f.PixelWidth).ToHashSet();

        Assert.Superset(new HashSet<int> { 16, 24, 32, 48, 64, 128, 256 }, sizes);
    }

    [Fact]
    public void Exe_has_an_icon()
    {
        var exe = Path.Combine(Path.GetDirectoryName(App.Location)!, "PlatoEmpire.exe");
        Assert.True(File.Exists(exe), $"{exe} not found");

        Assert.True(ExtractIconEx(exe, -1, null, null, 0) > 0, "PlatoEmpire.exe has no icon resource");
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern uint ExtractIconEx(string file, int index, IntPtr[]? large, IntPtr[]? small, uint count);
}

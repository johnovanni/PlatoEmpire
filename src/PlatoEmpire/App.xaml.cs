using System.IO;
using System.Windows;

namespace PlatoEmpire;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // PlatoEmpire.exe --selftest [report.txt]  runs bot-only games headlessly and writes a report.
        if (e.Args.Length > 0 && e.Args[0] == "--selftest")
        {
            File.WriteAllText(e.Args.Length > 1 ? e.Args[1] : "selftest.txt", SelfTest.Run());
            Shutdown();
            return;
        }
        if (e.Args.Length > 1 && e.Args[0] == "--snapshot")
        {
            SelfTest.Snapshot(e.Args[1]);
            Shutdown();
            return;
        }

        new MainWindow().Show();
    }
}

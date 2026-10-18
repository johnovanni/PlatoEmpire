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
        // PlatoEmpire.exe --compare [report.txt]  A/B tests the fleet limit across many bot games.
        if (e.Args.Length > 0 && e.Args[0] == "--compare")
        {
            File.WriteAllText(e.Args.Length > 1 ? e.Args[1] : "compare.txt", SelfTest.Compare());
            Shutdown();
            return;
        }
        // PlatoEmpire.exe --races [report.txt]  compares how the four races fare across many even-skill bot games.
        if (e.Args.Length > 0 && e.Args[0] == "--races")
        {
            File.WriteAllText(e.Args.Length > 1 ? e.Args[1] : "races.txt", SelfTest.RaceBalance());
            Shutdown();
            return;
        }
        // PlatoEmpire.exe --difficulty [report.txt]  one-on-one games against each difficulty level.
        if (e.Args.Length > 0 && e.Args[0] == "--difficulty")
        {
            File.WriteAllText(e.Args.Length > 1 ? e.Args[1] : "difficulty.txt", SelfTest.DifficultyLadder());
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

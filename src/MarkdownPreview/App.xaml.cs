using System.Windows;

namespace MarkdownPreview;

public partial class App : Application
{
    /// <summary>Starts an independent document window, optionally opening a command-line path.</summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var window = new MainWindow();
        window.Show();
        if (e.Args.Contains("--smoke-test")) window.RunSmokeTest();
        else if (e.Args.Length > 0) window.OpenPath(e.Args[0]);
    }
}

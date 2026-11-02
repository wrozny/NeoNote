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
        if (e.Args.Contains("--smoke-test"))
        {
            var outputIndex = Array.IndexOf(e.Args, "--smoke-output");
            window.RunSmokeTest(outputIndex >= 0 && outputIndex + 1 < e.Args.Length ? e.Args[outputIndex + 1] : null);
        }
        else if (e.Args.Length > 0) window.OpenPath(e.Args[0]);
    }
}

using System.Windows;

namespace ModernNotepad;

public partial class App : Application
{
    private string? smokeReportPath;

    /// <summary>Starts an independent document window, optionally opening a command-line path.</summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var smoke = e.Args.Contains("--smoke-test");
        if (smoke)
        {
            var outputIndex = Array.IndexOf(e.Args, "--smoke-output");
            smokeReportPath = outputIndex >= 0 && outputIndex + 1 < e.Args.Length ? e.Args[outputIndex + 1] : System.IO.Path.Combine(AppContext.BaseDirectory, "smoke-result.txt");
            DispatcherUnhandledException += OnUnhandledSmokeException;
        }
        try
        {
            var window = new MainWindow();
            window.Show();
            if (smoke) window.RunSmokeTest(smokeReportPath);
            else if (e.Args.Length > 0) window.OpenPath(e.Args[0]);
        }
        catch (Exception error) when (smoke) { WriteSmokeFailure(error); }
    }

    /// <summary>Captures asynchronous startup exceptions during automated Windows validation.</summary>
    private void OnUnhandledSmokeException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        WriteSmokeFailure(e.Exception);
    }

    /// <summary>Records an application-startup failure before terminating a test process without modal prompts.</summary>
    private void WriteSmokeFailure(Exception error)
    {
        System.IO.File.WriteAllText(smokeReportPath!, error.ToString());
        Environment.Exit(1);
    }
}

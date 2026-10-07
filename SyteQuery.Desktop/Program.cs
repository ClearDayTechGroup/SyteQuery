using Velopack;

namespace SyteQuery.Desktop;

/// <summary>
/// Custom entry point (see StartupObject in the csproj). Velopack's installer calls the exe with
/// special arguments during install, update and uninstall; <c>VelopackApp.Run()</c> must be the first
/// thing that happens so it can handle those and exit before any UI or hosting code starts. For a
/// normal launch it returns immediately and the WPF application starts as usual.
/// </summary>
public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build().Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}

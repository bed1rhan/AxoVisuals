using Microsoft.UI.Xaml;
namespace AxoVisuals;
public partial class App : Application
{
    private Window? window;
    public App()
    {
        Core.Imaging.ImagingRuntime.Initialize();
        UnhandledException += (_, e) => Core.Infrastructure.Diagnostics.Record("unhandled",e.Exception);
        InitializeComponent();
    }
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try { window = new MainWindow(); window.Activate(); }
        catch(Exception ex) { Core.Infrastructure.Diagnostics.Record("startup",ex); throw; }
    }
}


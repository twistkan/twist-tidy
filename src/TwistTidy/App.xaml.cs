using System.Windows;
using System.Windows.Threading;
namespace TwistTidy;
public partial class App : Application
{
    private System.Threading.Mutex? instance;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if(e.Args.Length==2&&e.Args[0]=="--package-check"){Views.SetupWindow.VerifyPackage(Environment.ProcessPath!,e.Args[1]);Shutdown();return;}
        if(e.Args.Length==2&&e.Args[0]=="--export-icon"){Assets.IconExporter.Export(e.Args[1],(System.Windows.Media.ImageSource)FindResource("Logo"));Shutdown();return;}
        if(Path.GetFileNameWithoutExtension(Environment.ProcessPath??"").Contains("Setup",StringComparison.OrdinalIgnoreCase))
        {var setup=new Views.SetupWindow();MainWindow=setup;setup.Show();return;}
        instance=new System.Threading.Mutex(true,@"Local\Tweest.TwistTidy",out bool created);
        if(!created){MessageBox.Show("TwistTidy is already open.","TwistTidy");Shutdown();return;}
        DispatcherUnhandledException+=(s,a)=>{MessageBox.Show(a.Exception.Message,"TwistTidy",MessageBoxButton.OK,MessageBoxImage.Warning);a.Handled=true;};
        var splash=new Views.Splash();MainWindow=splash;splash.Show();await Task.Delay(650);
        var window=new Views.MainWindow();MainWindow=window;window.Show();splash.Close();
        if(e.Args.Length==2&&e.Args[0]=="--visual-check"){await window.RenderScreens(e.Args[1]);Shutdown();}
    }
    protected override void OnExit(ExitEventArgs e){instance?.Dispose();base.OnExit(e);}
}

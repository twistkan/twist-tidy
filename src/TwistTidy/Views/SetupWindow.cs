using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TwistTidy.Core.Services;

namespace TwistTidy.Views;
public class SetupWindow : Window
{
    private readonly string destination=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","TwistTidy");
    private readonly TextBlock status=new(){Text="Ready to install. No administrator rights or internet connection needed.",TextWrapping=TextWrapping.Wrap,Margin=new(0,14,0,14)};
    private readonly CheckBox desktop=new(){Content="Create a desktop shortcut",IsChecked=true};
    public SetupWindow()
    {
        Style=(Style)FindResource(typeof(Window));Title="TwistTidy Setup | Powered by Tweest";Width=640;Height=500;ResizeMode=ResizeMode.NoResize;WindowStartupLocation=WindowStartupLocation.CenterScreen;Icon=(ImageSource)FindResource("Logo");
        var content=new StackPanel{Margin=new(30)};content.Children.Add(new Image{Source=(ImageSource)FindResource("Logo"),Width=65,Height=65,HorizontalAlignment=HorizontalAlignment.Left});content.Children.Add(new TextBlock{Text="Make room for creativity.",FontSize=28,FontWeight=FontWeights.SemiBold,Margin=new(0,14,0,12)});content.Children.Add(new TextBlock{Text="Install TwistTidy for your Windows account.\nThe app and its runtime are included in this file.",Margin=new(0,0,0,12)});content.Children.Add(new TextBlock{Text=destination,FontSize=12,Margin=new(0,0,0,14)});content.Children.Add(desktop);
        var install=new Button{Content="Install TwistTidy",Style=(Style)FindResource("Primary")};var close=new Button{Content="Close"};close.Click+=(s,e)=>Close();
        install.Click+=async(s,e)=>
        {
            install.IsEnabled=close.IsEnabled=desktop.IsEnabled=false;
            try
            {
                status.Text="Installing the app and verifying its contents...";string exe=Path.Combine(destination,"TwistTidy.exe");
                await Task.Run(()=>
                {
                    string self=Environment.ProcessPath??throw new IOException("Cannot locate the setup executable.");
                    InstallFiles(self,destination);
                });
                string menu=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),"Programs","TwistTidy.lnk");Shortcut(exe,menu);
                if(desktop.IsChecked==true)Shortcut(exe,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"TwistTidy.lnk"));
                status.Text="Installed successfully. Open TwistTidy from your Start menu"+(desktop.IsChecked==true?" or desktop.":".")+" Your files have not been scanned or changed.";
                install.Content="Installed";
            }
            catch(Exception ex){status.Text=ex.Message;install.IsEnabled=desktop.IsEnabled=true;}
            finally{close.IsEnabled=true;}
        };
        content.Children.Add(status);content.Children.Add(new StackPanel{Orientation=Orientation.Horizontal,Children={install,close}});content.Children.Add(new TextBlock{Text="Powered by Tweest • Offline",FontSize=11,Margin=new(0,18,0,0)});Content=content;
    }
    private static void InstallFiles(string self,string folder)
    {
        Directory.CreateDirectory(folder);string exe=Path.Combine(folder,"TwistTidy.exe");
        if(File.Exists(exe))
        {
            if(FileVersionInfo.GetVersionInfo(exe).ProductName!="TwistTidy")throw new IOException("An unrelated TwistTidy.exe already exists in the install folder. It was not replaced.");
            using var check=new FileStream(exe,FileMode.Open,FileAccess.ReadWrite,FileShare.None);
        }
        string temporary=Path.Combine(folder,"TwistTidy.install-"+Guid.NewGuid()+".tmp");File.Copy(self,temporary,false);
        if(Hashing.Full(self)!=Hashing.Full(temporary))throw new IOException("The installed copy did not pass verification. Run setup again.");
        File.Move(temporary,exe,true);
        foreach(string notice in new[]{"Microsoft-DotNet-License.txt","Microsoft-DotNet-Notices.txt"})
        {
            using var source=typeof(SetupWindow).Assembly.GetManifestResourceStream(notice)??throw new IOException("Missing runtime license notice.");
            using var output=File.Create(Path.Combine(folder,notice));source.CopyTo(output);
        }
    }
    internal static void VerifyPackage(string self,string folder)
    {
        InstallFiles(self,folder);string exe=Path.Combine(folder,"TwistTidy.exe"),linkPath=Path.Combine(folder,"TwistTidy.lnk");Shortcut(exe,linkPath);Shortcut(exe,linkPath);
        object shell=Activator.CreateInstance(Type.GetTypeFromCLSID(new("00021401-0000-0000-C000-000000000046"))!)!;
        try
        {((IPersistFile)shell).Load(linkPath,0);var path=new StringBuilder(32768);((IShellLink)shell).GetPath(path,path.Capacity,IntPtr.Zero,0);if(!path.ToString().Equals(exe,StringComparison.OrdinalIgnoreCase))throw new IOException("Shortcut target mismatch.");}
        finally{Marshal.FinalReleaseComObject(shell);}
        File.WriteAllText(Path.Combine(folder,"package-check.txt"),"PASS: standalone copy hash verified; native Shell shortcut saved, reloaded and target verified.\r\n");
    }
    private static void Shortcut(string exe,string path)
    {
        object shell=Activator.CreateInstance(Type.GetTypeFromCLSID(new("00021401-0000-0000-C000-000000000046"))!)!;
        try
        {
            var link=(IShellLink)shell;
            if(File.Exists(path))
            {
                ((IPersistFile)shell).Load(path,0);var existing=new StringBuilder(32768);link.GetPath(existing,existing.Capacity,IntPtr.Zero,0);
                if(!existing.ToString().Equals(exe,StringComparison.OrdinalIgnoreCase))path=Safety.Unique(path);
            }
            link.SetPath(exe);link.SetWorkingDirectory(Path.GetDirectoryName(exe)!);link.SetDescription("TwistTidy | Powered by Tweest");link.SetIconLocation(exe,0);((IPersistFile)shell).Save(path,true);
        }
        finally{Marshal.FinalReleaseComObject(shell);}
    }
    [ComImport,Guid("000214F9-0000-0000-C000-000000000046"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLink
    {
        void GetPath([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder file,int count,IntPtr data,uint flags);void GetIDList(out IntPtr id);void SetIDList(IntPtr id);void GetDescription([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder text,int count);void SetDescription([MarshalAs(UnmanagedType.LPWStr)]string text);void GetWorkingDirectory([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder text,int count);void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)]string text);void GetArguments([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder text,int count);void SetArguments([MarshalAs(UnmanagedType.LPWStr)]string text);void GetHotkey(out short key);void SetHotkey(short key);void GetShowCmd(out int command);void SetShowCmd(int command);void GetIconLocation([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder text,int count,out int index);void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)]string text,int index);void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)]string path,uint reserved);void Resolve(IntPtr hwnd,uint flags);void SetPath([MarshalAs(UnmanagedType.LPWStr)]string path);
    }
}

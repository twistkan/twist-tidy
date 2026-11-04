using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace TwistTidy.Views;
public class Splash : Window
{
    public Splash()
    {
        Style=(Style)FindResource(typeof(Window));
        Width=460;Height=320;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;WindowStartupLocation=WindowStartupLocation.CenterScreen;ShowInTaskbar=false;
        var p=new StackPanel{VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center};
        p.Children.Add(new Image{Source=(ImageSource)FindResource("Logo"),Width=86,Height=86});p.Children.Add(new TextBlock{Text="TwistTidy",FontSize=32,FontWeight=FontWeights.Bold,HorizontalAlignment=HorizontalAlignment.Center,Margin=new(0,14,0,0)});p.Children.Add(new TextBlock{Text="Order, twisted your way.",HorizontalAlignment=HorizontalAlignment.Center,Margin=new(0,6,0,22)});p.Children.Add(new TextBlock{Text="Powered by Tweest",FontSize=12,HorizontalAlignment=HorizontalAlignment.Center});Content=p;
    }
}

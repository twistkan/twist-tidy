using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace TwistTidy.Assets;
public static class IconExporter
{
    public static void Export(string path,ImageSource logo)
    {
        int[] sizes=[16,24,32,48,64,128,256];var images=new List<byte[]>();
        foreach(int size in sizes)
        {
            var visual=new DrawingVisual();using(var drawing=visual.RenderOpen())drawing.DrawImage(logo,new Rect(0,0,size,size));
            var bitmap=new RenderTargetBitmap(size,size,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var ms=new MemoryStream();encoder.Save(ms);images.Add(ms.ToArray());
        }
        using var f=File.Create(path);using var writer=new BinaryWriter(f);writer.Write((ushort)0);writer.Write((ushort)1);writer.Write((ushort)sizes.Length);int offset=6+16*sizes.Length;
        for(int i=0;i<sizes.Length;i++){writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)0);writer.Write((byte)0);writer.Write((ushort)1);writer.Write((ushort)32);writer.Write(images[i].Length);writer.Write(offset);offset+=images[i].Length;}
        foreach(var bytes in images)writer.Write(bytes);
    }
}

using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;

namespace TwistTidy.Core.Services;

public static class RecycleService
{
    // Windows 8+ Shell operation explicitly requests recycling, with no permanent-delete fallback.
    public static (string Data,string Info) Send(string path,string hash)
    {
        Safety.Check(path);
        if(new DriveInfo(Path.GetPathRoot(path)!).DriveType!=DriveType.Fixed)throw new IOException("Recycle Bin actions require a fixed local disk. Use a review folder for removable drives.");
        Exception? error=null;
        var thread=new Thread(()=>
        {
            IFileOperation? op=null;IShellItem? item=null;
            try
            {
                op=(IFileOperation)Activator.CreateInstance(Type.GetTypeFromCLSID(new("3AD05575-8857-4850-9277-11B85BDB8E09"))!)!;
                op.SetOperationFlags(0x00080000|0x00100000|0x0400|0x0010|0x0004|0x0040|0x2000);
                Guid iid=new("43826D1E-E718-42EE-BC55-A1E261C37BFE");Marshal.ThrowExceptionForHR(SHCreateItemFromParsingName(path,IntPtr.Zero,ref iid,out item));
                op.DeleteItem(item,IntPtr.Zero);op.PerformOperations();op.GetAnyOperationsAborted(out bool aborted);
                if(aborted||File.Exists(path))throw new IOException("Windows could not send this file to the Recycle Bin.");
            }catch(Exception e){error=e;}
            finally{if(item!=null)Marshal.FinalReleaseComObject(item);if(op!=null)Marshal.FinalReleaseComObject(op);}
        });
        thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();if(error!=null)throw new IOException("Recycle failed: "+error.Message,error);
        return Find(path,hash)??throw new IOException("File was recycled, but its restore record could not be found. Open Windows Recycle Bin to restore it.");
    }
    public static (string Data,string Info)? Find(string original,string hash)
    {
        string root=Path.Combine(Path.GetPathRoot(original)!,"$Recycle.Bin",WindowsIdentity.GetCurrent().User!.Value);
        if(!Directory.Exists(root))return null;
        foreach(string info in Directory.EnumerateFiles(root,"$I*"))
        {
            try
            {
                byte[] b=File.ReadAllBytes(info);if(b.Length<28)continue;long version=BitConverter.ToInt64(b,0);int offset=version==2?28:24;
                string path=Encoding.Unicode.GetString(b,offset,b.Length-offset).TrimEnd('\0');
                if(!path.Equals(original,StringComparison.OrdinalIgnoreCase))continue;
                string data=Path.Combine(root,"$R"+Path.GetFileName(info)[2..]);
                if(File.Exists(data)&&Hashing.Full(data)==hash)return(data,info);
            }catch(Exception e)when(e is IOException or UnauthorizedAccessException or ArgumentException){ }
        }
        return null;
    }
    public static void Restore(string data,string info,string destination,string hash)
    {
        Safety.Check(destination);
        string expected=Path.Combine(Path.GetPathRoot(destination)!,"$Recycle.Bin",WindowsIdentity.GetCurrent().User!.Value);
        if(!Safety.Within(data,expected)||!Safety.Within(info,expected)||Path.GetFileName(data)!="$R"+Path.GetFileName(info)[2..])throw new IOException("Invalid recycle restore record.");
        if(Hashing.Full(data)!=hash)throw new IOException("Recycled contents changed; restore skipped.");
        File.Move(data,destination,false);
        // This is Shell metadata belonging to the restored item, not a user file.
        if(File.Exists(info))File.Delete(info);
    }
    [DllImport("shell32.dll",CharSet=CharSet.Unicode,PreserveSig=true)]
    private static extern int SHCreateItemFromParsingName(string path,IntPtr context,ref Guid iid,out IShellItem item);
    [ComImport,Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler(IntPtr context,ref Guid handler,ref Guid iid,out IntPtr result);
        void GetParent(out IShellItem parent);
        void GetDisplayName(uint name,out IntPtr text);
        void GetAttributes(uint mask,out uint attributes);
        void Compare(IShellItem other,uint hint,out int order);
    }
    [ComImport,Guid("947AAB5F-0A5C-4C13-B4D6-4BF7836FC9F8"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOperation
    {
        void Advise(IntPtr sink,out uint cookie);void Unadvise(uint cookie);void SetOperationFlags(uint flags);void SetProgressMessage([MarshalAs(UnmanagedType.LPWStr)]string message);void SetProgressDialog(IntPtr dialog);void SetProperties(IntPtr props);void SetOwnerWindow(IntPtr hwnd);
        void ApplyPropertiesToItem(IShellItem item);void ApplyPropertiesToItems([MarshalAs(UnmanagedType.IUnknown)]object items);
        void RenameItem(IShellItem item,[MarshalAs(UnmanagedType.LPWStr)]string name,IntPtr sink);void RenameItems([MarshalAs(UnmanagedType.IUnknown)]object items,[MarshalAs(UnmanagedType.LPWStr)]string name);
        void MoveItem(IShellItem item,IShellItem folder,[MarshalAs(UnmanagedType.LPWStr)]string name,IntPtr sink);void MoveItems([MarshalAs(UnmanagedType.IUnknown)]object items,IShellItem folder);
        void CopyItem(IShellItem item,IShellItem folder,[MarshalAs(UnmanagedType.LPWStr)]string name,IntPtr sink);void CopyItems([MarshalAs(UnmanagedType.IUnknown)]object items,IShellItem folder);
        void DeleteItem(IShellItem item,IntPtr sink);void DeleteItems([MarshalAs(UnmanagedType.IUnknown)]object items);void NewItem(IShellItem folder,uint attributes,[MarshalAs(UnmanagedType.LPWStr)]string name,[MarshalAs(UnmanagedType.LPWStr)]string template,IntPtr sink);void PerformOperations();void GetAnyOperationsAborted([MarshalAs(UnmanagedType.Bool)]out bool aborted);
    }
}


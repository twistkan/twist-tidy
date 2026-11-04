using System.Security.Cryptography;
using System.Text.Json;
using TwistTidy.Core.Models;

namespace TwistTidy.Core.Services;

public static class Safety
{
    public static bool Within(string path, string root) => path.Equals(root,StringComparison.OrdinalIgnoreCase) || path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);
    public static string Check(string path)
    {
        if(string.IsNullOrWhiteSpace(path)) throw new IOException("Choose a folder first.");
        string full=Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
        if(full.StartsWith(@"\\") || full.StartsWith(@"\?\")) throw new IOException("Use a local drive path.");
        if(full.Length<=3) throw new IOException("Choose a specific work folder, not a whole drive.");
        string[] blocked=[Environment.GetFolderPath(Environment.SpecialFolder.Windows), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)];
        if(blocked.Any(b=>b.Length>0 && Within(full,b))) throw new IOException("System and application data folders are protected.");
        if(full.Split(Path.DirectorySeparatorChar).Any(s=>s.Equals("$RECYCLE.BIN",StringComparison.OrdinalIgnoreCase)||s.Equals("System Volume Information",StringComparison.OrdinalIgnoreCase))) throw new IOException("This is a protected system folder.");
        for(string? p=full; p!=null; p=Path.GetDirectoryName(p))
            if((File.Exists(p)||Directory.Exists(p)) && (File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0) throw new IOException("Links and junctions are skipped for safety.");
        return full;
    }
    public static string Segment(string input)
    {
        var text = new string(input.Trim().Select(c=>Path.GetInvalidFileNameChars().Contains(c)?'_':c).ToArray()).TrimEnd('.',' ');
        if(text.Length==0 || text is "." or "..") throw new IOException("A folder name is empty or invalid.");
        string stem=text.Split('.')[0].ToUpperInvariant();
        if(new[]{"CON","PRN","AUX","NUL","COM1","COM2","COM3","COM4","COM5","COM6","COM7","COM8","COM9","LPT1","LPT2","LPT3","LPT4","LPT5","LPT6","LPT7","LPT8","LPT9"}.Contains(stem)) text="_"+text;
        return text;
    }
    public static string Relative(string input)
    {
        if(Path.IsPathRooted(input)) throw new IOException("Folder patterns must be relative.");
        var bits=input.Replace('/',Path.DirectorySeparatorChar).Split(Path.DirectorySeparatorChar);
        if(bits.Any(s=>s is "" or "." or ".." || s.Contains(':'))) throw new IOException("Use folder names without empty parts, . or ..");
        return Path.Combine(bits.Select(Segment).ToArray());
    }
    public static string Unique(string path, HashSet<string>? reserved=null)
    {
        string candidate=path; int i=2;
        while(File.Exists(candidate)||Directory.Exists(candidate)||(reserved?.Contains(candidate)??false))
            candidate=Path.Combine(Path.GetDirectoryName(path)!,Path.GetFileNameWithoutExtension(path)+$" ({i++})"+Path.GetExtension(path));
        reserved?.Add(candidate); return candidate;
    }
}
public class LocalStore
{
    public string Root { get; }
    public List<string> Warnings { get; }=[];
    private static readonly JsonSerializerOptions Options=new(){WriteIndented=true};
    public LocalStore(string? root=null) {Root=root??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"TwistTidy");Directory.CreateDirectory(Path.Combine(Root,"History"));}
    public Settings LoadSettings()
    {
        try
        {
            var settings=Read<Settings>(Path.Combine(Root,"settings.json"))??new();
            if(settings.CameraPatterns==null||settings.Presets==null||settings.Templates==null||settings.ExcludedFolders==null||settings.Templates.Count==0)throw new JsonException("Invalid settings structure.");
            return settings;
        }
        catch(Exception e)when(e is IOException or JsonException){Warnings.Add("Settings could not be read. Defaults loaded; the existing file is preserved until you save. "+e.Message);return new();}
    }
    public void SaveSettings(Settings settings)=>Write(Path.Combine(Root,"settings.json"),settings);
    public void Save(Batch batch)=>Write(Path.Combine(Root,"History",batch.Id+".json"),batch);
    public List<Batch> History()
    {
        var batches=new List<Batch>();
        foreach(string path in Directory.GetFiles(Path.Combine(Root,"History"),"*.json"))
        {try{var b=Read<Batch>(path);if(b!=null)batches.Add(b);}catch(Exception e)when(e is IOException or JsonException){string warning="Unreadable history record preserved: "+Path.GetFileName(path);if(!Warnings.Contains(warning))Warnings.Add(warning);}}
        return batches.OrderByDescending(b=>b.Date).ToList();
    }
    public static T? Read<T>(string path) {if(!File.Exists(path))return default; return JsonSerializer.Deserialize<T>(File.ReadAllText(path),Options);}
    public static void Write<T>(string path,T value)
    {
        string temp=path+".tmp";
        using(var f=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){JsonSerializer.Serialize(f,value,Options);f.Flush(true);}
        File.Move(temp,path,true);
    }
}
public static class Hashing
{
    public static string Full(string path,CancellationToken ct=default)
    {
        using var f=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,1024*1024,FileOptions.SequentialScan);
        using var sha=IncrementalHash.CreateHash(HashAlgorithmName.SHA256); byte[] buffer=new byte[1024*1024]; int n;
        while((n=f.Read(buffer,0,buffer.Length))>0){ct.ThrowIfCancellationRequested();sha.AppendData(buffer,0,n);} return Convert.ToHexString(sha.GetHashAndReset());
    }
    public static string Partial(string path,CancellationToken ct)
    {
        using var f=File.OpenRead(path);using var sha=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);byte[] b=new byte[65536];
        foreach(long pos in new[]{0L,Math.Max(0,f.Length/2-32768),Math.Max(0,f.Length-65536)}){ct.ThrowIfCancellationRequested();f.Position=pos;int n=f.Read(b);sha.AppendData(b,0,n);}return Convert.ToHexString(sha.GetHashAndReset());
    }
}

using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using TwistTidy.Core.Models;

namespace TwistTidy.Core.Services;

public class Scanner
{
    public List<string> Warnings { get; }=[];
    public List<ScanFile> Scan(string root,IEnumerable<string> excluded,CancellationToken ct,IProgress<ScanProgress>? progress=null)
    {
        root=Safety.Check(root);if(!Directory.Exists(root))throw new IOException("The scan folder does not exist.");
        var exclusions=excluded.Where(x=>!string.IsNullOrWhiteSpace(x)).ToArray();var result=new List<ScanFile>();var stack=new Stack<string>();stack.Push(root);
        while(stack.Count>0)
        {
            ct.ThrowIfCancellationRequested();string dir=stack.Pop();
            try
            {
                Safety.Check(dir);
                foreach(var file in Directory.EnumerateFiles(dir))
                {
                    ct.ThrowIfCancellationRequested();try{Safety.Check(file);var f=new FileInfo(file); result.Add(new(){Path=file,Size=f.Length,Modified=f.LastWriteTimeUtc,Created=f.CreationTimeUtc});if(result.Count%50==0)progress?.Report(new(result.Count,$"Scanning {result.Count:N0} files"));}catch(Exception e)when(e is IOException or UnauthorizedAccessException){Warn(file+": "+e.Message);}
                }
                foreach(var child in Directory.EnumerateDirectories(dir))
                {
                    if(exclusions.Any(x=>Path.IsPathRooted(x)?Safety.Within(child,Path.GetFullPath(x)):Path.GetFileName(child).Equals(x,StringComparison.OrdinalIgnoreCase)))continue;
                    if((File.GetAttributes(child)&FileAttributes.ReparsePoint)!=0){Warn(child+": link skipped");continue;}stack.Push(child);
                }
            }
            catch(Exception e)when(e is IOException or UnauthorizedAccessException){Warn(dir+": "+e.Message);}
        }
        progress?.Report(new(result.Count,$"Scanned {result.Count:N0} files"));return result;
    }
    private void Warn(string message){if(Warnings.Count<500)Warnings.Add(message);}
}
public record MediaInfo(DateTime Date,string DateSource,string Resolution,string Duration);
public static class Metadata
{
    public static MediaInfo Read(ScanFile file)
    {
        DateTime date=file.Modified.ToLocalTime();string source="modified date",resolution="Unknown resolution",duration="";
        object? shell=null,folder=null,item=null;
        try
        {
            if(!OperatingSystem.IsWindows())return new(date,source,resolution,duration);
            shell=Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")!);
            folder=((dynamic)shell!).NameSpace(Path.GetDirectoryName(file.Path));item=((dynamic)folder!).ParseName(Path.GetFileName(file.Path));
            object? taken=((dynamic)item!).ExtendedProperty("System.Photo.DateTaken");
            if(taken is DateTime dt && dt.Year>1900){date=dt;source="media metadata";}
            else{object? encoded=((dynamic)item!).ExtendedProperty("System.Media.DateEncoded");if(encoded is DateTime enc && enc.Year>1900){date=enc;source="media metadata (encoded)";}}
            object? w=((dynamic)item!).ExtendedProperty("System.Video.FrameWidth");object? h=((dynamic)item!).ExtendedProperty("System.Video.FrameHeight");
            if(Convert.ToInt32(w)==0){w=((dynamic)item!).ExtendedProperty("System.Image.HorizontalSize");h=((dynamic)item!).ExtendedProperty("System.Image.VerticalSize");}
            if(Convert.ToInt32(w)>0 && Convert.ToInt32(h)>0)resolution=$"{w}x{h}";
            object? ticks=((dynamic)item!).ExtendedProperty("System.Media.Duration");if(ticks!=null && Convert.ToInt64(ticks)>0)duration=TimeSpan.FromTicks(Convert.ToInt64(ticks)).ToString(@"hh\:mm\:ss");
        }catch{ /* Unsupported codecs and absent metadata use explicit fallbacks. */ }
        finally{foreach(var o in new[]{item,folder,shell})if(o!=null && Marshal.IsComObject(o))Marshal.FinalReleaseComObject(o);}
        return new(date,source,resolution,duration);
    }
}
public static class Planner
{
    private static readonly Dictionary<string,string> Types=new(StringComparer.OrdinalIgnoreCase);
    static Planner()
    {
        foreach(var (type,exts) in new[]{("Footage","mp4 mov mxf avi mkv mts braw r3d"),("Photos","jpg jpeg png raw cr2 cr3 arw dng heic"),("Audio","wav mp3 aac flac"),("Projects","prproj drp aep psd")})
            foreach(var ext in exts.Split(' '))Types["."+ext]=type;
    }
    public static List<PlanRow> Sort(IEnumerable<ScanFile> files,string destination,string pattern,Settings settings,CancellationToken ct,IProgress<ScanProgress>? progress=null)
    {
        destination=Safety.Check(destination);if(string.IsNullOrWhiteSpace(pattern))throw new IOException("Choose a sorting pattern.");
        var rows=new List<PlanRow>();var reserved=new HashSet<string>(StringComparer.OrdinalIgnoreCase);int count=0;
        foreach(var f in files)
        {
            ct.ThrowIfCancellationRequested();if(!Types.TryGetValue(Path.GetExtension(f.Path),out string? type))continue;
            var meta=Metadata.Read(f);string name=Path.GetFileNameWithoutExtension(f.Path);
            string source=settings.CameraPatterns.FirstOrDefault(p=>p.Prefix.Length>0 && name.StartsWith(p.Prefix,StringComparison.OrdinalIgnoreCase) && (p.Prefix!="C" || Regex.IsMatch(name,@"^C\d{4}")))?.Source??"Other";
            string relative=pattern.Replace("{type}",type).Replace("{date}",meta.Date.ToString("yyyy-MM-dd")).Replace("{source}",Safety.Segment(source)).Replace("{resolution}",meta.Resolution);
            if(relative.Contains('{')||relative.Contains('}'))throw new IOException("Unknown pattern token. Use type, date, source or resolution.");
            string target=Safety.Check(Path.Combine(destination,Safety.Relative(relative),Path.GetFileName(f.Path)));
            if(target.Equals(f.Path,StringComparison.OrdinalIgnoreCase))continue;
            target=Safety.Unique(target,reserved);
            rows.Add(new(){Source=f.Path,Destination=target,Size=f.Size,ModifiedTicks=f.Modified.Ticks,Note=$"{source} | {meta.Date:yyyy-MM-dd} ({meta.DateSource}) | {meta.Resolution}"+(meta.Duration.Length>0?$" | {meta.Duration}":"")});
            progress?.Report(new(++count,$"Planning media file {count:N0}"));
        }
        return rows;
    }
    public static List<PlanRow> Project(string parent,string client,string project,DateTime date,string projectType,FolderTemplate template,bool readme,string namePattern="{date}_{client}_{project}")
    {
        parent=Safety.Check(parent);
        string name=namePattern.Replace("{date}",date.ToString("yyyy-MM-dd")).Replace("{client}",Safety.Segment(client)).Replace("{project}",Safety.Segment(project)).Replace("{type}",Safety.Segment(projectType));
        if(name.Contains('{')||name.Contains('}')||name.Contains('/')||name.Contains('\\'))throw new IOException("Project name pattern supports date, client, project and type tokens, with no path separators.");
        string root=Safety.Unique(Path.Combine(parent,Safety.Segment(name)));
        var rows=new List<PlanRow>{new(){Action="Create folder",Destination=root}};
        foreach(var folder in template.Folders.Distinct(StringComparer.OrdinalIgnoreCase))rows.Add(new(){Action="Create folder",Destination=Safety.Check(Path.Combine(root,Safety.Relative(folder)))});
        if(readme)rows.Add(new(){Action="Create README",Destination=Path.Combine(root,"README.txt"),Content=$"Project: {project}\r\nClient: {client}\r\nDate: {date:yyyy-MM-dd}\r\nType: {projectType}\r\nTemplate: {template.Name}\r\n\r\nCreated with TwistTidy. Powered by Tweest.\r\n"});
        return rows;
    }
    public static List<PlanRow> Duplicates(List<ScanFile> files,long minimum,string extensions,bool oldest,CancellationToken ct,IProgress<ScanProgress>? progress,List<string> warnings)
    {
        var filter=extensions.Split([',',';',' '],StringSplitOptions.RemoveEmptyEntries).Select(x=>"."+x.TrimStart('.')).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rows=new List<PlanRow>();int done=0,group=0;
        foreach(var sizeGroup in files.Where(f=>f.Size>=minimum&&(filter.Count==0||filter.Contains(Path.GetExtension(f.Path)))).GroupBy(f=>f.Size).Where(g=>g.Count()>1))
        {
            var quick=new List<(ScanFile File,string Hash)>();
            foreach(var f in sizeGroup){ct.ThrowIfCancellationRequested();try{quick.Add((f,Hashing.Partial(f.Path,ct)));}catch(Exception e)when(e is IOException or UnauthorizedAccessException){warnings.Add(f.Path+": "+e.Message);}progress?.Report(new(++done,$"Comparing candidate {done:N0}"));}
            foreach(var partial in quick.GroupBy(x=>x.Hash).Where(g=>g.Count()>1))
            {
                var full=new List<(ScanFile File,string Hash)>();
                foreach(var f in partial){ct.ThrowIfCancellationRequested();try{full.Add((f.File,Hashing.Full(f.File.Path,ct)));}catch(Exception e)when(e is IOException or UnauthorizedAccessException){warnings.Add(f.File.Path+": "+e.Message);}}
                foreach(var exact in full.GroupBy(x=>x.Hash).Where(g=>g.Count()>1))
                {
                    group++;var ordered=oldest?exact.OrderBy(x=>x.File.Modified).ThenBy(x=>x.File.Path,StringComparer.OrdinalIgnoreCase):exact.OrderByDescending(x=>x.File.Path.Count(c=>c==Path.DirectorySeparatorChar)).ThenBy(x=>x.File.Modified);
                    bool keep=true;
                    foreach(var f in ordered){rows.Add(new(){Source=f.File.Path,Size=f.File.Size,ModifiedTicks=f.File.Modified.Ticks,Hash=f.Hash,Group=$"Group {group:000}",Action=keep?"Keep":"Recycle",Selected=!keep,Note=keep?"Suggested keeper | SHA-256 match":"Identical content | SHA-256 match"});keep=false;}
                }
            }
        }
        return rows;
    }
}

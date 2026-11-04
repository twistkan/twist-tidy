using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TwistTidy.Core.Models;

public class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    { if (EqualityComparer<T>.Default.Equals(field, value)) return false; field = value; Changed(name); return true; }
}
public class PlanRow : Observable
{
    private bool selected = true;
    public bool Selected { get => selected; set => Set(ref selected, value); }
    public string File => Path.GetFileName(Source.Length > 0 ? Source : Destination);
    public string Source { get; set; } = "";
    public string Destination { get; set; } = "";
    public string Action { get; set; } = "Move";
    public long Size { get; set; }
    public long ModifiedTicks { get; set; }
    public string Hash { get; set; } = "";
    public string Note { get; set; } = "";
    public string Content { get; set; } = "";
    public string Group { get; set; } = "";
    public string SizeText => Format.Bytes(Size);
}
public class ScanFile
{
    public string Path { get; set; } = "";
    public long Size { get; set; }
    public DateTime Modified { get; set; }
    public DateTime Created { get; set; }
}
public record ScanProgress(int Count, string Message);
public class CameraPattern { public string Prefix { get; set; } = ""; public string Source { get; set; } = ""; }
public class FolderTemplate
{
    public string Name { get; set; } = "Custom";
    public List<string> Folders { get; set; } = [];
}
public class Settings
{
    public string Theme { get; set; } = "Dark";
    public string Accent { get; set; } = "#7C3AED";
    public List<CameraPattern> CameraPatterns { get; set; } = [new(){Prefix="DJI_",Source="Drone"},new(){Prefix="GOPR",Source="GoPro"},new(){Prefix="GX",Source="GoPro"},new(){Prefix="MVI_",Source="Canon"},new(){Prefix="C",Source="Sony"},new(){Prefix="PXL_",Source="Phone"},new(){Prefix="IMG_",Source="Phone"}];
    public List<string> Presets { get; set; } = ["{type}/{date}/{source}","Footage/{source}/{date}","{type}","{date}","{source}","{resolution}"];
    public List<string> ExcludedFolders { get; set; } = [".git","node_modules",".tools","$RECYCLE.BIN","System Volume Information"];
    public List<FolderTemplate> Templates { get; set; } = [
        new(){Name="Video Production",Folders=["01_Footage","02_Audio","03_Graphics","04_Edit","05_Exports","06_Docs"]},
        new(){Name="Drone Shoot",Folders=["01_Footage/Drone","02_Audio","03_Flight_Logs","04_Edit","05_Exports","06_Docs"]},
        new(){Name="Documentary",Folders=["01_Footage/Interviews","01_Footage/B_Roll","02_Audio/Voiceover","02_Audio/Music","03_Research","04_Edit","05_Exports","06_Docs"]},
        new(){Name="Commercial",Folders=["01_Footage","02_Audio","03_Graphics/Brand","04_Edit","05_Exports/Review","05_Exports/Final","06_Docs/Brief"]}];
}
public class JournalEntry
{
    public string Action { get; set; } = "";
    public string Source { get; set; } = "";
    public string Destination { get; set; } = "";
    public string Hash { get; set; } = "";
    public string State { get; set; } = "Pending";
    public string Error { get; set; } = "";
    public string RecycleData { get; set; } = "";
    public string RecycleInfo { get; set; } = "";
    public string UndoDestination { get; set; } = "";
}
public class Batch
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime Date { get; set; } = DateTime.Now;
    public string Title { get; set; } = "";
    public string State { get; set; } = "Running";
    public List<JournalEntry> Entries { get; set; } = [];
    public int Count => Entries.Count(e => e.Action != "Create folder");
    public override string ToString() => $"{Date:g}   {Title}   {Count} files   {State}";
}
public static class Format
{
    public static string Bytes(long size) { double n=size; string[] units=["B","KB","MB","GB","TB"]; int i=0; while(n>=1024 && i<4){n/=1024;i++;} return $"{n:0.##} {units[i]}"; }
}

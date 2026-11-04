using System.Text;
using System.Security.Cryptography;
using TwistTidy.Core.Models;

namespace TwistTidy.Core.Services;

public class ExecutionService(LocalStore store)
{
    private sealed class HeldFiles : IDisposable
    {
        public List<FileStream> Streams { get; }=[];
        public void Dispose(){foreach(var stream in Streams)stream.Dispose();}
    }
    private void EnsureDirectory(string dir,Batch batch)
    {
        Safety.Check(dir);if(Directory.Exists(dir))return;
        string? parent=Path.GetDirectoryName(dir);if(parent!=null&&!Directory.Exists(parent))EnsureDirectory(parent,batch);
        var entry=new JournalEntry{Action="Create folder",Destination=dir};batch.Entries.Add(entry);store.Save(batch);
        Directory.CreateDirectory(dir);entry.State="Done";store.Save(batch);
    }
    public Batch Execute(string title,IReadOnlyList<PlanRow> all,CancellationToken ct,IProgress<ScanProgress>? progress=null)
    {
        var selected=all.Where(r=>r.Selected&&r.Action!="Keep").ToArray();if(selected.Length==0)throw new IOException("Select at least one operation.");
        using var keepers=new HeldFiles();
        // Validate a surviving identical keeper before accepting any duplicate action.
        foreach(var g in selected.Where(r=>r.Group.Length>0).GroupBy(r=>r.Group))
        {
            var keeper=all.FirstOrDefault(r=>r.Group==g.Key&&r.Action=="Keep");
            if(keeper==null)throw new IOException("Each duplicate group needs a keeper.");
            Safety.Check(keeper.Source);keepers.Streams.Add(new FileStream(keeper.Source,FileMode.Open,FileAccess.Read,FileShare.Read));
            if(Hashing.Full(keeper.Source,ct)!=keeper.Hash)throw new IOException("A keeper changed since scanning. Scan again.");
        }
        var batch=new Batch{Title=title};store.Save(batch);int completed=0;
        try
        {
            foreach(var row in selected)
            {
                ct.ThrowIfCancellationRequested();JournalEntry? entry=null;
                try
                {
                    if(row.Action=="Create folder"){EnsureDirectory(row.Destination,batch);continue;}
                    if(row.Action is not ("Move" or "Recycle" or "Create README"))throw new IOException("Unsupported operation.");
                    if(row.Action!="Create README")Safety.Check(row.Source);
                    // Hold off writers while hashing and moving this file. Permit our own rename/delete.
                    using var fileLock=row.Action=="Create README"?null:new FileStream(row.Source,FileMode.Open,FileAccess.Read,FileShare.Read|FileShare.Delete);
                    if(row.Action!="Recycle")
                    {
                        Safety.Check(row.Destination);
                        if(File.Exists(row.Destination)||Directory.Exists(row.Destination))throw new IOException("Destination appeared after preview. Scan again for a new suffix.");
                    }
                    string hash;
                    if(row.Action=="Create README")hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(row.Content)));
                    else
                    {
                        Safety.Check(row.Source);var info=new FileInfo(row.Source);
                        if(info.Length!=row.Size||info.LastWriteTimeUtc.Ticks!=row.ModifiedTicks)throw new IOException("File changed since preview. Scan again.");
                        hash=Hashing.Full(row.Source,ct);if(row.Hash.Length>0&&hash!=row.Hash)throw new IOException("File content changed since scan.");
                    }
                    ct.ThrowIfCancellationRequested();
                    if(row.Action!="Recycle")EnsureDirectory(Path.GetDirectoryName(row.Destination)!,batch);
                    entry=new(){Action=row.Action,Source=row.Source,Destination=row.Destination,Hash=hash};batch.Entries.Add(entry);store.Save(batch);
                    if(row.Action=="Move"){Safety.Check(row.Source);Safety.Check(row.Destination);File.Move(row.Source,row.Destination,false);}
                    else if(row.Action=="Create README")
                    {using var stream=new FileStream(row.Destination,FileMode.CreateNew,FileAccess.Write,FileShare.None);var bytes=Encoding.UTF8.GetBytes(row.Content);stream.Write(bytes);stream.Flush(true);}
                    else {var recycled=RecycleService.Send(row.Source,hash);entry.RecycleData=recycled.Data;entry.RecycleInfo=recycled.Info;}
                    entry.State="Done";store.Save(batch);
                }
                catch(OperationCanceledException){throw;}
                catch(Exception e)when(e is IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
                {
                    entry??=new(){Action=row.Action,Source=row.Source,Destination=row.Destination,State="Skipped"};
                    if(!batch.Entries.Contains(entry))batch.Entries.Add(entry);
                    // Keep Pending entries recoverable if the filesystem changed before an error.
                    entry.Error=e.Message;store.Save(batch);
                }
                progress?.Report(new(++completed,$"Processed {completed:N0} of {selected.Length:N0}"));
            }
            batch.State=batch.Entries.Any(e=>e.Error.Length>0)?"Completed with issues":"Completed";
        }
        catch(OperationCanceledException){batch.State="Cancelled, partial batch";}
        finally{store.Save(batch);}
        return batch;
    }
    public List<PlanRow> UndoPreview(Batch batch)
    {
        var rows=new List<PlanRow>();var reserved=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var (entry,index) in batch.Entries.Select((e,i)=>(e,i)).Reverse())
        {
            if(entry.State is "Undone" or "Skipped")continue;
            if(entry.State=="Undo pending")
            {
                if(entry.Action is "Move" or "Recycle")
                {
                    string undoSource=entry.Action=="Move"?entry.Destination:entry.RecycleData;
                    if(!File.Exists(undoSource)&&File.Exists(entry.UndoDestination)&&Hashing.Full(entry.UndoDestination)==entry.Hash){entry.State="Undone";continue;}
                }
                else if(entry.Action=="Create folder"&&!Directory.Exists(entry.Destination)){entry.State="Undone";continue;}
                else if(entry.Action=="Create README"&&!File.Exists(entry.Destination)&&RecycleService.Find(entry.Destination,entry.Hash)!=null){entry.State="Undone";continue;}
                entry.State="Done";
            }
            if(entry.State=="Pending")
            {
                if(entry.Action=="Move"&&!File.Exists(entry.Source)&&File.Exists(entry.Destination)&&Hashing.Full(entry.Destination)==entry.Hash)entry.State="Done";
                else if(entry.Action=="Create README"&&File.Exists(entry.Destination)&&Hashing.Full(entry.Destination)==entry.Hash)entry.State="Done";
                else if(entry.Action=="Recycle"&&!File.Exists(entry.Source))
                {var found=RecycleService.Find(entry.Source,entry.Hash);if(found!=null){entry.RecycleData=found.Value.Data;entry.RecycleInfo=found.Value.Info;entry.State="Done";}}
                if(entry.State!="Done")continue;
            }
            string action,from,to;
            if(entry.Action=="Create folder"){action="Remove empty folder";from=entry.Destination;to="";}
            else if(entry.Action=="Create README"){action="Recycle README";from=entry.Destination;to="Recycle Bin";}
            else {action=entry.Action=="Recycle"?"Restore from Recycle Bin":"Undo move";from=entry.Action=="Recycle"?entry.RecycleData:entry.Destination;to=Safety.Unique(entry.Source,reserved);}
            rows.Add(new(){Source=from,Destination=to,Action=action,Group=index.ToString(),Note=entry.Error});
        }
        store.Save(batch);return rows;
    }
    public void Undo(Batch batch,IEnumerable<PlanRow> rows,CancellationToken ct,IProgress<ScanProgress>? progress=null)
    {
        int count=0;
        foreach(var row in rows.Where(r=>r.Selected))
        {
            if(ct.IsCancellationRequested)break;
            var e=batch.Entries[int.Parse(row.Group)];
            try
            {
                if(e.Action!="Recycle")Safety.Check(e.Destination);
                using var fileLock=e.Action=="Create folder"?null:new FileStream(e.Action=="Recycle"?e.RecycleData:e.Destination,FileMode.Open,FileAccess.Read,FileShare.Read|FileShare.Delete);
                e.UndoDestination=row.Destination;e.State="Undo pending";store.Save(batch);
                if(row.Action=="Remove empty folder")
                {Safety.Check(e.Destination);if(Directory.Exists(e.Destination))Directory.Delete(e.Destination,false);}
                else if(row.Action=="Recycle README")
                {Safety.Check(e.Destination);if(Hashing.Full(e.Destination,ct)!=e.Hash)throw new IOException("README was edited. Left in place.");RecycleService.Send(e.Destination,e.Hash);}
                else
                {
                    Safety.Check(row.Destination);if(File.Exists(row.Destination)||Directory.Exists(row.Destination))throw new IOException("Restore destination appeared after preview. Preview undo again.");
                    // Parent normally survives. Recreate only ancestors recorded as created by this batch or original paths.
                    Safety.Check(Path.GetDirectoryName(row.Destination)!);Directory.CreateDirectory(Path.GetDirectoryName(row.Destination)!);
                    if(row.Action=="Restore from Recycle Bin")RecycleService.Restore(e.RecycleData,e.RecycleInfo,row.Destination,e.Hash);
                    else{Safety.Check(e.Destination);if(Hashing.Full(e.Destination,ct)!=e.Hash)throw new IOException("File changed after move. Undo skipped to protect edits.");File.Move(e.Destination,row.Destination,false);}
                }
                e.State="Undone";e.Error="";
            }
            catch(OperationCanceledException){break;}
            catch(Exception ex)when(ex is IOException or UnauthorizedAccessException){e.Error=ex.Message;e.State="Done";}
            store.Save(batch);progress?.Report(new(++count,$"Undo: {count:N0} operations processed"));
        }
        batch.State=batch.Entries.All(e=>e.State is "Undone" or "Skipped")?"Undone":"Partially undone";store.Save(batch);
    }
}

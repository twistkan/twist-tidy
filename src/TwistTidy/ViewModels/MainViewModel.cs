using System.Collections.ObjectModel;
using TwistTidy.Core.Models;
using TwistTidy.Core.Services;
namespace TwistTidy.ViewModels;
public class MainViewModel : Observable
{
    public LocalStore Store { get; }=new();
    public Settings Settings { get; set; }
    public ObservableCollection<PlanRow> Rows { get; }=[];
    private string status="Ready. Your files stay where they are until you approve.";
    public string Status { get=>status;set=>Set(ref status,value); }
    private string summary="No plan yet";
    public string Summary { get=>summary;set=>Set(ref summary,value); }
    private bool busy;
    public bool Busy { get=>busy;set{Set(ref busy,value);Changed(nameof(Ready));} }
    public bool Ready=>!Busy;
    public CancellationTokenSource? Cancellation { get; set; }
    public MainViewModel(){Settings=Store.LoadSettings();}
    public void SetRows(IEnumerable<PlanRow> rows)
    {Rows.Clear();foreach(var r in rows){Rows.Add(r);r.PropertyChanged+=(s,e)=>UpdateSummary();}UpdateSummary();}
    public void UpdateSummary()
    {
        var selected=Rows.Where(r=>r.Selected&&r.Action!="Keep").ToArray();
        var folders=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var row in selected)
        {
            string? p=row.Action=="Create folder"?row.Destination:row.Action is "Move" or "Create README"?Path.GetDirectoryName(row.Destination):null;
            while(!string.IsNullOrEmpty(p)&&!Directory.Exists(p)){folders.Add(p);p=Path.GetDirectoryName(p);}
        }
        Summary=$"{selected.Length:N0} operations selected   •   {Format.Bytes(selected.Sum(r=>r.Size))}   •   {folders.Count:N0} new folders";
    }
}

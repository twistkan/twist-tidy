using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using TwistTidy.Core.Models;
using TwistTidy.Core.Services;
namespace TwistTidy.Views;

public class TemplateEditor : Window
{
    private readonly Settings settings;
    private readonly LocalStore store;
    private readonly List<FolderTemplate> templates;
    private readonly ComboBox choices=new(){MinWidth=220};
    private readonly TextBox templateName=new();
    private readonly TextBox folderName=new(){Text="New folder",MinWidth=190};
    private readonly TreeView tree=new();
    private FolderTemplate Current=>templates[choices.SelectedIndex];
    public TemplateEditor(Settings settings,LocalStore store)
    {
        this.settings=settings;this.store=store;templates=settings.Templates.Select(t=>new FolderTemplate{Name=t.Name,Folders=[..t.Folders]}).ToList();
        Style=(Style)FindResource(typeof(Window));
        Title="Folder template editor | Powered by Tweest";Width=830;Height=690;MinWidth=740;MinHeight=600;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var root=new DockPanel{Margin=new(24)};
        var footer=new StackPanel();footer.Children.Add(Row(B("New template",()=>{templates.Add(new(){Name="New template"});Reload(templates.Count-1);}),B("Import JSON",Import),B("Export JSON",Export),B("Save templates",Save,true)));footer.Children.Add(new TextBlock{Text="Powered by Tweest • Offline",FontSize=11,Margin=new(0,12,0,0)});DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
        var top=new StackPanel();top.Children.Add(new TextBlock{Text="Shape your next project.",FontSize=26,FontWeight=FontWeights.SemiBold,Margin=new(0,0,0,12)});top.Children.Add(choices);top.Children.Add(new TextBlock{Text="Template name",FontSize=12});top.Children.Add(templateName);top.Children.Add(Row(folderName,B("Add root",()=>Add(false)),B("Add child",()=>Add(true))));top.Children.Add(Row(B("Rename",Rename),B("Remove",Remove),B("Move up",()=>Move(-1)),B("Move down",()=>Move(1)),B("Nest",Nest),B("Unnest",Unnest)));DockPanel.SetDock(top,Dock.Top);root.Children.Add(top);root.Children.Add(tree);Content=root;
        choices.SelectionChanged+=(s,e)=>{if(choices.SelectedIndex>=0){templateName.Text=Current.Name;RefreshTree();}};
        templateName.LostFocus+=(s,e)=>{if(choices.SelectedIndex>=0)Current.Name=templateName.Text.Trim();};
        tree.SelectedItemChanged+=(s,e)=>{if(tree.SelectedItem is TreeViewItem item)folderName.Text=Path.GetFileName((string)item.Tag);};Reload(0);
    }
    private static StackPanel Row(params UIElement[] items){var s=new StackPanel{Orientation=Orientation.Horizontal};foreach(var i in items)s.Children.Add(i);return s;}
    private Button B(string text,Action action,bool primary=false){var b=new Button{Content=text,Padding=new(11,9,11,9)};if(primary)b.Style=(Style)FindResource("Primary");b.Click+=(s,e)=>{try{action();}catch(Exception ex){MessageBox.Show(this,ex.Message,"Template editor",MessageBoxButton.OK,MessageBoxImage.Warning);}};return b;}
    private void Reload(int selected){choices.ItemsSource=null;choices.ItemsSource=templates.Select(t=>t.Name).ToList();choices.SelectedIndex=selected;}
    private string Selected()=>tree.SelectedItem is TreeViewItem item?(string)item.Tag:throw new IOException("Select a folder first.");
    private void RefreshTree()
    {
        tree.Items.Clear();var map=new Dictionary<string,TreeViewItem>(StringComparer.OrdinalIgnoreCase);
        foreach(var path in Current.Folders)
        {
            string current="";
            foreach(var part in Safety.Relative(path).Split(Path.DirectorySeparatorChar))
            {
                string parent=current;current=current.Length==0?part:Path.Combine(current,part);
                if(map.ContainsKey(current))continue;var node=new TreeViewItem{Header=part,Tag=current,IsExpanded=true};map[current]=node;if(parent.Length==0)tree.Items.Add(node);else map[parent].Items.Add(node);
            }
        }
    }
    private void Add(bool child)
    {string name=Safety.Segment(folderName.Text);string path=child?Path.Combine(Selected(),name):name;if(Current.Folders.Contains(path,StringComparer.OrdinalIgnoreCase))throw new IOException("A folder with that name already exists.");Current.Folders.Add(path);RefreshTree();}
    private void Replace(string from,string to)
    {
        if(Safety.Within(to,from))throw new IOException("A folder cannot be nested inside itself.");
        if(Current.Folders.Any(p=>p.Equals(to,StringComparison.OrdinalIgnoreCase)))throw new IOException("A folder with that name exists.");
        Current.Folders=Current.Folders.Select(p=>Safety.Within(p,from)?to+p[from.Length..]:p).ToList();RefreshTree();
    }
    private void Rename(){var path=Selected();var name=Safety.Segment(folderName.Text);Replace(path,Path.Combine(Path.GetDirectoryName(path)??"",name));}
    private void Remove(){var path=Selected();Current.Folders.RemoveAll(p=>Safety.Within(p,path));RefreshTree();}
    private void Move(int delta)
    {
        string path=Selected(),parent=Path.GetDirectoryName(path)??"";
        var siblings=Current.Folders.Where(p=>(Path.GetDirectoryName(p)??"").Equals(parent,StringComparison.OrdinalIgnoreCase)).ToList();
        int index=siblings.FindIndex(p=>p.Equals(path,StringComparison.OrdinalIgnoreCase));if(index<0||index+delta<0||index+delta>=siblings.Count)return;
        string other=siblings[index+delta];var block=Current.Folders.Where(p=>Safety.Within(p,path)).ToList();Current.Folders.RemoveAll(p=>Safety.Within(p,path));int target=delta<0?Current.Folders.IndexOf(other):Current.Folders.FindLastIndex(p=>Safety.Within(p,other))+1;Current.Folders.InsertRange(target,block);RefreshTree();
    }
    private void Nest()
    {
        string path=Selected(),parent=Path.GetDirectoryName(path)??"";var siblings=Current.Folders.Where(p=>(Path.GetDirectoryName(p)??"")==parent).ToList();int i=siblings.IndexOf(path);if(i<=0)throw new IOException("Move this folder below a sibling before nesting it.");Replace(path,Path.Combine(siblings[i-1],Path.GetFileName(path)));
    }
    private void Unnest(){string path=Selected();string? parent=Path.GetDirectoryName(path);if(string.IsNullOrEmpty(parent))return;Replace(path,Path.Combine(Path.GetDirectoryName(parent)??"",Path.GetFileName(path)));}
    private void Save()
    {
        Current.Name=templateName.Text.Trim();if(templates.Any(t=>string.IsNullOrWhiteSpace(t.Name)))throw new IOException("Give every template a name.");foreach(var t in templates)t.Folders=t.Folders.Select(Safety.Relative).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        settings.Templates=templates;store.SaveSettings(settings);DialogResult=true;
    }
    private void Import()
    {
        var d=new OpenFileDialog{Filter="Folder template (*.json)|*.json"};if(d.ShowDialog(this)!=true)return;
        var t=LocalStore.Read<FolderTemplate>(d.FileName)??throw new IOException("The template is empty.");t.Folders=t.Folders.Select(Safety.Relative).Distinct(StringComparer.OrdinalIgnoreCase).ToList();if(string.IsNullOrWhiteSpace(t.Name))throw new IOException("The template needs a name.");templates.Add(t);Reload(templates.Count-1);
    }
    private void Export()
    {
        Current.Name=templateName.Text.Trim();var d=new SaveFileDialog{Filter="Folder template (*.json)|*.json",FileName=Safety.Segment(Current.Name)+".json"};if(d.ShowDialog(this)==true)LocalStore.Write(d.FileName,Current);
    }
}

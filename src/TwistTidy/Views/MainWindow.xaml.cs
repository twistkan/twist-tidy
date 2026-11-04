using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using TwistTidy.Core.Models;
using TwistTidy.Core.Services;
using TwistTidy.ViewModels;

namespace TwistTidy.Views;
public partial class MainWindow : Window
{
    private readonly MainViewModel vm=new();
    private string current="Home";
    private Batch? undoBatch;
    private string lastProject="";
    private TextBlock? recovered;
    private TextBox? sourceBox;
    private TextBox? destinationBox;
    private readonly List<string> warnings=[];
    public MainWindow()
    {
        InitializeComponent();DataContext=vm;Icon=(ImageSource)FindResource("Logo");ApplyTheme();ShowPage("Home");
        if(vm.Store.Warnings.Count>0)vm.Status=string.Join(" ",vm.Store.Warnings);
        foreach(var button in Navigation.Children.OfType<Button>())
        {
            string label=((string)button.Content)[5..].Trim();
            string geometry=(string)button.Tag switch{
                "Home"=>"M2,10 L10,3 18,10 M5,9 V18 H15 V9 M9,18 V12 H12 V18",
                "Sorter"=>"M2,6 H8 L10,8 H18 V18 H2 Z M4,4 H9 L11,6 H17",
                "Duplicates"=>"M3,3 H13 V14 H3 Z M7,7 H17 V18 H7",
                "Builder"=>"M2,4 H9 L11,7 H18 V18 H2 Z M10,10 V16 M7,13 H13",
                "History"=>"M4,8 C8,-1 20,6 16,14 C13,21 4,18 3,13 M2,3 V9 H8 M10,6 V11 L13,13",
                "Settings"=>"M3,5 H17 M3,10 H17 M3,15 H17 M7,2 V8 M13,7 V13 M6,12 V18",
                _=>"M10,2 C21,2 21,18 10,18 C-1,18 -1,2 10,2 M10,8 V15 M10,5 V6"};
            var icon=new System.Windows.Shapes.Path{Data=Geometry.Parse(geometry),StrokeThickness=1.6,Width=20,Height=20,Stretch=Stretch.Uniform,Margin=new(0,0,12,0)};icon.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty,"Foreground");
            button.Content=Horizontal(icon,new TextBlock{Text=label,VerticalAlignment=VerticalAlignment.Center});
        }
        Closing+=(s,e)=>{if(vm.Busy){e.Cancel=true;vm.Status="Cancel the current operation before closing. Completed actions remain in History.";}};
    }
    private static TextBlock Text(string value,double size=14,bool bold=false)=>new(){Text=value,FontSize=size,FontWeight=bold?FontWeights.SemiBold:FontWeights.Normal,Margin=new(0,0,0,8)};
    private Button Button(string label,Action action,bool primary=false)
    {var b=new Button{Content=label};if(primary)b.Style=(Style)FindResource("Primary");b.Click+=(s,e)=>{try{action();}catch(Exception ex){Error(ex);}};return b;}
    private void Ready(FrameworkElement element)=>element.SetBinding(IsEnabledProperty,new Binding(nameof(vm.Ready)));
    private Border Card(UIElement child,Thickness? padding=null)
    {var card=new Border{BorderThickness=new(1),CornerRadius=new(16),Padding=padding??new(22),Child=child,Margin=new(0,0,0,14)};card.SetResourceReference(Border.BackgroundProperty,"Surface");card.SetResourceReference(Border.BorderBrushProperty,"Line");return card;}
    private static StackPanel Stack(params UIElement[] children){var p=new StackPanel();foreach(var c in children)p.Children.Add(c);return p;}
    private static StackPanel Horizontal(params UIElement[] children){var p=Stack(children);p.Orientation=Orientation.Horizontal;return p;}
    private TextBox Input(string text="",double width=double.NaN){var b=new TextBox{Text=text,Width=width};return b;}
    private static ComboBox Combo(IEnumerable<string> items,int index=0,double width=double.NaN){var c=new ComboBox{ItemsSource=items.ToArray(),SelectedIndex=index,Width=width};return c;}
    private static UIElement Field(string label,UIElement control)
    {var caption=Text(label,12,true);caption.MinHeight=28;var field=Stack(caption,control);if(control is FrameworkElement element&&!double.IsNaN(element.Width))field.Width=element.Width;return field;}
    private UIElement FolderField(string label,TextBox box)
    {
        var d=new DockPanel();var browse=Button("Browse",()=>{var dialog=new OpenFolderDialog{Title=label,Multiselect=false};if(dialog.ShowDialog(this)==true)box.Text=dialog.FolderName;});DockPanel.SetDock(browse,Dock.Right);d.Children.Add(browse);d.Children.Add(box);return Field(label,d);
    }
    private void Navigate(object sender,RoutedEventArgs e)=>ShowPage((string)((Button)sender).Tag);
    private void ShowPage(string name)
    {
        if(vm.Busy)return;current=name;undoBatch=null;vm.SetRows([]);warnings.Clear();recovered=null;
        foreach(var b in Navigation.Children.OfType<Button>())b.Background=(Brush)FindResource((string)b.Tag==name?"Accent":"Surface2");
        (PageTitle.Text,Subtitle.Text)=name switch{
            "Sorter"=>("A place for every frame.","Scan your footage, shape the folder pattern, and review every move."),
            "Duplicates"=>("Same content. More clarity.","Compare bytes with SHA-256, choose what stays, and review the rest."),
            "Builder"=>("Start with a clean structure.","Build the folders your next production needs, ready for the first frame."),
            "History"=>("Every decision has a way back.","Review past batches and preview a safe undo."),
            "Settings"=>("Make order your own.","Local preferences, sorting patterns, and reusable production templates."),
            "About"=>("Made for your creative workspace.","TwistTidy 1.0.0 • Native Windows • Fully offline"),
            _=>("Your next project starts here.","Give every file a place, and keep every decision yours.")};
        Page.Content=name switch{"Sorter"=>Sorter(),"Duplicates"=>Duplicates(),"Builder"=>Builder(),"History"=>History(),"Settings"=>SettingsPage(),"About"=>About(),_=>Home()};
        Page.BeginAnimation(OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(160)));
    }
    private UIElement Home()
    {
        var history=vm.Store.History();var top=Stack(Text("LESS FILE CHAOS. MORE CREATIVE FLOW.",11,true),Text("Your files, in a better place.",30,true),Text("Sort a shoot. Find repeated files. Set up the next job.\nEvery change starts with a preview and stays in your local history.",16));
        var cards=new Grid();for(int i=0;i<3;i++)cards.ColumnDefinitions.Add(new());
        var features=new[]{("01  /  ORGANIZE","Footage Sorter","From mixed camera cards to a clear, consistent folder structure.","Sorter","Sort footage"),("02  /  RECLAIM","Duplicate Finder","Find identical files by their contents and choose the keeper.","Duplicates","Find duplicates"),("03  /  CREATE","Project Builder","A fresh production folder, with a place for every part of the job.","Builder","Build a project")};
        for(int i=0;i<3;i++){var f=features[i];var c=Card(Stack(Text(f.Item1,11,true),Text(f.Item2,22,true),Text(f.Item3),Button(f.Item5,()=>ShowPage(f.Item4),true)));c.Margin=new(0,0,i<2?14:0,14);Grid.SetColumn(c,i);cards.Children.Add(c);}
        var latest=history.FirstOrDefault();
        var stats=Card(Horizontal(Stat(history.Count.ToString("N0"),"Batches in local history"),Stat(history.Sum(b=>b.Count).ToString("N0"),"File actions recorded"),Stat(latest?.Date.ToString("dd MMM, HH:mm")??"Fresh workspace","Last activity")));
        return new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Content=Stack(Card(top,new(28)),cards,stats,Text("PREVIEW   →   APPROVE   →   UNDO",12,true),Text("Original files stay untouched during scans. Undo protects later edits and restores recycled items while they remain available.",12))};
    }
    private static UIElement Stat(string value,string label)=>new StackPanel{Width=265,Children={Text(value,27,true),Text(label,12)}};
    private void Invalidate(object? s=null,EventArgs? e=null)
    {if(vm.Busy)return;vm.SetRows([]);if(recovered!=null)recovered.Text="0 B";vm.Status="Options changed. Build a fresh preview before approving.";}
    private void Watch(TextBox box)=>box.TextChanged+=Invalidate;
    private UIElement Sorter()
    {
        sourceBox=Input();destinationBox=Input(lastProject);var source=sourceBox;var destination=destinationBox;
        var pattern=Combo(vm.Settings.Presets);pattern.IsEditable=true;pattern.IsTextSearchEnabled=false;pattern.Text=vm.Settings.Presets.FirstOrDefault()??"{type}/{date}/{source}";
        Watch(source);Watch(destination);pattern.AddHandler(TextBox.TextChangedEvent,new TextChangedEventHandler((s,e)=>Invalidate()));pattern.SelectionChanged+=(s,e)=>Invalidate();
        var tokens=Horizontal();foreach(string token in new[]{"{type}","{date}","{source}","{resolution}"})tokens.Children.Add(Button(token,()=>{pattern.Text=pattern.Text.TrimEnd('/')+"/"+token;}));
        tokens.Children.Add(Button("Save preset",()=>{Safety.Relative(pattern.Text.Replace("{type}","Footage").Replace("{date}","2026-10-26").Replace("{source}","Drone").Replace("{resolution}","1920x1080"));if(!vm.Settings.Presets.Contains(pattern.Text)){vm.Settings.Presets.Add(pattern.Text);vm.Store.SaveSettings(vm.Settings);vm.Status="Preset saved locally.";}}));
        var options=Stack(FolderField("SCAN FOLDER • includes subfolders",source),FolderField("DESTINATION • sorted files go here",destination),Field("FOLDER PATTERN • / separates folders",pattern),tokens);
        var scan=Button("Scan and preview",async()=>
        {
            string src=source.Text,dest=destination.Text,pat=pattern.Text;Safety.Check(src);Safety.Check(dest);
            await Run(async(ct,p)=>
            {
                var result=await Task.Run(()=>{var scanner=new Scanner();var excluded=vm.Settings.ExcludedFolders.ToList();if(Safety.Within(Path.GetFullPath(dest),Path.GetFullPath(src))&&!dest.Equals(src,StringComparison.OrdinalIgnoreCase))excluded.Add(Path.GetFullPath(dest));var files=scanner.Scan(src,excluded,ct,p);return (Rows:Planner.Sort(files,dest,pat,vm.Settings,ct,p),Warnings:scanner.Warnings);},ct);
                warnings.AddRange(result.Warnings);vm.SetRows(result.Rows);vm.Status=$"Preview ready. {result.Rows.Count:N0} media files planned. {warnings.Count} scan warnings.";
            });
        },true);
        options.Children.Add(scan);Ready(options);return Workspace(options);
    }
    private UIElement Duplicates()
    {
        var source=Input();var review=Input();var min=Input("1",80);var extensions=Input("",200);var keep=Combo(["Oldest modified file","Deepest organized folder"],0,225);var action=Combo(["Send to Recycle Bin","Move to review folder"],0,220);
        recovered=Text("0 B",36,true);var space=Horizontal(recovered,Stack(Text("POTENTIALLY RECLAIMABLE",11,true),Text("Recycle Bin space frees when emptied.\nEmptying it removes the option to restore.",11)));space.Children[1].SetValue(MarginProperty,new Thickness(22,4,0,0));
        foreach(var b in new[]{source,min,extensions,review})Watch(b);keep.SelectionChanged+=(s,e)=>Invalidate();action.SelectionChanged+=(s,e)=>Invalidate();
        var options=Stack(space,FolderField("SCAN FOLDER",source),Horizontal(Field("MINIMUM SIZE (MB)",min),Field("EXTENSIONS • empty means all",extensions),Field("SUGGESTED KEEPER",keep)),Horizontal(Field("DUPLICATE ACTION",action),new TextBlock{Text="Excluded folders follow Settings.",VerticalAlignment=VerticalAlignment.Center}),FolderField("REVIEW FOLDER • used only for move action",review));
        options.Children.Add(Button("Find duplicates",async()=>
        {
            if(!double.TryParse(min.Text,out double mb)||mb<0||double.IsNaN(mb)||double.IsInfinity(mb)||mb>long.MaxValue/1048576d)throw new IOException("Enter a valid minimum size in MB.");
            string src=source.Text,ext=extensions.Text,dest=review.Text;bool oldest=keep.SelectedIndex==0,move=action.SelectedIndex==1;Safety.Check(src);if(move)Safety.Check(dest);
            await Run(async(ct,p)=>
            {
                var result=await Task.Run(()=>{var scanner=new Scanner();var excludes=vm.Settings.ExcludedFolders.ToList();if(move)excludes.Add(Path.GetFullPath(dest));var files=scanner.Scan(src,excludes,ct,p);var rows=Planner.Duplicates(files,(long)(mb*1048576),ext,oldest,ct,p,scanner.Warnings);var reserved=new HashSet<string>(StringComparer.OrdinalIgnoreCase);foreach(var row in rows.Where(r=>r.Action!="Keep")){row.Action=move?"Move":"Recycle";row.Destination=move?Safety.Unique(Path.Combine(dest,Path.GetFileName(row.Source)),reserved):"Recycle Bin";}return (rows,scanner.Warnings);},ct);
                warnings.AddRange(result.Warnings);vm.SetRows(result.rows);UpdateRecoverable();vm.Status=$"{result.rows.Select(r=>r.Group).Distinct().Count()} duplicate groups found. Choose a keeper in each group.";
            });
        },true));Ready(options);
        var root=Workspace(options,true);
        vm.Rows.CollectionChanged+=(s,e)=>UpdateRecoverable();return root;
    }
    private void UpdateRecoverable(){if(recovered!=null)recovered.Text=Format.Bytes(vm.Rows.Where(r=>r.Selected&&r.Action!="Keep").Sum(r=>r.Size));}
    private UIElement Builder()
    {
        var parent=Input();var client=Input("",210);var project=Input("",230);var date=new DatePicker{SelectedDate=DateTime.Today,Width=155};var template=Combo(vm.Settings.Templates.Select(t=>t.Name),0,220);var type=Combo(["Video production","Drone shoot","Documentary","Commercial","Other"],0,200);var readme=new CheckBox{Content="Include project README",IsChecked=true};
        var namePattern=Input("{date}_{client}_{project}");
        foreach(var b in new[]{parent,client,project,namePattern})Watch(b);date.SelectedDateChanged+=(s,e)=>Invalidate();template.SelectionChanged+=(s,e)=>Invalidate();type.SelectionChanged+=(s,e)=>Invalidate();readme.Click+=(s,e)=>Invalidate();
        var options=Stack(FolderField("PARENT FOLDER",parent),Horizontal(Field("CLIENT",client),Field("PROJECT",project),Field("DATE",date)),Horizontal(Field("FOLDER TEMPLATE",template),Field("PROJECT TYPE",type),readme));
        options.Children.Add(Field("PROJECT FOLDER NAME • {date}, {client}, {project}, {type}",namePattern));
        options.Children.Add(Button("Preview project folders",()=>
        {
            var chosen=vm.Settings.Templates[template.SelectedIndex];var rows=Planner.Project(parent.Text,client.Text,project.Text,date.SelectedDate??DateTime.Today,type.Text,chosen,readme.IsChecked==true,namePattern.Text);lastProject=rows.First().Destination;vm.SetRows(rows);vm.Status="Project preview ready. Untick any folders you do not need. Selected children include their parent folders.";
        },true));
        options.Children.Add(Button("Manage folder templates",()=>{if(new TemplateEditor(vm.Settings,vm.Store){Owner=this}.ShowDialog()==true){template.ItemsSource=vm.Settings.Templates.Select(t=>t.Name).ToList();template.SelectedIndex=0;Invalidate();}}));Ready(options);return Workspace(options);
    }
    private UIElement Workspace(UIElement options,bool duplicates=false)
    {
        var layout=new Grid();layout.RowDefinitions.Add(new(){Height=GridLength.Auto});layout.RowDefinitions.Add(new(){Height=GridLength.Auto});layout.RowDefinitions.Add(new(){Height=new(1,GridUnitType.Star)});layout.RowDefinitions.Add(new(){Height=GridLength.Auto});
        var config=new DockPanel{MaxHeight=340};
        if(options is StackPanel optionStack)
        {
            var scan=optionStack.Children.OfType<Button>().FirstOrDefault(b=>b.Style==FindResource("Primary"));
            if(scan!=null){optionStack.Children.Remove(scan);Ready(scan);DockPanel.SetDock(scan,Dock.Bottom);config.Children.Add(scan);}
        }
        var scroll=new ScrollViewer{Content=options,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};config.Children.Add(scroll);layout.Children.Add(Card(config,new(18)));
        var summary=new TextBlock{Margin=new(0,0,0,10),FontWeight=FontWeights.SemiBold};summary.SetBinding(TextBlock.TextProperty,new Binding(nameof(vm.Summary)));Grid.SetRow(summary,1);layout.Children.Add(summary);
        var grid=new DataGrid{ItemsSource=vm.Rows};
        grid.Columns.Add(new DataGridCheckBoxColumn{Header="Use",Binding=new Binding("Selected"){Mode=BindingMode.TwoWay,UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged},Width=48});
        if(duplicates)grid.Columns.Add(Col("Group","Group",85));
        grid.Columns.Add(Col("File","File",150));grid.Columns.Add(Col("From","Source",new DataGridLength(1,DataGridLengthUnitType.Star)));grid.Columns.Add(Col("To","Destination",new DataGridLength(1,DataGridLengthUnitType.Star)));grid.Columns.Add(Col("Action","Action",duplicates?88:120));grid.Columns.Add(Col("Size","SizeText",75));
        Grid.SetRow(grid,2);layout.Children.Add(grid);Ready(grid);
        var info=Text("Select a row to inspect its metadata and complete paths.",11);info.MaxHeight=62;
        var image=new Image{Width=94,Height=62,Stretch=Stretch.Uniform,Margin=new(0,0,12,0),Visibility=Visibility.Collapsed};
        grid.SelectionChanged+=(s,e)=>
        {
            image.Source=null;image.Visibility=Visibility.Collapsed;
            if(grid.SelectedItem is not PlanRow r)return;info.Text=$"{r.Note}\nFrom: {r.Source}\nTo: {r.Destination}";
            if(new[]{".jpg",".jpeg",".png",".bmp",".gif"}.Contains(Path.GetExtension(r.Source).ToLowerInvariant()))
            {try{var bmp=new BitmapImage();bmp.BeginInit();bmp.CacheOption=BitmapCacheOption.OnLoad;bmp.DecodePixelWidth=190;bmp.UriSource=new Uri(r.Source);bmp.EndInit();bmp.Freeze();image.Source=bmp;image.Visibility=Visibility.Visible;}catch{info.Text+="\nImage preview unavailable.";}}
        };
        var controls=Horizontal();var select=Button("Select all",()=>{foreach(var r in vm.Rows.Where(r=>r.Action!="Keep"))r.Selected=true;UpdateRecoverable();});var clear=Button("Clear selection",()=>{foreach(var r in vm.Rows)r.Selected=false;UpdateRecoverable();});controls.Children.Add(select);controls.Children.Add(clear);
        if(duplicates)controls.Children.Add(Button("Keep selected file",()=>
        {
            if(grid.SelectedItem is not PlanRow chosen)return;
            var old=vm.Rows.FirstOrDefault(r=>r.Group==chosen.Group&&r.Action=="Keep");if(old==chosen)return;
            var sample=vm.Rows.First(r=>r.Group==chosen.Group&&r.Action!="Keep");string act=sample.Action,dest=sample.Destination;
            if(old!=null){old.Action=act;old.Selected=true;old.Destination=act=="Move"?Safety.Unique(Path.Combine(Path.GetDirectoryName(dest)!,Path.GetFileName(old.Source)),vm.Rows.Where(r=>r!=old&&r!=chosen).Select(r=>r.Destination).ToHashSet(StringComparer.OrdinalIgnoreCase)):"Recycle Bin";old.Note="Identical content | SHA-256 match";}
            chosen.Action="Keep";chosen.Selected=false;chosen.Destination="";chosen.Note="Your keeper | SHA-256 match";grid.Items.Refresh();vm.UpdateSummary();UpdateRecoverable();
        }));
        controls.Children.Add(Button("Scan warnings",()=>MessageBox.Show(warnings.Count==0?"No scan warnings.":string.Join("\n",warnings.Take(100)),"Scan warnings",MessageBoxButton.OK,MessageBoxImage.Information)));
        controls.Children.Add(Button("Approve selected",async()=>{grid.CommitEdit(DataGridEditingUnit.Cell,true);grid.CommitEdit(DataGridEditingUnit.Row,true);await Approve();},true));Ready(controls);
        var bottom=Stack(Horizontal(image,info),controls);Grid.SetRow(bottom,3);layout.Children.Add(bottom);
        foreach(var r in vm.Rows)r.PropertyChanged+=(s,e)=>UpdateRecoverable();
        vm.Rows.CollectionChanged+=(s,e)=>{if(e.NewItems!=null)foreach(PlanRow r in e.NewItems)r.PropertyChanged+=(a,b)=>UpdateRecoverable();};
        return layout;
    }
    private static DataGridTextColumn Col(string title,string path,DataGridLength width)=>new(){Header=title,Binding=new Binding(path),Width=width,IsReadOnly=true};
    private async Task Approve()
    {
        var selected=vm.Rows.Where(r=>r.Selected&&r.Action!="Keep").ToList();if(selected.Count==0)throw new IOException("Select rows in the preview first.");vm.UpdateSummary();
        if(MessageBox.Show(this,vm.Summary+"\n\n"+(undoBatch!=null?"Apply this undo preview?":"Apply exactly the selected preview rows?")+"\nExisting files will never be overwritten.",undoBatch!=null?"Approve undo":"Approve file operations",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
        await Run(async(ct,p)=>
        {
            var engine=new ExecutionService(vm.Store);
            if(undoBatch!=null)
            {var batch=undoBatch;await Task.Run(()=>engine.Undo(batch,selected,ct,p));vm.SetRows([]);vm.Status=$"{batch.State}. Open History for details.";}
            else
            {
                string title=current=="Builder"?"Project Builder":current=="Duplicates"?"Duplicate Finder":"Footage Sorter";var all=vm.Rows.ToList();var batch=await Task.Run(()=>engine.Execute(title,all,ct,p));vm.SetRows([]);vm.Status=$"{batch.State}. {batch.Entries.Count(e=>e.State=="Done")} actions recorded. Undo is available in History.";
                if(current=="Builder" && batch.State=="Completed")lastProject=batch.Entries.FirstOrDefault(e=>e.Action=="Create folder")?.Destination??lastProject;
                if(batch.Entries.Any(e=>e.Error.Length>0))MessageBox.Show(this,string.Join("\n\n",batch.Entries.Where(e=>e.Error.Length>0).Take(12).Select(e=>$"{e.Source}{e.Destination}\n{e.Error}")),"Batch issues",MessageBoxButton.OK,MessageBoxImage.Warning);
            }
        });
        if(current=="Builder" && !string.IsNullOrEmpty(lastProject)&&Directory.Exists(lastProject))
        {if(MessageBox.Show(this,"Open the Footage Sorter with this project's footage folder as destination?","Project ready",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes){var folder=Directory.EnumerateDirectories(lastProject).FirstOrDefault(d=>Path.GetFileName(d).Contains("Footage"))??lastProject;lastProject=folder;ShowPage("Sorter");}}
    }
    private async Task Run(Func<CancellationToken,IProgress<ScanProgress>,Task> work)
    {
        if(vm.Busy)return;vm.Busy=true;vm.Cancellation=new();Progress.Visibility=CancelButton.Visibility=Visibility.Visible;
        var p=new Progress<ScanProgress>(v=>vm.Status=v.Message);
        try{await work(vm.Cancellation.Token,p);}
        catch(OperationCanceledException){vm.SetRows([]);vm.Status="Cancelled. Completed operations remain in History.";}
        catch(Exception e){Error(e);}
        finally{vm.Cancellation.Dispose();vm.Cancellation=null;vm.Busy=false;Progress.Visibility=CancelButton.Visibility=Visibility.Collapsed;}
    }
    private void Cancel(object sender,RoutedEventArgs e){vm.Cancellation?.Cancel();vm.Status="Cancelling after the current safe operation...";}
    private void Error(Exception e){vm.Status=e.Message;MessageBox.Show(this,e.Message,"TwistTidy",MessageBoxButton.OK,MessageBoxImage.Warning);}
    private UIElement History()
    {
        var history=vm.Store.History();var list=new ListBox{ItemsSource=history};var details=new TextBox{IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Height=180};
        list.SelectionChanged+=(s,e)=>{if(list.SelectedItem is Batch b)details.Text=string.Join("\n",b.Entries.Select(x=>$"{x.State} | {x.Action} | {x.Source} → {x.Destination}"+(x.Error.Length>0?$"\n{x.Error}":"")));};
        var preview=Button("Preview undo for selected batch",async()=>
        {
            if(list.SelectedItem is not Batch b)throw new IOException("Choose a batch from history.");
            await Run(async(ct,p)=>{var rows=await Task.Run(()=>new ExecutionService(vm.Store).UndoPreview(b),ct);undoBatch=b;vm.SetRows(rows);PageTitle.Text="Preview the way back.";Subtitle.Text="Edited files are protected. Restore name clashes receive a numeric suffix.";Page.Content=Workspace(Text($"UNDO • {b.Title} • {b.Date:g}",18,true));vm.Status="Review the undo rows, then approve selected.";});
        },true);Ready(preview);
        var grid=new DockPanel();var bottom=Stack(preview,details,Text("Recycle Bin restoration requires the item to still be in the Recycle Bin. Empty folders are removed only when they contain no files.",12));DockPanel.SetDock(bottom,Dock.Bottom);grid.Children.Add(bottom);
        grid.Children.Add(history.Count==0?Card(Stack(Text("A clear history starts here.",22,true),Text("Approved batches appear here with their file paths and undo status. Scanning alone does not create a batch."))):list);return grid;
    }
    private UIElement SettingsPage()
    {
        var theme=Combo(["Dark","Light"],vm.Settings.Theme=="Light"?1:0,170);var accent=Input(vm.Settings.Accent,155);
        var patterns=Input(string.Join("\n",vm.Settings.CameraPatterns.Select(p=>$"{p.Prefix}={p.Source}")));patterns.AcceptsReturn=true;patterns.Height=150;patterns.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;
        var presets=Input(string.Join("\n",vm.Settings.Presets));presets.AcceptsReturn=true;presets.Height=130;presets.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;
        var excludes=Input(string.Join("\n",vm.Settings.ExcludedFolders));excludes.AcceptsReturn=true;excludes.Height=100;excludes.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;
        var save=Button("Save settings",()=>
        {
            _=ColorConverter.ConvertFromString(accent.Text);var camera=new List<CameraPattern>();
            foreach(var line in Lines(patterns.Text)){var bits=line.Split('=',2);if(bits.Length!=2||bits[0].Trim().Length==0)throw new IOException("Camera rules use PREFIX=Source, one rule per line.");camera.Add(new(){Prefix=bits[0].Trim(),Source=Safety.Segment(bits[1])});}
            foreach(var preset in Lines(presets.Text)){var check=preset.Replace("{type}","Footage").Replace("{date}","2026-10-26").Replace("{source}","Drone").Replace("{resolution}","1920x1080");if(check.Contains('{')||check.Contains('}'))throw new IOException("A preset contains an unknown token.");Safety.Relative(check);}
            vm.Settings.Theme=theme.Text;vm.Settings.Accent=accent.Text;vm.Settings.CameraPatterns=camera;vm.Settings.Presets=Lines(presets.Text);vm.Settings.ExcludedFolders=Lines(excludes.Text);vm.Store.SaveSettings(vm.Settings);ApplyTheme();vm.Status="Settings saved on this PC.";
        },true);
        return new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Content=Stack(Card(Stack(Text("Appearance",20,true),Horizontal(Field("THEME",theme),Field("ACCENT • HEX COLOR",accent)))),Card(Stack(Text("Camera and source rules",20,true),Text("First matching filename prefix wins. C matches Sony-style C0001 filenames.",12),patterns)),Card(Stack(Text("Sorting presets",20,true),Text("One relative pattern per line. Tokens: {type}, {date}, {source}, {resolution}.",12),presets)),Card(Stack(Text("Excluded folders",20,true),Text("One folder name or absolute path per line. System folders and links are always skipped.",12),excludes)),Horizontal(save,Button("Edit folder templates",()=>new TemplateEditor(vm.Settings,vm.Store){Owner=this}.ShowDialog())))};
    }
    private static List<string> Lines(string text)=>text.Replace("\r","").Split('\n',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).ToList();
    private UIElement About()=>Card(Stack(new Image{Source=(ImageSource)FindResource("Logo"),Width=96,Height=96,HorizontalAlignment=HorizontalAlignment.Left},Text("TwistTidy",36,true),Text("Order, twisted your way.",18),Text("Version 1.0.0\nPowered by Tweest",14,true),Text("TwistTidy never connects to the internet. There are no accounts, analytics, update checks, or cloud services. Your settings, templates and action history are JSON files stored in %AppData%\\TwistTidy."),Text("Built with C# and .NET 8 WPF. Icons and logo are drawn locally. Typography uses Windows' Segoe UI fallback. Windows' installed offline metadata handlers supply media information when available."),Text("Scans do not modify files. Every operation is previewed before approval. Moves and project creation are recorded for undo. Recycled items can be restored while they remain available. Changed files are left in place to protect your edits."),Text("Third-party packages: none. No bundled external codecs.",12)));
    private void ApplyTheme()
    {
        bool light=vm.Settings.Theme=="Light";var resources=Application.Current.Resources;
        foreach(var (key,color) in new[]{("Background",light?"#F1F3F8":"#0F1115"),("Surface",light?"#FFFFFF":"#171A21"),("Surface2",light?"#E4E8F0":"#212630"),("Foreground",light?"#171B27":"#F2F3F8"),("Muted",light?"#536078":"#A2ABBD"),("Line",light?"#CED4E1":"#303643")})resources[key]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        try{var color=(Color)ColorConverter.ConvertFromString(vm.Settings.Accent);resources["Accent"]=new SolidColorBrush(color);resources["Brand"]=new LinearGradientBrush(color,(Color)ColorConverter.ConvertFromString("#FB923C"),45);}catch{ }
    }
    internal async Task RenderScreens(string directory)
    {
        Directory.CreateDirectory(directory);
        foreach(string screen in new[]{"Home","Sorter","Duplicates","Builder","History","Settings","About"})
        {
            ShowPage(screen);await Task.Delay(200);UpdateLayout();
            var bitmap=new RenderTargetBitmap((int)ActualWidth,(int)ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(this);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var output=File.Create(Path.Combine(directory,screen+".png"));encoder.Save(output);
        }
        var editor=new TemplateEditor(vm.Settings,vm.Store){Owner=this};editor.Show();await Task.Delay(200);editor.UpdateLayout();var image=new RenderTargetBitmap((int)editor.ActualWidth,(int)editor.ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(editor);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using(var f=File.Create(Path.Combine(directory,"Templates.png")))png.Save(f);editor.Close();
        var setup=new SetupWindow{Owner=this};setup.Show();await Task.Delay(200);setup.UpdateLayout();var setupImage=new RenderTargetBitmap((int)setup.ActualWidth,(int)setup.ActualHeight,96,96,PixelFormats.Pbgra32);setupImage.Render(setup);var setupPng=new PngBitmapEncoder();setupPng.Frames.Add(BitmapFrame.Create(setupImage));using(var f=File.Create(Path.Combine(directory,"Setup.png")))setupPng.Save(f);setup.Close();
        vm.Settings.Theme="Light";ApplyTheme();ShowPage("Home");await Task.Delay(200);UpdateLayout();var light=new RenderTargetBitmap((int)ActualWidth,(int)ActualHeight,96,96,PixelFormats.Pbgra32);light.Render(this);var lightPng=new PngBitmapEncoder();lightPng.Frames.Add(BitmapFrame.Create(light));using(var f=File.Create(Path.Combine(directory,"Light.png")))lightPng.Save(f);
    }
}

using System.Collections.ObjectModel;
using AxoVisuals.Core.Batch;
using AxoVisuals.Core.Imaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
namespace AxoVisuals;
public sealed partial class MainWindow
{
    private readonly ObservableCollection<string> batchRows = [];
    private readonly List<string> batchFiles = [];
    private string? batchDirectory;
    private CancellationTokenSource? batchCancellation;
    private ListView? batchList;
    private void BuildBatch()
    {
        Inspector.Children.Add(Label("Batch processing", true));
        Inspector.Children.Add(Label("Each image is processed independently. Originals are preserved; name conflicts receive a numeric suffix."));
        AddButton("Add images…", async () =>
        {
            var picker = new FileOpenPicker(); WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
            foreach(var extension in ImageDocument.Extensions) picker.FileTypeFilter.Add(extension);
            var files = await picker.PickMultipleFilesAsync(); foreach(var file in files) AddBatchFile(file.Path);
        });
        AddButton("Clear list", () => { batchFiles.Clear(); batchRows.Clear(); return Task.CompletedTask; });
        AddButton("Choose output folder…", () =>
        {
            var folder = Infrastructure.SaveDialog.PickFolder(WinRT.Interop.WindowNative.GetWindowHandle(this));
            if(folder != null) { batchDirectory = folder; BuildInspector(); }
            return Task.CompletedTask;
        });
        Inspector.Children.Add(Label(batchDirectory ?? "No output folder selected"));
        var format = Choice("Format", ImagePipeline.Formats.Select(f=>f.Name));
        var quality = Number("Quality",85,1,100); quality.Visibility = Visibility.Collapsed;
        format.SelectionChanged += (_,_) => quality.Visibility = ImagePipeline.Formats[format.SelectedIndex].HasQuality ? Visibility.Visible : Visibility.Collapsed;
        var policy = Choice("Metadata", ["Keep metadata", "Privacy clean", "Remove GPS", "Remove all metadata"],1);
        var resize = new CheckBox { Content = "Fit within dimensions", IsChecked = false }; Inspector.Children.Add(resize);
        var width = Number("Maximum width",1920); var height = Number("Maximum height",1080);
        var resizeHint = Label("Aspect ratio is preserved. Smaller images are not enlarged.");
        Inspector.Children.Add(resizeHint);
        void UpdateResizeVisibility() => width.Visibility = height.Visibility = resizeHint.Visibility = resize.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        resize.Checked += (_, _) => UpdateResizeVisibility();
        resize.Unchecked += (_, _) => UpdateResizeVisibility();
        UpdateResizeVisibility();
        var progress = new ProgressBar { Minimum = 0, Maximum = 100 }; Inspector.Children.Add(progress);
        AddButton("Start batch", async () =>
        {
            if(batchFiles.Count == 0 || batchDirectory == null) throw new InvalidOperationException("Add images and choose an output folder.");
            var options = new BatchOptions(new ExportOptions(ImagePipeline.Formats[format.SelectedIndex].Name, (uint)quality.Value, (MetadataPolicy)policy.SelectedIndex), resize.IsChecked == true ? Positive(width.Value) : null, resize.IsChecked == true ? Positive(height.Value) : null);
            batchCancellation = new CancellationTokenSource();
            var updates = new Progress<BatchProgress>(p => { var index = batchFiles.IndexOf(p.Source); if(index >= 0) batchRows[index] = System.IO.Path.GetFileName(p.Source) + "  ·  " + p.Status; progress.Value = 100d*p.Index/p.Total; });
            try { var result = await BatchProcessor.RunAsync(batchFiles.ToArray(),batchDirectory,options,updates,batchCancellation.Token); exportNotice = $"Batch: {result.Succeeded} exported, {result.Failed} failed" + (result.Cancelled ? " · Cancelled" : ""); }
            finally { batchCancellation.Dispose(); batchCancellation = null; }
        });
        var cancel = new Button { Content = "Cancel batch", HorizontalAlignment = HorizontalAlignment.Stretch }; cancel.Click += (_,_) => batchCancellation?.Cancel(); Inspector.Children.Add(cancel);
        batchList = new ListView { ItemsSource = batchRows, SelectionMode = ListViewSelectionMode.None };
        BatchWorkspace.Children.Clear(); BatchWorkspace.RowDefinitions.Clear();
        BatchWorkspace.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        BatchWorkspace.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1,GridUnitType.Star) });
        BatchWorkspace.Children.Add(new TextBlock { Text = "Batch queue", FontSize = 16, Margin = new Thickness(0,0,0,16) });
        Grid.SetRow(batchList,1); BatchWorkspace.Children.Add(batchList);
        BatchWorkspace.Visibility = Visibility.Visible;
    }
    private void AddBatchFile(string path)
    {
        if(batchFiles.Contains(path,StringComparer.OrdinalIgnoreCase)) return;
        batchFiles.Add(path); batchRows.Add(System.IO.Path.GetFileName(path) + "  ·  Ready");
    }
}

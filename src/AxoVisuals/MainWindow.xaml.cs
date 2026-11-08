using AxoVisuals.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
namespace AxoVisuals;
public sealed partial class MainWindow : Window
{
    private readonly WorkspaceViewModel model = new();
    private bool busy; private string? exportNotice;
    public MainWindow()
    {
        InitializeComponent(); InitializeChrome(); Title = "AxoVisuals"; AppWindow.SetIcon(System.IO.Path.Combine(AppContext.BaseDirectory,"Assets","App.ico")); AppWindow.Resize(new Windows.Graphics.SizeInt32(1280, 820));
        AppWindow.Closing += (_, args) => { if(busy) { args.Cancel = true; backgroundCancellation?.Cancel(); batchCancellation?.Cancel(); StatusText.Text = "Finishing the current operation. Close again when ready."; } };
        Closed += (_, _) => { model.Dispose(); backgroundService.Dispose(); };
        Viewport.SizeChanged += (_,_) => { if(fitMode && model.Document != null) FitClick(this,new RoutedEventArgs()); };
        Viewport.ViewChanged += (_,_) => UpdateZoomLabel();
        Root.Loaded += async (_,_) =>
        {
            BuildInspector(); var files = Environment.GetCommandLineArgs().Skip(1).Where(System.IO.File.Exists).ToArray();
            if(files.Length == 1) await RunAsync(() => OpenAsync(files[0]));
            else if(files.Length > 1) { ToolList.SelectedIndex = 9; foreach(var file in files) AddBatchFile(file); }
        };
    }
    private bool fitMode = true;
    private void UpdateZoomLabel() { if(model.Document != null) ZoomText.Text = $"{ImageSurface.Width / model.Width * Viewport.ZoomFactor * (Root.XamlRoot?.RasterizationScale ?? 1) * 100:F0}%"; }
    private async Task RunAsync(Func<Task> work)
    {
        if (busy) return;
        busy = true; UpdateHistoryControls(); ToolList.IsEnabled = false; BusyProgress.Visibility = Visibility.Visible; StatusText.Text = "Working…";
        try { exportNotice = null; await work(); StatusText.Text = exportNotice ?? "Ready"; }
        catch (Exception ex) { StatusText.Text = Core.Infrastructure.UserErrors.Describe(ex); Core.Infrastructure.Diagnostics.Record("operation",ex); }
        finally { busy = false; ToolList.IsEnabled = true; BusyProgress.Visibility = Visibility.Collapsed; UpdateHistoryControls(); }
    }
    private async void OpenClick(object sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        var picker = new FileOpenPicker(); WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
        foreach (var extension in Core.Imaging.ImageDocument.Extensions) picker.FileTypeFilter.Add(extension);
        var file = await picker.PickSingleFileAsync(); if (file != null) await OpenAsync(file.Path);
    });
    private async Task OpenAsync(string path) { await model.OpenAsync(path); beforeBackground = null; replacementImage = null; await ShowPreviewAsync(); FileNameText.Text = System.IO.Path.GetFileName(path); BuildInspector(); EmptyState.Visibility = Visibility.Collapsed; }
    private async Task ShowPreviewAsync()
    {
        if (model.PreviewBytes == null) return;
        await DisplayBytesAsync(model.PreviewBytes);
    }
    private async Task DisplayBytesAsync(byte[] bytes)
    {
        using var stream = new InMemoryRandomAccessStream(); using (var writer = new DataWriter(stream.GetOutputStreamAt(0))) { writer.WriteBytes(bytes); await writer.StoreAsync(); }
        var source = new BitmapImage(); await source.SetSourceAsync(stream); PreviewImage.Source = source; FitClick(this, new RoutedEventArgs());
    }
    private void FitClick(object sender, RoutedEventArgs e) { if(model.Document == null) return; fitMode = true; var scale = Math.Min(Math.Max(100,Viewport.ActualWidth-20)/model.Width, Math.Max(100,Viewport.ActualHeight-20)/model.Height); ImageSurface.Width = model.Width * scale; ImageSurface.Height = model.Height * scale; CropRectangle.Visibility = Visibility.Collapsed; crop = null; Viewport.ChangeView(0, 0, 1); UpdateZoomLabel(); }
    private async void ActualClick(object sender, RoutedEventArgs e) => await RunAsync(async () => { if (model.Document == null) return; await DisplayBytesAsync(await model.FullPreviewAsync()); fitMode = false; ImageSurface.Width = model.Width / Root.XamlRoot.RasterizationScale; ImageSurface.Height = model.Height / Root.XamlRoot.RasterizationScale; CropRectangle.Visibility = Visibility.Collapsed; crop = null; Viewport.ChangeView(0, 0, 1); UpdateZoomLabel(); });
    private void OnDragOver(object sender, DragEventArgs e) { if(e.DataView.Contains(StandardDataFormats.StorageItems)) e.AcceptedOperation = DataPackageOperation.Copy; }
    private async void OnDrop(object sender, DragEventArgs e) => await RunAsync(async () => { var files = await e.DataView.GetStorageItemsAsync(); if(ToolList.SelectedIndex == 9 || files.Count > 1) { ToolList.SelectedIndex = 9; foreach(var file in files) AddBatchFile(file.Path); } else if (files.Count > 0) await OpenAsync(files[0].Path); });
    private void ToolChanged(object sender, SelectionChangedEventArgs e) { try { BuildInspector(); } catch(Exception ex) { Core.Infrastructure.Diagnostics.Record("inspector",ex); if(StatusText != null) StatusText.Text = Core.Infrastructure.UserErrors.Describe(ex); } }
    private async void ResetClick(object sender, RoutedEventArgs e) => await RunAsync(async () => { await model.ApplyAsync(null, true); await ShowPreviewAsync(); BuildInspector(); });
    private async void ExportClick(object sender, RoutedEventArgs e)
    {
        if(busy) return;
        if(model.Document == null) { StatusText.Text = "Open an image first."; return; }
        if(ToolList.SelectedIndex != 6) { ToolList.SelectedIndex = 6; StatusText.Text = "Review output settings, then choose Export image."; }
        else await RunAsync(ExportAsync);
    }
}





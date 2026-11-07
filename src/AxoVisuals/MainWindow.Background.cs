using AxoVisuals.Core.AI;
using AxoVisuals.Core.Imaging;
using ImageMagick;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
namespace AxoVisuals;
public sealed partial class MainWindow
{
    private readonly BackgroundRemovalService backgroundService = new();
    private CancellationTokenSource? backgroundCancellation;
    private byte[]? replacementImage;
    private byte[]? beforeBackground;
    private void BuildBackground()
    {
        Inspector.Children.Add(Label("U²-Net · local CPU inference"));
        Inspector.Children.Add(Label("The model is bundled. No image or metadata leaves this computer. Fine hair, glass and low-contrast edges may need a different source image."));
        var mode = Choice("Background", ["Transparent", "Solid color", "Replacement image"]);
        var color = Field("Color · #RRGGBB", "#FFFFFF"); color.Visibility = Visibility.Collapsed;
        var replacement = new Button { Content = replacementImage == null ? "Choose background image…" : "Replace background image…", Visibility = Visibility.Collapsed, HorizontalAlignment = HorizontalAlignment.Stretch };
        replacement.Click += async (_,_) => await RunAsync(async () =>
        {
            var picker = new FileOpenPicker(); foreach(var ext in ImageDocument.Extensions) picker.FileTypeFilter.Add(ext);
            WinRT.Interop.InitializeWithWindow.Initialize(picker,WinRT.Interop.WindowNative.GetWindowHandle(this));
            var file = await picker.PickSingleFileAsync(); if(file == null) return;
            replacementImage = await Task.Run(()=> { using var doc = ImageDocument.Open(file.Path); using var image = doc.CloneOriginal(); image.Strip(); image.Resize(new MagickGeometry(4096,4096) { Greater = true }); return image.ToByteArray(MagickFormat.Png); });
            replacement.Content = "Background image selected";
        });
        Inspector.Children.Add(replacement);
        mode.SelectionChanged += (_,_) => { color.Visibility = mode.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed; replacement.Visibility = mode.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed; };
        var feather = Number("Edge feather (px)",0.5,0,10); feather.SmallChange = 0.5;
        AddButton("Remove background", async () =>
        {
            if(model.Document == null) return;
            if(mode.SelectedIndex == 1 && !System.Text.RegularExpressions.Regex.IsMatch(color.Text,"^#[0-9a-fA-F]{6}$")) throw new InvalidOperationException("Use a color such as #FFFFFF.");
            if(mode.SelectedIndex == 2 && replacementImage == null) throw new InvalidOperationException("Choose a replacement image.");
            var backgroundColor = mode.SelectedIndex == 1 ? color.Text : null;
            var backgroundBytes = mode.SelectedIndex == 2 ? replacementImage : null;
            beforeBackground = model.PreviewBytes;
            backgroundCancellation = new CancellationTokenSource();
            try
            {
                var token = backgroundCancellation.Token;
                var edits = model.Edits.ToArray();
                var mask = await Task.Run(()=> { using var image = ImagePipeline.Render(model.Document,edits,token); return backgroundService.CreateMask(image,System.IO.Path.Combine(AppContext.BaseDirectory,"models","u2net.onnx"),token); },token);
                token.ThrowIfCancellationRequested();
                await ApplyEditAsync(new BackgroundEdit(mask,feather.Value,backgroundColor,backgroundBytes));
                model.Export = model.Export with { Format = "PNG" };
                exportNotice = "Background removed · PNG selected for transparency";
            }
            finally { backgroundCancellation.Dispose(); backgroundCancellation = null; }
        });
        var cancel = new Button { Content = "Cancel", HorizontalAlignment = HorizontalAlignment.Stretch }; cancel.Click += (_,_) => backgroundCancellation?.Cancel(); Inspector.Children.Add(cancel);
        AddButton("Show before", async () => { if(beforeBackground != null) await DisplayBytesAsync(beforeBackground); });
        AddButton("Show after", ShowPreviewAsync);
    }
}

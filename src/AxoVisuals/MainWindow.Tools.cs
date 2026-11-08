using AxoVisuals.Core.Imaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Storage.Pickers;
namespace AxoVisuals;
public sealed partial class MainWindow
{
    private static TextBlock Label(string text, bool heading = false) => new() { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = heading ? 14 : 12, Foreground = new SolidColorBrush(heading ? Microsoft.UI.Colors.White : Windows.UI.Color.FromArgb(255, 164, 170, 179)) };
    private void AddButton(string text, Func<Task> action)
    {
        var button = new Button { Content = text, HorizontalAlignment = HorizontalAlignment.Stretch };
        button.Click += async (_, _) => await RunAsync(action); Inspector.Children.Add(button);
    }
    private NumberBox Number(string header, double value, double minimum = 1, double maximum = 40000)
    {
        var box = new NumberBox { Header = header, Value = value, Minimum = minimum, Maximum = maximum, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact, SmallChange = 1 };
        Inspector.Children.Add(box); return box;
    }
    private ComboBox Choice(string header, IEnumerable<string> values, int selected = 0)
    {
        var box = new ComboBox { Header = header, HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach(var value in values) box.Items.Add(value);
        box.SelectedIndex = selected; Inspector.Children.Add(box); return box;
    }
    private Slider Adjustment(string header, double value, double minimum, double maximum)
    {
        Inspector.Children.Add(Label(header));
        var slider = new Slider { Minimum = minimum, Maximum = maximum, Value = value, StepFrequency = 1, SmallChange = 1, LargeChange = 10 };
        Inspector.Children.Add(slider); return slider;
    }
    private void BuildInspector()
    {
        if(Inspector == null) return;
        Inspector.Children.Clear();
        var tool = ToolList.SelectedIndex; var showInspector = model.Document != null || tool is 9 or 10; InspectorColumn.Width = new GridLength(showInspector ? 300 : 0); InspectorBorder.Visibility = showInspector ? Visibility.Visible : Visibility.Collapsed; BatchWorkspace.Visibility = Visibility.Collapsed;
        CropOverlay.IsHitTestVisible = tool == 2;
        CropRectangle.Visibility = Visibility.Collapsed;
        crop = null;
        if(tool == 9) { BuildBatch(); return; }
        if(tool == 10) { BuildAbout(); return; }
        if(model.Document == null) { Inspector.Children.Add(Label("Open an image to begin.")); return; }
        Inspector.Children.Add(Label(tool switch {1=>"Resize",2=>"Crop",3=>"Rotate / Flip",4=>"Adjustments",5=>"Filters",6=>"Convert / Compress",7=>"Metadata",8=>"Background",_=>"Image properties"}, true));
        if(tool == 0)
        {
            var info = model.Document.Info;
            Inspector.Children.Add(Label($"{model.Width:N0} × {model.Height:N0} px\n{info.Format}  ·  {info.FileSize / 1024d:N1} KB\n\n{model.Edits.Count} pending image operations"));
            Inspector.Children.Add(Label("Original files are never overwritten. Export creates a new image."));
            Inspector.Children.Add(Label("Ctrl + mouse wheel to zoom. Scroll to pan. Fit restores the full image."));
            Inspector.Children.Add(Label("For animated or multi-page images, only the first frame is processed."));
        }
        else if(tool == 1)
        {
            var width = Number("Width (px)", model.Width); var height = Number("Height (px)", model.Height);
            var aspect = new CheckBox { Content = "Preserve aspect ratio", IsChecked = true }; Inspector.Children.Add(aspect);
            bool updating = false;
            width.ValueChanged += (_, _) => { if(updating || aspect.IsChecked != true) return; updating = true; height.Value = Math.Max(1, Math.Round(width.Value * model.Height / model.Width)); updating = false; };
            height.ValueChanged += (_, _) => { if(updating || aspect.IsChecked != true) return; updating = true; width.Value = Math.Max(1, Math.Round(height.Value * model.Width / model.Height)); updating = false; };
            var percent = Number("Scale (%)", 100, 1, 1000);
            percent.ValueChanged += (_, _) => { updating = true; width.Value = Math.Round(model.Width * percent.Value / 100); height.Value = Math.Round(model.Height * percent.Value / 100); updating = false; };
            Inspector.Children.Add(Label("The dimensions above are the output size. Values larger than the current image will upscale."));
            AddButton("Apply resize", () => ApplyEditAsync(new ResizeEdit(Positive(width.Value), Positive(height.Value))));
        }
        else if(tool == 2)
        {
            var ratio = Choice("Aspect ratio", ["Free", "1:1", "4:3", "3:2", "16:9"]);
            ratio.SelectionChanged += (_, _) => cropRatio = ratio.SelectedIndex switch {1=>1,2=>4d/3,3=>1.5,4=>16d/9,_=>0};
            cropRatio = 0;
            Inspector.Children.Add(Label("Drag on the image to select a crop. Drawing again replaces the selection."));
            cropText = Label("No selection"); Inspector.Children.Add(cropText);
            AddButton("Apply crop", async () => { if(crop == null) throw new InvalidOperationException("Draw a crop selection first."); await ApplyEditAsync(crop); });
            AddButton("Reset selection", () => { crop = null; CropRectangle.Visibility = Visibility.Collapsed; cropText.Text = "No selection"; return Task.CompletedTask; });
        }
        else if(tool == 3)
        {
            AddButton("Rotate 90° left", () => ApplyEditAsync(new RotateEdit(-90)));
            AddButton("Rotate 90° right", () => ApplyEditAsync(new RotateEdit(90)));
            AddButton("Flip horizontally", () => ApplyEditAsync(new FlipEdit(true)));
            AddButton("Flip vertically", () => ApplyEditAsync(new FlipEdit(false)));
        }
        else if(tool == 4) BuildAdjustments();
        else if(tool == 5) BuildFilters();
        else if(tool == 6) BuildExportOptions();
        else if(tool == 7) BuildMetadata();
        else if(tool == 8) BuildBackground();
    }
    private void BuildAdjustments()
    {
        Inspector.Children.Add(Label("Adjust sliders, then apply them as one undoable edit."));
        var brightness = Adjustment("Brightness", 0, -100, 100); var contrast = Adjustment("Contrast", 0, -100, 100);
        var saturation = Adjustment("Saturation", 0, -100, 100); var exposure = Adjustment("Exposure", 0, -4, 4);
        var temperature = Adjustment("Temperature", 0, -100, 100); var tint = Adjustment("Tint", 0, -100, 100);
        var highlights = Adjustment("Highlights", 0, -100, 100); var shadows = Adjustment("Shadows", 0, -100, 100);
        AddButton("Apply adjustments", () => ApplyEditAsync(new ColorAdjustEdit(brightness.Value,contrast.Value,saturation.Value,exposure.Value,temperature.Value,tint.Value,highlights.Value,shadows.Value)));
        AddButton("Reset values", () => { foreach(var slider in new[]{brightness,contrast,saturation,exposure,temperature,tint,highlights,shadows}) slider.Value = 0; return Task.CompletedTask; });
    }
    private void BuildFilters()
    {
        Inspector.Children.Add(Label("Each filter is an individual, undoable image operation."));
        foreach(var preset in Enum.GetValues<FilterPreset>()) AddButton(preset switch { FilterPreset.BlackAndWhite => "Black & White", _ => preset.ToString() }, () => ApplyEditAsync(new FilterEdit(preset)));
    }
    private void BuildAbout()
    {
        Inspector.Children.Add(Label("AxoVisuals 0.2.0 development build",true));
        Inspector.Children.Add(Label("Local image utility · Windows x64\n\nNo accounts, telemetry, uploads or automatic downloads."));
        Inspector.Children.Add(Label("Formats",true));
        Inspector.Children.Add(Label("PNG · JPEG (.jpg / .jpeg)\nWebP · AVIF · TIFF · BMP\n\nHEIC / HEIF: read only. HEVC encoding is not bundled. Unsupported variants are reported safely. No OS codec installation is needed."));
        Inspector.Children.Add(Label("Processing",true));
        Inspector.Children.Add(Label("All image processing is performed locally. Original files are never modified.\n\nMagick.NET Q8 · 8-bit channels\nONNX Runtime CPU · U²-Net\n\nThe local model is included. Background removal works offline.\n\nLimits: 80 megapixels, 512 MB input, 40,000 pixels per side. Animated and multi-page files use only the first frame."));
        Inspector.Children.Add(Label("Licenses",true));
        Inspector.Children.Add(Label("AxoVisuals is developed by Axolotl Technology.\n© 2026 Axolotl Technology. All rights reserved.\n\nLicense texts and third-party notices are in the licenses folder beside the application."));
    }
    private static uint Positive(double value) { if(double.IsNaN(value) || value < 1 || value > 40000) throw new InvalidOperationException("Enter valid dimensions."); return (uint)Math.Round(value); }
    private async Task ApplyEditAsync(ImageEdit edit) { await model.ApplyAsync(edit); await ShowPreviewAsync(); BuildInspector(); }
    private void BuildExportOptions()
    {
        var formats = ImagePipeline.Formats;
        var format = Choice("Output format", formats.Select(f=>f.Name), Array.FindIndex(formats, f=>f.Name == model.Export.Format));
        var quality = Number("Quality", model.Export.Quality, 1, 100);
        var targetSize = Number("Target size (KB, optional)", 0, 0, 1024000);
        var encoderHint = Label(""); Inspector.Children.Add(encoderHint);
        var policy = Choice("Metadata", ["Keep metadata", "Privacy clean", "Remove GPS", "Remove all metadata"], (int)model.Export.Metadata);
        Inspector.Children.Add(Label($"Output: {model.Width} × {model.Height} px\nOriginal: {model.Document!.Info.FileSize / 1024d:N1} KB"));
        Inspector.Children.Add(Label("JPEG and BMP flatten transparency onto white. Metadata support varies by format."));
        var size = Label("Preview the export to measure its exact encoded size."); Inspector.Children.Add(size);
        void RefreshEncoder()
        {
            var selected=formats[format.SelectedIndex];
            quality.Visibility = selected.HasQuality ? Visibility.Visible : Visibility.Collapsed;
            targetSize.Visibility = selected.HasQuality ? Visibility.Visible : Visibility.Collapsed;
            encoderHint.Text=selected.Name=="PNG" ? "PNG uses lossless compression. Pixel quality is unchanged." : selected.HasQuality ? "Higher quality usually produces a larger file." : "This encoder has no quality control.";
            size.Text="Settings changed. Preview again to update the result.";
        }
        format.SelectionChanged += (_,_) => { model.Export = model.Export with { Format = formats[format.SelectedIndex].Name }; RefreshEncoder(); };
        quality.ValueChanged += (_,_) => { if(!double.IsNaN(quality.Value)) model.Export = model.Export with { Quality = (uint)quality.Value }; size.Text="Quality changed. Preview again to update the result."; };
        policy.SelectionChanged += (_,_) => { model.Export = model.Export with { Metadata = (MetadataPolicy)policy.SelectedIndex }; size.Text="Metadata policy changed. Preview again to update the result."; };
        RefreshEncoder();
        AddButton("Preview export result", async () =>
        {
            var bytes=await model.EncodeAsync();
            var preview=await Task.Run(()=> { using var image=new ImageMagick.MagickImage(bytes); image.Resize(new ImageMagick.MagickGeometry(1600,1200){Greater=true}); image.Settings.SetDefine(ImageMagick.MagickFormat.Png,"compression-level","1"); return image.ToByteArray(ImageMagick.MagickFormat.Png); });
            await DisplayBytesAsync(preview);
            var change=100d*(bytes.Length-model.Document.Info.FileSize)/model.Document.Info.FileSize;
            size.Text=$"Original: {model.Document.Info.FileSize/1024d:N1} KB\nEncoded: {bytes.Length/1024d:N1} KB\n{Math.Abs(change):F1}% {(change<=0 ? "smaller" : "larger")}\nShowing export result · {model.Export.Format}";
        });
        AddButton("Optimize for target size", async () =>
        {
            if(targetSize.Value < 1) throw new InvalidOperationException("Enter a target size in KB.");
            var result = await model.EncodeToTargetAsync((long)Math.Round(targetSize.Value * 1024));
            model.Export = model.Export with { Quality = result.Quality };
            var preview = await Task.Run(()=> { using var image=new ImageMagick.MagickImage(result.Bytes); image.Resize(new ImageMagick.MagickGeometry(1600,1200){Greater=true}); return image.ToByteArray(ImageMagick.MagickFormat.Png); });
            await DisplayBytesAsync(preview);
            size.Text = result.TargetReached ? $"Target met: {result.Bytes.Length / 1024d:N1} KB at quality {result.Quality}. Resolution was not changed." : $"Target cannot be met without resizing. Best result: {result.Bytes.Length / 1024d:N1} KB at quality {result.Quality}.";
        });
        AddButton("Back to working image", async ()=> { await ShowPreviewAsync(); size.Text="Showing working image. Preview export result to measure current settings."; });
        AddButton("Export image…", ExportAsync);
    }
    private async Task ExportAsync()
    {
        if(model.Document == null) throw new InvalidOperationException("Open an image first.");
        var format = ImagePipeline.Formats.First(f=>f.Name == model.Export.Format);
        var path = Infrastructure.SaveDialog.Pick(WinRT.Interop.WindowNative.GetWindowHandle(this),System.IO.Path.GetFileNameWithoutExtension(model.Document.Info.Path) + "-edited",format.Name,format.Extension);
        if(path == null) return;
        if(string.Equals(System.IO.Path.GetFullPath(path), model.Document.Info.Path, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("The original image is protected. Choose a new filename.");
        if(System.IO.File.Exists(path)) throw new InvalidOperationException("Choose a new filename; existing files are protected.");
        var bytes = await model.EncodeAsync();
        await ImagePipeline.WriteNewAsync(path,bytes,CancellationToken.None);
        exportNotice = $"Exported {bytes.Length / 1024d:N1} KB";
    }
    
    
    
}







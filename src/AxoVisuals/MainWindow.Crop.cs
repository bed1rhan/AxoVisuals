using AxoVisuals.Core.Imaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;
namespace AxoVisuals;
public sealed partial class MainWindow
{
    private Point? cropStart;
    private CropEdit? crop;
    private double cropRatio;
    private TextBlock? cropText;
    private void CropPressed(object sender, PointerRoutedEventArgs e)
    {
        if(busy || model.Document == null || ToolList.SelectedIndex != 2) return;
        cropStart = e.GetCurrentPoint(CropOverlay).Position;
        CropOverlay.CapturePointer(e.Pointer); e.Handled = true;
    }
    private void CropMoved(object sender, PointerRoutedEventArgs e)
    {
        if(cropStart == null || model.Document == null) return;
        var point = e.GetCurrentPoint(CropOverlay).Position;
        var start = cropStart.Value;
        var x = Math.Clamp(Math.Min(start.X, point.X), 0, ImageSurface.Width);
        var y = Math.Clamp(Math.Min(start.Y, point.Y), 0, ImageSurface.Height);
        var w = Math.Min(Math.Abs(point.X-start.X), ImageSurface.Width-x);
        var h = Math.Min(Math.Abs(point.Y-start.Y), ImageSurface.Height-y);
        if(cropRatio > 0) { if(w/h > cropRatio) w = h*cropRatio; else h = w/cropRatio; }
        if(w < 1 || h < 1) return;
        Canvas.SetLeft(CropRectangle, x); Canvas.SetTop(CropRectangle, y); CropRectangle.Width = w; CropRectangle.Height = h; CropRectangle.Visibility = Visibility.Visible;
        var px = (uint)(x / ImageSurface.Width * model.Width); var py = (uint)(y / ImageSurface.Height * model.Height);
        crop = new(px, py, Math.Min(model.Width-px, Math.Max(1,(uint)(w/ImageSurface.Width*model.Width))), Math.Min(model.Height-py, Math.Max(1,(uint)(h/ImageSurface.Height*model.Height))));
        if(cropText != null) cropText.Text = $"{crop.Width} × {crop.Height} px  ·  ({crop.X}, {crop.Y})";
        e.Handled = true;
    }
    private void CropReleased(object sender, PointerRoutedEventArgs e) { cropStart = null; CropOverlay.ReleasePointerCapture(e.Pointer); e.Handled = true; }
}

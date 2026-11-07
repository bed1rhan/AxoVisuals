using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

namespace AxoVisuals;
public sealed partial class MainWindow
{
    private void InitializeChrome()
    {
        // Keep native caption hit testing (drag, double-click, Snap Layouts, system menu).
        // Only the visible glyphs/backgrounds are replaced by the colored circles beneath them.
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleStrip);
        var title=AppWindow.TitleBar;
        var transparent=Windows.UI.Color.FromArgb(0,0,0,0);
        title.ButtonForegroundColor=transparent; title.ButtonInactiveForegroundColor=transparent;
        title.ButtonBackgroundColor=transparent; title.ButtonInactiveBackgroundColor=transparent;
        title.ButtonHoverForegroundColor=transparent; title.ButtonHoverBackgroundColor=transparent;
        title.ButtonPressedForegroundColor=transparent; title.ButtonPressedBackgroundColor=transparent;
        title.PreferredHeightOption=TitleBarHeightOption.Standard;
        Root.Loaded += (_,_) => UpdateCaptionLayout();
        AppWindow.Changed += (_,_) => UpdateCaptionLayout();
    }
    private void UpdateCaptionLayout()
    {
        var scale=Root.XamlRoot?.RasterizationScale ?? 1;
        CaptionCircles.Width=AppWindow.TitleBar.RightInset > 0 ? AppWindow.TitleBar.RightInset/scale : 138;
    }
    private void TitleSizeChanged(object sender, SizeChangedEventArgs e) { if(CaptionCircles != null) UpdateCaptionLayout(); }
    private void UpdateHistoryControls() { UndoButton.IsEnabled = !busy && model.CanUndo; RedoButton.IsEnabled = !busy && model.CanRedo; }
    private async Task ChangeHistoryAsync(bool redo)
    {
        if(redo) await model.RedoAsync(); else await model.UndoAsync();
        beforeBackground = null;
        await ShowPreviewAsync(); BuildInspector();
    }
    private async void UndoClick(object sender, RoutedEventArgs e) => await RunAsync(()=>ChangeHistoryAsync(false));
    private async void RedoClick(object sender, RoutedEventArgs e) => await RunAsync(()=>ChangeHistoryAsync(true));
    private async void UndoAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        // Text fields retain their native text undo behavior.
        if(Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(Root.XamlRoot) is Microsoft.UI.Xaml.Controls.TextBox) return;
        args.Handled=true; if(model.CanUndo) await RunAsync(()=>ChangeHistoryAsync(false));
    }
    private async void RedoAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if(Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(Root.XamlRoot) is Microsoft.UI.Xaml.Controls.TextBox) return;
        args.Handled=true; if(model.CanRedo) await RunAsync(()=>ChangeHistoryAsync(true));
    }
}


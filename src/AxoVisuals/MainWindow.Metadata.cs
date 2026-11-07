using AxoVisuals.Core.Imaging;
using AxoVisuals.Core.Metadata;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
namespace AxoVisuals;
public sealed partial class MainWindow
{
    private TextBox Field(string header, string text, bool multiline = false)
    {
        var input = new TextBox { Header = header, Text = text, AcceptsReturn = multiline, TextWrapping = TextWrapping.Wrap, MaxLength = 4000 };
        Inspector.Children.Add(input); return input;
    }
    private void BuildMetadata()
    {
        if(model.Document == null) return;
        Inspector.Children.Add(Label("Changes apply on export. Privacy clean removes all descriptive metadata and keeps only the color profile."));
        var policy = Choice("Export policy", ["Keep metadata", "Privacy clean", "Remove GPS", "Remove all metadata"], (int)model.Export.Metadata);
        policy.SelectionChanged += (_,_) => model.Export = model.Export with { Metadata = (MetadataPolicy)policy.SelectedIndex };
        AddButton("Remove GPS on export", () => { model.Export = model.Export with { Metadata = MetadataPolicy.RemoveGps }; BuildInspector(); return Task.CompletedTask; });
        AddButton("Remove all metadata on export", () => { model.Export = model.Export with { Metadata = MetadataPolicy.RemoveAll }; BuildInspector(); return Task.CompletedTask; });
        using var image = model.Document.CloneOriginal();
        MetadataFields fields;
        try { fields = model.Export.Fields ?? MetadataService.ReadFields(image); }
        catch(Exception) { Inspector.Children.Add(Label("Metadata is malformed. Use Privacy clean or Remove all metadata.")); return; }
        var entries = MetadataService.Read(image);
        var rows = new StackPanel { Spacing = 8 };
        foreach(var group in entries.GroupBy(e=>e.Group))
        {
            rows.Children.Add(Label(group.Key, true));
            foreach(var entry in group.Take(200)) rows.Children.Add(new TextBlock { Text = entry.Name + ": " + entry.Value, TextWrapping = TextWrapping.Wrap, FontSize = 12, IsTextSelectionEnabled = true });
        }
        if(entries.Count == 0) rows.Children.Add(Label("No embedded metadata."));
        Inspector.Children.Add(new Expander { Header = $"Original metadata ({entries.Count})", Content = rows, HorizontalAlignment = HorizontalAlignment.Stretch });
        Inspector.Children.Add(Label("Editable fields", true));
        var title = Field("Title",fields.Title); var description = Field("Description", fields.Description,true); var author = Field("Author",fields.Author); var copyright = Field("Copyright",fields.Copyright); var date = Field("Capture date · YYYY-MM-DD HH:MM:SS",fields.CaptureDate);
        var latitude = Field("Latitude · decimal",fields.Latitude); var longitude = Field("Longitude · decimal",fields.Longitude);
                var extraInputs = new Dictionary<string,TextBox>();
        var extraRows = new StackPanel { Spacing = 10 };
        try
        {
            var values = fields.Additional ?? MetadataService.ReadAdditional(image);
            foreach(var field in MetadataService.AdditionalFields)
            {
                var input = new TextBox { Header = field.Label + " · " + field.Group, Text = values.GetValueOrDefault(field.Key) ?? "", MaxLength = field.MaximumLength };
                extraInputs[field.Key] = input; extraRows.Children.Add(input);
            }
        }
        catch(System.Xml.XmlException) { extraRows.Children.Add(Label("Malformed XMP: editing is blocked; cleaning remains available.")); }
        Inspector.Children.Add(new Expander { Header = "Additional editable fields", Content = extraRows, HorizontalAlignment = HorizontalAlignment.Stretch });
        Inspector.Children.Add(Label("Clear an editable field to remove it. Dimensions, orientation, offsets, exposure values, color profiles and unknown fields are read-only or derived; inspect them in Original metadata."));
        AddButton("Apply metadata edits", async () =>
        {
            var edit = new MetadataFields(title.Text, description.Text, author.Text,copyright.Text,date.Text,latitude.Text,longitude.Text,extraInputs.ToDictionary(p=>p.Key,p=>p.Value.Text));
            await Task.Run(()=> { using var validation = model.Document.CloneOriginal(); MetadataService.Edit(validation,edit); });
            model.SetMetadata(edit);
            BuildInspector(); exportNotice = "Metadata edits pending · Keep metadata enabled";
        });
        Inspector.Children.Add(Label("Applying edits selects Keep metadata. Choosing a cleaning policy later can remove these fields. PNG, JPEG, WebP and TIFF support metadata; BMP does not."));
    }
}


using System.Xml.Linq;
using ImageMagick;
namespace AxoVisuals.Core.Metadata;

public sealed record EditableMetadataField(string Key, string Label, string Group, int MaximumLength, ExifTag<string>? ExifTag, IptcTag? IptcTag, XName XmpName);
public static partial class MetadataService
{
    private static readonly XNamespace Tiff = "http://ns.adobe.com/tiff/1.0/";
    private static readonly XNamespace Photoshop = "http://ns.adobe.com/photoshop/1.0/";
    public static readonly EditableMetadataField[] AdditionalFields =
    [
        new("make","Camera manufacturer","EXIF / XMP",256,ImageMagick.ExifTag.Make,null,Tiff+"Make"),
        new("model","Camera model","EXIF / XMP",256,ImageMagick.ExifTag.Model,null,Tiff+"Model"),
        new("software","Software","EXIF / XMP",256,ImageMagick.ExifTag.Software,null,XName.Get("CreatorTool","http://ns.adobe.com/xap/1.0/")),
        new("lens","Lens model","EXIF / XMP",256,ImageMagick.ExifTag.LensModel,null,XName.Get("LensModel","http://cipa.jp/exif/1.0/")),
        new("headline","Headline","IPTC / XMP",256,null,ImageMagick.IptcTag.Headline,Photoshop+"Headline"),
        new("credit","Credit","IPTC / XMP",32,null,ImageMagick.IptcTag.Credit,Photoshop+"Credit"),
        new("source","Source","IPTC / XMP",32,null,ImageMagick.IptcTag.Source,Photoshop+"Source"),
        new("city","City","IPTC / XMP",32,null,ImageMagick.IptcTag.City,Photoshop+"City"),
        new("label","Label","XMP",256,null,null,XName.Get("Label","http://ns.adobe.com/xap/1.0/"))
    ];
    public static Dictionary<string,string> ReadAdditional(MagickImage image)
    {
        var xml = ReadXmp(image); var exif = image.GetExifProfile(); var iptc = image.GetIptcProfile();
        return AdditionalFields.ToDictionary(f=>f.Key, f=>
            xml?.Descendants(f.XmpName).FirstOrDefault()?.Value ?? xml?.Descendants().Attributes(f.XmpName).FirstOrDefault()?.Value ??
            (f.ExifTag is not null ? exif?.GetValue(f.ExifTag)?.Value : null) ??
            (f.IptcTag != null ? iptc?.Values.FirstOrDefault(v=>v.Tag == f.IptcTag)?.Value : null) ?? "");
    }
    private static void EditAdditional(MagickImage image, IReadOnlyDictionary<string,string>? values)
    {
        if(values == null || values.Count == 0) return;
        // Never expose arbitrary tag IDs: structural, offset, color and dimension fields are derived.
        foreach(var pair in values)
        {
            var field = AdditionalFields.FirstOrDefault(f=>f.Key == pair.Key) ?? throw new InvalidOperationException("This metadata field is read-only.");
            if(pair.Value.Contains('\0') || System.Text.Encoding.UTF8.GetByteCount(pair.Value) > field.MaximumLength) throw new InvalidOperationException($"{field.Label} must be at most {field.MaximumLength} UTF-8 bytes and contain no null characters.");
        }
        var xml = ReadXmp(image)!;
        var description = xml.Descendants(Rdf+"Description").First();
        var exif = image.GetExifProfile() ?? new ExifProfile();
        var iptc = image.GetIptcProfile() ?? new IptcProfile();
        bool exifChanged=false, iptcChanged=false;
        foreach(var field in AdditionalFields)
        {
            if(!values.TryGetValue(field.Key,out var value)) continue;
            SetXmp(xml,description,field.XmpName,value);
            if(field.ExifTag is not null) { SetText(exif,field.ExifTag,value); exifChanged=true; }
            if(field.IptcTag != null) { SetIptc(iptc,field.IptcTag.Value,value); iptcChanged=true; }
        }
        if(exifChanged) image.SetProfile(exif);
        if(iptcChanged) image.SetProfile(iptc);
        image.SetProfile(new XmpProfile(xml));
    }
}


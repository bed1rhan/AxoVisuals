using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using AxoVisuals.Core.Imaging;
using ImageMagick;
namespace AxoVisuals.Core.Metadata;

public sealed record MetadataFields(string Title = "", string Description = "", string Author = "", string Copyright = "", string CaptureDate = "", string Latitude = "", string Longitude = "", IReadOnlyDictionary<string,string>? Additional = null);
public sealed record MetadataEntry(string Group, string Name, string Value);
public static partial class MetadataService
{
    private static readonly XNamespace Rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
    private static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";
    private static readonly XNamespace Exif = "http://ns.adobe.com/exif/1.0/";
    private static readonly XNamespace Xmp = "http://ns.adobe.com/xap/1.0/";
    public static List<MetadataEntry> Read(MagickImage image)
    {
        var rows = new List<MetadataEntry>();
        try { var exif = image.GetExifProfile(); if(exif != null) foreach(var value in exif.Values) rows.Add(new("EXIF", value.Tag.ToString(), Limit(value.ToString()))); }
        catch(MagickException) { rows.Add(new("EXIF", "Warning", "Malformed EXIF block")); }
        try { var iptc = image.GetIptcProfile(); if(iptc != null) foreach(var value in iptc.Values) rows.Add(new("IPTC", value.Tag.ToString(), Limit(value.ToString()))); }
        catch(MagickException) { rows.Add(new("IPTC", "Warning", "Malformed IPTC block")); }
        try
        {
            var xmp = ReadXmp(image);
            if(xmp != null)
            {
                foreach(var element in xmp.Descendants().Where(e=>!e.HasElements && !string.IsNullOrWhiteSpace(e.Value))) rows.Add(new("XMP", element.Name.LocalName, Limit(element.Value)));
                foreach(var attribute in xmp.Descendants().Attributes().Where(a=>!a.IsNamespaceDeclaration && a.Name != Rdf+"about")) rows.Add(new("XMP", attribute.Name.LocalName, Limit(attribute.Value)));
            }
        }
        catch(Exception e) when(e is XmlException or MagickException) { rows.Add(new("XMP", "Warning", "Malformed XMP block; editing is unavailable until metadata is removed.")); }
        foreach(var name in image.ProfileNames) rows.Add(new("Profiles", name, "Embedded profile"));
        foreach(var name in image.AttributeNames) rows.Add(new("Other", name, Limit(image.GetAttribute(name))));
        return rows;
    }
    private static string Limit(string? value) => value is null ? "" : value.Length > 4096 ? value[..4096] + "…" : value;
    private static XDocument? ReadXmp(MagickImage image)
    {
        var bytes = image.GetXmpProfile()?.ToByteArray(); if(bytes == null || bytes.Length == 0) return null;
        using var reader = XmlReader.Create(new MemoryStream(bytes), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 16_000_000 });
        return XDocument.Load(reader);
    }
    public static MetadataFields ReadFields(MagickImage image)
    {
        var exif = image.GetExifProfile();
        XDocument? xmp = null; try { xmp = ReadXmp(image); } catch(XmlException) { }
        string Field(XName name) => xmp?.Descendants(name).FirstOrDefault()?.Value ?? xmp?.Descendants().Attributes(name).FirstOrDefault()?.Value ?? "";
        string title = Field(Dc+"title");
        var titleBytes = exif?.GetValue(ExifTag.XPTitle)?.Value;
        if(title.Length == 0 && titleBytes != null) title = Encoding.Unicode.GetString(titleBytes).TrimEnd('\0');
        var lat = Coordinate(exif, true); var lon = Coordinate(exif, false);
        return new(title, Field(Dc+"description") is {Length:>0} d ? d : exif?.GetValue(ExifTag.ImageDescription)?.Value ?? "", Field(Dc+"creator") is {Length:>0} a ? a : exif?.GetValue(ExifTag.Artist)?.Value ?? "", Field(Dc+"rights") is {Length:>0} c ? c : exif?.GetValue(ExifTag.Copyright)?.Value ?? "", exif?.GetValue(ExifTag.DateTimeOriginal)?.Value ?? Field(Exif+"DateTimeOriginal"), lat, lon);
    }
    private static string Coordinate(IExifProfile? profile, bool latitude)
    {
        var values = latitude ? profile?.GetValue(ExifTag.GPSLatitude)?.Value : profile?.GetValue(ExifTag.GPSLongitude)?.Value;
        if(values is not { Length: 3 }) return "";
        double number = values[0].ToDouble() + values[1].ToDouble()/60 + values[2].ToDouble()/3600;
        var direction = latitude ? profile?.GetValue(ExifTag.GPSLatitudeRef)?.Value : profile?.GetValue(ExifTag.GPSLongitudeRef)?.Value;
        if(direction is "S" or "W") number *= -1;
        return number.ToString("0.######", CultureInfo.InvariantCulture);
    }
    public static void Edit(MagickImage image, MetadataFields fields)
    {
        // Parse first: malformed unknown XMP must not be silently replaced.
        var xml = ReadXmp(image) ?? new XDocument(new XElement(XName.Get("xmpmeta", "adobe:ns:meta/"), new XElement(Rdf+"RDF", new XElement(Rdf+"Description", new XAttribute(Rdf+"about", "")))));
        var description = xml.Descendants(Rdf+"Description").FirstOrDefault() ?? throw new InvalidOperationException("The XMP structure cannot be edited safely.");
        var profile = image.GetExifProfile() ?? new ExifProfile();
        SetText(profile, ExifTag.ImageDescription, fields.Description); SetText(profile, ExifTag.Artist, fields.Author); SetText(profile, ExifTag.Copyright, fields.Copyright);
        if(fields.Title.Length == 0) profile.RemoveValue(ExifTag.XPTitle); else profile.SetValue(ExifTag.XPTitle, Encoding.Unicode.GetBytes(fields.Title + "\0"));
        string date = "";
        if(!string.IsNullOrWhiteSpace(fields.CaptureDate))
        {
            if(!DateTime.TryParseExact(fields.CaptureDate, ["yyyy:MM:dd HH:mm:ss", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)) throw new InvalidOperationException("Use capture date YYYY-MM-DD HH:MM:SS.");
            date = parsed.ToString("yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture);
        }
        SetText(profile, ExifTag.DateTimeOriginal, date);
        SetXmp(xml, description, Dc+"title", fields.Title, "Alt"); SetXmp(xml, description, Dc+"description", fields.Description, "Alt"); SetXmp(xml, description, Dc+"creator", fields.Author, "Seq"); SetXmp(xml, description, Dc+"rights", fields.Copyright, "Alt");
        SetXmp(xml, description, Exif+"DateTimeOriginal", date.Length > 0 ? date[..10].Replace(':','-') + "T" + date[11..] : "");
        if(string.IsNullOrWhiteSpace(fields.Latitude) != string.IsNullOrWhiteSpace(fields.Longitude)) throw new InvalidOperationException("Enter both latitude and longitude, or leave both empty.");
        foreach(var value in profile.Values.Where(v=>v.Tag.ToString().StartsWith("GPS", StringComparison.Ordinal)).ToArray()) profile.RemoveValue(value.Tag);
        RemoveXmpGps(xml);
        if(!string.IsNullOrWhiteSpace(fields.Latitude))
        {
            if(!double.TryParse(fields.Latitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) || !double.IsFinite(lat) || Math.Abs(lat)>90 || !double.TryParse(fields.Longitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var lon) || !double.IsFinite(lon) || Math.Abs(lon)>180) throw new InvalidOperationException("Enter valid decimal GPS coordinates (latitude ±90, longitude ±180).");
            profile.SetValue(ExifTag.GPSLatitude, Dms(lat)); profile.SetValue(ExifTag.GPSLongitude, Dms(lon));
            profile.SetValue(ExifTag.GPSLatitudeRef, lat < 0 ? "S" : "N"); profile.SetValue(ExifTag.GPSLongitudeRef, lon < 0 ? "W" : "E");
        }
        image.SetProfile(profile); image.SetProfile(new XmpProfile(xml));
        EditAdditional(image, fields.Additional);
        var iptc = image.GetIptcProfile();
        if(iptc != null)
        {
            SetIptc(iptc, IptcTag.Title, fields.Title); SetIptc(iptc, IptcTag.Caption, fields.Description); SetIptc(iptc, IptcTag.Byline, fields.Author); SetIptc(iptc, IptcTag.CopyrightNotice, fields.Copyright); image.SetProfile(iptc);
        }
    }
    private static void SetIptc(IIptcProfile profile, IptcTag tag, string value) { profile.RemoveValue(tag); if(value.Length > 0) profile.SetValue(tag,value); }
    private static void SetText(IExifProfile profile, ExifTag<string> tag, string value) { profile.RemoveValue(tag); if(value.Length > 0) profile.SetValue(tag,value); }
    private static Rational[] Dms(double number) { number = Math.Abs(number); var degrees = Math.Floor(number); var minutes = Math.Floor((number-degrees)*60); return [new Rational(degrees), new Rational(minutes), new Rational(((number-degrees)*60-minutes)*60)]; }
    private static void SetXmp(XDocument xml, XElement description, XName name, string value, string? collection = null)
    {
        xml.Descendants(name).Remove(); xml.Descendants().Attributes(name).Remove(); if(value.Length == 0) return;
        description.Add(new XElement(name, collection == null ? value : new XElement(Rdf+collection, new XElement(Rdf+"li", collection == "Alt" ? new XAttribute(XNamespace.Xml+"lang","x-default") : null, value))));
    }
    private static void RemoveXmpGps(XDocument xml)
    {
        static bool IsGps(string name) => name.Contains("GPS", StringComparison.OrdinalIgnoreCase);
        xml.Descendants().Where(e=>IsGps(e.Name.LocalName)).ToArray().Remove();
        xml.Descendants().Attributes().Where(a=>IsGps(a.Name.LocalName)).ToArray().Remove();
    }
    public static void ApplyPolicy(MagickImage image, MetadataPolicy policy)
    {
        if(policy == MetadataPolicy.Keep) return;
        if(policy == MetadataPolicy.RemoveGps)
        {
            var exif = image.GetExifProfile();
            if(exif != null) { foreach(var item in exif.Values.Where(v=>v.Tag.ToString().StartsWith("GPS",StringComparison.Ordinal)).ToArray()) exif.RemoveValue(item.Tag); exif.RemoveValue(ExifTag.MakerNote); exif.RemoveThumbnail(); image.SetProfile(exif); }
            var xml = ReadXmp(image); if(xml != null) { RemoveXmpGps(xml); image.SetProfile(new XmpProfile(xml)); }
            // Opaque Photoshop resource blocks can duplicate GPS-bearing EXIF/XMP.
            image.RemoveProfile("8bim");
            foreach(var name in image.AttributeNames.Where(n=>n.Contains("GPS",StringComparison.OrdinalIgnoreCase)).ToArray()) image.RemoveAttribute(name);
            return;
        }
        var color = image.GetColorProfile();
        if(policy == MetadataPolicy.RemoveAll && color != null) image.TransformColorSpace(ColorProfiles.SRGB);
        image.Strip();
        foreach(var name in image.AttributeNames.ToArray()) image.RemoveAttribute(name);
        if(policy == MetadataPolicy.PrivacyClean && color != null) image.SetProfile(color);
    }
    public static void NormalizeDisplayMetadata(MagickImage image)
    {
        var profile = image.GetExifProfile();
        if(profile != null)
        {
            profile.RemoveThumbnail();
            profile.SetValue(ExifTag.Orientation,(ushort)1);
            profile.SetValue(ExifTag.PixelXDimension,new Number(image.Width));
            profile.SetValue(ExifTag.PixelYDimension,new Number(image.Height));
            image.SetProfile(profile);
        }
        // Keep unfamiliar XMP untouched; update only well-known display fields.
        XDocument? xml;
        try { xml = ReadXmp(image); } catch(XmlException) { return; }
        if(xml == null) return;
        XNamespace tiff = "http://ns.adobe.com/tiff/1.0/";
        foreach(var node in xml.Descendants(tiff+"Orientation")) node.Value = "1";
        foreach(var node in xml.Descendants().Attributes(tiff+"Orientation")) node.Value = "1";
        foreach(var name in new[]{Exif+"PixelXDimension",Exif+"PixelYDimension",tiff+"ImageWidth",tiff+"ImageLength"})
        {
            var value = (name.LocalName is "PixelXDimension" or "ImageWidth" ? image.Width : image.Height).ToString(CultureInfo.InvariantCulture);
            foreach(var node in xml.Descendants(name)) node.Value = value;
            foreach(var node in xml.Descendants().Attributes(name)) node.Value = value;
        }
        xml.Descendants(Xmp+"Thumbnails").Remove();
        image.SetProfile(new XmpProfile(xml));
    }
}



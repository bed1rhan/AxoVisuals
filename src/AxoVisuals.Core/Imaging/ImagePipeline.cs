using ImageMagick;
namespace AxoVisuals.Core.Imaging;

public abstract record ImageEdit;
public sealed record ResizeEdit(uint Width, uint Height) : ImageEdit;
public sealed record CropEdit(uint X, uint Y, uint Width, uint Height) : ImageEdit;
public sealed record RotateEdit(int Degrees) : ImageEdit;
public sealed record FlipEdit(bool Horizontal) : ImageEdit;
public enum MetadataPolicy { Keep, PrivacyClean, RemoveGps, RemoveAll }
public sealed record ExportOptions(string Format = "PNG", uint Quality = 85, MetadataPolicy Metadata = MetadataPolicy.PrivacyClean, AxoVisuals.Core.Metadata.MetadataFields? Fields = null);
public sealed record FormatOption(string Name, string Extension, MagickFormat Format, bool HasQuality);
public static class ImagePipeline
{
    public static readonly FormatOption[] Formats = [new("PNG", ".png", MagickFormat.Png, false), new("JPEG", ".jpg", MagickFormat.Jpeg, true), new("WebP", ".webp", MagickFormat.WebP, true), new("AVIF", ".avif", MagickFormat.Avif, true), new("TIFF", ".tiff", MagickFormat.Tiff, false), new("BMP", ".bmp", MagickFormat.Bmp, false)];
    public static (uint Width, uint Height) Fit(uint width, uint height, uint targetWidth, uint targetHeight)
    {
        if (width == 0 || height == 0 || targetWidth == 0 || targetHeight == 0) throw new ArgumentException("Dimensions must be positive.");
        var scale = Math.Min((double)targetWidth / width, (double)targetHeight / height);
        return ((uint)Math.Max(1, Math.Round(width * scale)), (uint)Math.Max(1, Math.Round(height * scale)));
    }
    public static MagickImage Render(ImageDocument document, IReadOnlyList<ImageEdit> edits, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        var image = document.CloneOriginal();
        try
        {
            foreach (var edit in edits)
            {
                token.ThrowIfCancellationRequested();
                Apply(image, edit);
            }
            token.ThrowIfCancellationRequested();
            return image;
        }
        catch { image.Dispose(); throw; }
    }
    public static void Apply(MagickImage image, ImageEdit edit)
    {
                switch(edit)
                {
                    case AxoVisuals.Core.AI.BackgroundEdit b: AxoVisuals.Core.AI.BackgroundRemovalService.Apply(image,b); break;
                    case ResizeEdit r:
                        ValidateSize(r.Width, r.Height);
                        image.FilterType = FilterType.Lanczos;
                        image.Resize(new MagickGeometry(r.Width, r.Height) { IgnoreAspectRatio = true }); break;
                    case CropEdit c:
                        ValidateSize(c.Width, c.Height);
                        if ((ulong)c.X + c.Width > image.Width || (ulong)c.Y + c.Height > image.Height) throw new InvalidOperationException("Crop must be inside the image.");
                        image.Crop(new MagickGeometry((int)c.X, (int)c.Y, c.Width, c.Height)); image.ResetPage(); break;
                    case RotateEdit r: image.Rotate(r.Degrees); image.ResetPage(); break;
                    case FlipEdit f: if(f.Horizontal) image.Flop(); else image.Flip(); break;
                }
    }
    public static void ValidateSize(uint width, uint height)
    {
        if (width < 1 || height < 1 || width > 40000 || height > 40000 || (ulong)width * height > ImageDocument.MaximumPixels)
            throw new InvalidOperationException("Use dimensions between 1 and 40,000 pixels, up to 80 megapixels.");
    }
    public static byte[] Encode(MagickImage image, ExportOptions options)
    {
        var format = Formats.FirstOrDefault(f => f.Name == options.Format) ?? throw new NotSupportedException("This encoder is unavailable.");
        if(options.Fields != null) AxoVisuals.Core.Metadata.MetadataService.Edit(image, options.Fields);
        if(options.Metadata is MetadataPolicy.Keep or MetadataPolicy.RemoveGps) AxoVisuals.Core.Metadata.MetadataService.NormalizeDisplayMetadata(image);
        AxoVisuals.Core.Metadata.MetadataService.ApplyPolicy(image, options.Metadata);
        if(format.HasQuality) image.Quality = Math.Clamp(options.Quality, 1, 100);
        if(format.Format == MagickFormat.Png) image.Settings.SetDefine(MagickFormat.Png, "compression-level", "6");
        if(format.Format == MagickFormat.Jpeg || format.Format == MagickFormat.Bmp)
        {
            image.BackgroundColor = MagickColors.White;
            image.Alpha(AlphaOption.Remove);
        }
        return image.ToByteArray(format.Format);
    }
    public static async Task ExportAsync(ImageDocument document, IReadOnlyList<ImageEdit> edits, ExportOptions options, string destination, CancellationToken token = default)
    {
        if (string.Equals(Path.GetFullPath(destination), document.Info.Path, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Choose a new filename. The original is protected.");
        var bytes = await Task.Run(() => { using var image = Render(document, edits, token); return Encode(image, options); }, token);
        await WriteNewAsync(destination, bytes, token);
    }
    public static async Task WriteNewAsync(string destination, byte[] bytes, CancellationToken token)
    {
        var fullPath = Infrastructure.LocalPaths.Validate(destination);
        if(File.Exists(fullPath)) throw new InvalidOperationException("A file already exists here. Choose a new filename.");
        var temporary = Path.Combine(Path.GetDirectoryName(fullPath)!, ".axovisuals-" + Guid.NewGuid().ToString("N") + ".tmp");
        try { await File.WriteAllBytesAsync(temporary, bytes, token); token.ThrowIfCancellationRequested(); File.Move(temporary, fullPath, false); }
        finally { if(File.Exists(temporary)) File.Delete(temporary); }
    }
}




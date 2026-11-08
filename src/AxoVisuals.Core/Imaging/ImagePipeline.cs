using ImageMagick;
namespace AxoVisuals.Core.Imaging;

public abstract record ImageEdit;
public sealed record ResizeEdit(uint Width, uint Height) : ImageEdit;
public sealed record CropEdit(uint X, uint Y, uint Width, uint Height) : ImageEdit;
public sealed record RotateEdit(int Degrees) : ImageEdit;
public sealed record FlipEdit(bool Horizontal) : ImageEdit;
public sealed record ColorAdjustEdit(double Brightness = 0, double Contrast = 0, double Saturation = 0, double Exposure = 0, double Temperature = 0, double Tint = 0, double Highlights = 0, double Shadows = 0) : ImageEdit;
public enum FilterPreset { Grayscale, Sepia, BlackAndWhite, Vintage, Warm, Cool, Vivid }
public sealed record FilterEdit(FilterPreset Preset) : ImageEdit;
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
                    case ColorAdjustEdit a: ApplyAdjustments(image, a); break;
                    case FilterEdit f: ApplyFilter(image, f.Preset); break;
                    default: throw new NotSupportedException($"The image edit '{edit.GetType().Name}' is not supported.");
                }
    }
    private static void ApplyAdjustments(MagickImage image, ColorAdjustEdit adjust)
    {
        if (adjust.Brightness != 0 || adjust.Contrast != 0) image.BrightnessContrast(new Percentage(Math.Clamp(adjust.Brightness, -100, 100)), new Percentage(Math.Clamp(adjust.Contrast, -100, 100)));
        var exposure = Math.Pow(2, Math.Clamp(adjust.Exposure, -4, 4)) * 100;
        var saturation = Math.Clamp(100 + adjust.Saturation, 0, 200);
        var hue = Math.Clamp(100 + adjust.Tint, 0, 200);
        if (exposure != 100 || saturation != 100 || hue != 100) image.Modulate(new Percentage(exposure), new Percentage(saturation), new Percentage(hue));
        if (adjust.Highlights != 0) image.BrightnessContrast(new Percentage(adjust.Highlights / 4), new Percentage(adjust.Highlights / 4));
        if (adjust.Shadows != 0) image.BrightnessContrast(new Percentage(adjust.Shadows / 3), new Percentage(0));
        if (adjust.Temperature != 0)
        {
            var color = adjust.Temperature > 0 ? new MagickColor("#FFB36B") : new MagickColor("#78B7FF");
            image.Colorize(color, new Percentage(Math.Min(Math.Abs(adjust.Temperature), 100) / 5));
        }
    }
    private static void ApplyFilter(MagickImage image, FilterPreset preset)
    {
        switch (preset)
        {
            case FilterPreset.Grayscale: image.Grayscale(PixelIntensityMethod.Rec601Luma); break;
            case FilterPreset.Sepia: image.SepiaTone(new Percentage(80)); break;
            case FilterPreset.BlackAndWhite: image.Grayscale(PixelIntensityMethod.Rec601Luma); image.Threshold(new Percentage(50)); break;
            case FilterPreset.Vintage: image.SepiaTone(new Percentage(65)); image.Modulate(new Percentage(95), new Percentage(78), new Percentage(95)); break;
            case FilterPreset.Warm: image.Colorize(new MagickColor("#FFAB5C"), new Percentage(12)); break;
            case FilterPreset.Cool: image.Colorize(new MagickColor("#77B8FF"), new Percentage(12)); break;
            case FilterPreset.Vivid: image.Modulate(new Percentage(105), new Percentage(135), new Percentage(100)); break;
            default: throw new NotSupportedException($"The filter '{preset}' is not supported.");
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
    public static (byte[] Bytes, uint Quality, bool TargetReached) EncodeToTarget(MagickImage image, ExportOptions options, long targetBytes)
    {
        var format = Formats.FirstOrDefault(f => f.Name == options.Format) ?? throw new NotSupportedException("This encoder is unavailable.");
        if (!format.HasQuality) throw new InvalidOperationException("Target file size is available for JPEG, WebP, and AVIF only.");
        if (targetBytes < 1024) throw new InvalidOperationException("Choose a target size of at least 1 KB.");
        byte[]? best = null; uint bestQuality = 1;
        var low = 1; var high = 100;
        while (low <= high)
        {
            var quality = (uint)((low + high) / 2);
            using var candidate = (MagickImage)image.Clone();
            var bytes = Encode(candidate, options with { Quality = quality });
            if (bytes.Length <= targetBytes) { best = bytes; bestQuality = quality; low = (int)quality + 1; }
            else high = (int)quality - 1;
        }
        if (best != null) return (best, bestQuality, true);
        using var minimum = (MagickImage)image.Clone();
        return (Encode(minimum, options with { Quality = 1 }), 1, false);
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




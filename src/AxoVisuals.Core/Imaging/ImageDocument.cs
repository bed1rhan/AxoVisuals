using ImageMagick;
namespace AxoVisuals.Core.Imaging;

public sealed record ImageInfo(string Path, string Format, uint Width, uint Height, long FileSize);

public sealed class ImageDocument : IDisposable
{
    public ImageInfo Info { get; }
    private readonly MagickImage original;
    public const ulong MaximumPixels = 80_000_000;
    public static readonly string[] Extensions = [".png", ".jpg", ".jpeg", ".webp", ".avif", ".heic", ".heif", ".tif", ".tiff", ".bmp"];
    private ImageDocument(string path, MagickImage image)
    {
        original = image;
        Info = new(path, image.Format.ToString(), image.Width, image.Height, new FileInfo(path).Length);
    }
    public static ImageDocument Open(string path)
    {
        ImagingRuntime.Initialize();
        path = Infrastructure.LocalPaths.Validate(path);
        if (!Extensions.Contains(System.IO.Path.GetExtension(path).ToLowerInvariant()))
            throw new NotSupportedException("This file type is not supported.");
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > 512L * 1024 * 1024) throw new InvalidOperationException("This file exceeds the 512 MB safety limit.");
        var info = new MagickImageInfo(stream);
        if ((ulong)info.Width * info.Height > MaximumPixels) throw new InvalidOperationException("This image exceeds the 80 megapixel safety limit.");
        if (!new[] { MagickFormat.Png, MagickFormat.Jpeg, MagickFormat.WebP, MagickFormat.Avif, MagickFormat.Heic, MagickFormat.Tiff, MagickFormat.Bmp, MagickFormat.Bmp2, MagickFormat.Bmp3 }.Contains(info.Format))
            throw new NotSupportedException("The file contents are not a supported raster image.");
        stream.Position = 0;
        var image = new MagickImage();
        try
        {
            image.Read(stream, new MagickReadSettings { FrameIndex = 0, FrameCount = 1 });
            image.AutoOrient();
            image.ResetPage();
            return new ImageDocument(System.IO.Path.GetFullPath(path), image);
        }
        catch { image.Dispose(); throw; }
    }
    public MagickImage CloneOriginal() => (MagickImage)original.Clone();
    public byte[] Preview()
    {
        using var image = CloneOriginal();
        image.Resize(new MagickGeometry(1600, 1200) { Greater = true });
        return image.ToByteArray(MagickFormat.Png);
    }
    public void Dispose() => original.Dispose();
}




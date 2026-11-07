using ImageMagick;
namespace AxoVisuals.Core.Infrastructure;
public static class UserErrors
{
    public static string Describe(Exception error) => error switch
    {
        OperationCanceledException => "Operation cancelled. Originals are unchanged.",
        OutOfMemoryException or MagickResourceLimitErrorException => "Not enough memory for this image. Try a smaller image or close other applications.",
        UnauthorizedAccessException => "Access denied. Choose a writable local destination.",
        FileNotFoundException or DirectoryNotFoundException => "The file or folder is no longer available.",
        MagickMissingDelegateErrorException => "The required codec is unavailable for this image variant. Try PNG, JPEG, WebP or AVIF.",
        NotSupportedException or InvalidOperationException => error.Message,
        System.Xml.XmlException => "The XMP metadata is malformed. Use Privacy clean or Remove all metadata.",
        MagickException => "The image could not be decoded or encoded. It may be damaged or unsupported.",
        IOException => "The file could not be written. Check free space and destination permissions.",
        _ => "The operation could not be completed. The original image is unchanged."
    };
}
public static class Diagnostics
{
    public static void Record(string operation, Exception error)
    {
        try
        {
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AxoVisuals");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory,"diagnostics.log");
            if(File.Exists(path) && new FileInfo(path).Length > 64_000) File.WriteAllText(path,"");
            // Never log messages, filenames, paths, image contents or metadata.
            File.AppendAllText(path,$"{DateTime.UtcNow:O} {operation} {error.GetType().Name} 0x{error.HResult:X8}\n");
        }
        catch { /* Diagnostics must never interrupt image processing. */ }
    }
}


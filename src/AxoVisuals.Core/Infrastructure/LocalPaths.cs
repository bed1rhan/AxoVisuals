namespace AxoVisuals.Core.Infrastructure;
public static class LocalPaths
{
    public static string Validate(string path)
    {
        var full = Path.GetFullPath(path);
        if(full.StartsWith(@"\\",StringComparison.Ordinal) || new DriveInfo(Path.GetPathRoot(full)!).DriveType == DriveType.Network)
            throw new InvalidOperationException("Choose a file or folder on a local drive.");
        return full;
    }
}

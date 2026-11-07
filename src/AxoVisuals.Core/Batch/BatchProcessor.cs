using AxoVisuals.Core.Imaging;
namespace AxoVisuals.Core.Batch;
public sealed record BatchOptions(ExportOptions Export, uint? MaxWidth = null, uint? MaxHeight = null, bool AllowUpscale = false);
public sealed record BatchProgress(int Index, int Total, string Source, string Status, string? Output = null);
public sealed record BatchResult(int Succeeded, int Failed, bool Cancelled);
public static class BatchProcessor
{
    public static async Task<BatchResult> RunAsync(IReadOnlyList<string> sources, string directory, BatchOptions options, IProgress<BatchProgress>? progress = null, CancellationToken token = default)
    {
        directory = Infrastructure.LocalPaths.Validate(directory);
        if(!Directory.Exists(directory)) throw new InvalidOperationException("Choose an existing output folder.");
        var success = 0; var failed = 0;
        for(var index = 0; index < sources.Count; index++)
        {
            if(token.IsCancellationRequested) return new(success,failed,true);
            var source = sources[index];
            progress?.Report(new(index, sources.Count, source, "Processing"));
            try
            {
                using var document = await Task.Run(()=>ImageDocument.Open(source), token);
                var edits = new List<ImageEdit>();
                if(options.MaxWidth is uint w && options.MaxHeight is uint h)
                {
                    var size = ImagePipeline.Fit(document.Info.Width, document.Info.Height, w,h);
                    if(options.AllowUpscale || size.Width < document.Info.Width || size.Height < document.Info.Height) edits.Add(new ResizeEdit(size.Width, size.Height));
                }
                var extension = ImagePipeline.Formats.First(f=>f.Name == options.Export.Format).Extension;
                var stem = Path.GetFileNameWithoutExtension(source) + "-processed";
                var destination = Path.Combine(directory, stem + extension);
                var suffix = 1;
                while(File.Exists(destination) || sources.Any(p=>string.Equals(Path.GetFullPath(p),destination,StringComparison.OrdinalIgnoreCase))) destination = Path.Combine(directory, stem + "-" + suffix++ + extension);
                await ImagePipeline.ExportAsync(document, edits, options.Export, destination, token);
                success++; progress?.Report(new(index+1, sources.Count, source, "Done", destination));
            }
            catch(OperationCanceledException) { progress?.Report(new(index,sources.Count,source,"Cancelled")); return new(success,failed,true); }
            catch(Exception e) when(e is not StackOverflowException)
            {
                failed++;
                var message = e is UnauthorizedAccessException ? "Destination access denied" : e is NotSupportedException ? "Unsupported format" : "Cannot process image";
                progress?.Report(new(index+1,sources.Count,source,message));
            }
        }
        return new(success,failed,false);
    }
}


using AxoVisuals.Core.Imaging;
using ImageMagick;
namespace AxoVisuals.ViewModels;

public sealed class WorkspaceViewModel : IDisposable
{
    private sealed record State(ImageEdit[] Edits, ExportOptions Export);
    private readonly List<State> undo = [];
    private readonly List<State> redo = [];
    private MagickImage? working;
    public bool CanUndo => undo.Count > 0;
    public bool CanRedo => redo.Count > 0;
    public ImageDocument? Document { get; private set; }
    public byte[]? PreviewBytes { get; private set; }
    public uint Width { get; private set; }
    public uint Height { get; private set; }
    public List<ImageEdit> Edits { get; } = [];
    public ExportOptions Export { get; set; } = new();
    private State Snapshot() => new(Edits.ToArray(), Export);
    private static byte[] Preview(MagickImage source)
    {
        using var preview = (MagickImage)source.Clone();
        preview.Resize(new MagickGeometry(1600,1200) { Greater = true });
        preview.Strip();
        preview.Settings.SetDefine(MagickFormat.Png,"compression-level","1");
        return preview.ToByteArray(MagickFormat.Png);
    }
    public async Task OpenAsync(string path)
    {
        var result = await Task.Run(() => { var document = ImageDocument.Open(path); try { using var image = document.CloneOriginal(); return (document, Preview(image)); } catch { document.Dispose(); throw; } });
        Dispose(); Document = result.document; PreviewBytes = result.Item2; Edits.Clear(); undo.Clear(); redo.Clear(); Export = new(); Width = Document.Info.Width; Height = Document.Info.Height;
    }
    private void Commit(MagickImage image, byte[] preview, State state)
    {
        working?.Dispose(); working = image;
        PreviewBytes = preview; Width = image.Width; Height = image.Height;
        Edits.Clear(); Edits.AddRange(state.Edits); Export = state.Export;
    }
    private void Remember()
    {
        undo.Add(Snapshot()); redo.Clear();
        // Store commands and shared masks, never a full raster for each history entry.
        if(undo.Count > 32) undo.RemoveAt(0);
    }
    public async Task ApplyAsync(ImageEdit? edit, bool reset = false)
    {
        if(Document == null) return;
        var next = new State(reset ? [] : edit == null ? Edits.ToArray() : [..Edits,edit], reset ? new() : Export);
        var result = await Task.Run(() =>
        {
            var image = reset ? Document.CloneOriginal() : working != null ? (MagickImage)working.Clone() : ImagePipeline.Render(Document, Edits);
            try { if(edit != null) ImagePipeline.Apply(image,edit); return (image, Preview(image)); }
            catch { image.Dispose(); throw; }
        });
        Remember(); Commit(result.image,result.Item2,next);
    }
    public void SetMetadata(AxoVisuals.Core.Metadata.MetadataFields fields)
    {
        Remember(); Export = Export with { Fields = fields, Metadata = MetadataPolicy.Keep };
    }
    public async Task UndoAsync() => await RestoreAsync(undo,redo);
    public async Task RedoAsync() => await RestoreAsync(redo,undo);
    private async Task RestoreAsync(List<State> source, List<State> destination)
    {
        if(Document == null || source.Count == 0) return;
        var state = source[^1];
        var result = await Task.Run(() => { var image = ImagePipeline.Render(Document,state.Edits); try { return (image,Preview(image)); } catch { image.Dispose(); throw; } });
        destination.Add(Snapshot()); source.RemoveAt(source.Count-1); Commit(result.image,result.Item2,state);
    }
    public async Task<byte[]> EncodeAsync(CancellationToken token = default)
    {
        if(Document == null) throw new InvalidOperationException("Open an image first.");
        var options = Export;
        return await Task.Run(() => { token.ThrowIfCancellationRequested(); using var image = working != null ? (MagickImage)working.Clone() : ImagePipeline.Render(Document,Edits,token); return ImagePipeline.Encode(image,options); },token);
    }
    public async Task<byte[]> FullPreviewAsync()
    {
        if(Document == null) throw new InvalidOperationException("Open an image first.");
        return await Task.Run(() => { using var image = working != null ? (MagickImage)working.Clone() : ImagePipeline.Render(Document,Edits); image.Settings.SetDefine(MagickFormat.Png,"compression-level","1"); return image.ToByteArray(MagickFormat.Png); });
    }
    public void Dispose() { working?.Dispose(); working = null; Document?.Dispose(); Document = null; }
}

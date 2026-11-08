using AxoVisuals.Core.Imaging;
using AxoVisuals.Core.Metadata;
using AxoVisuals.ViewModels;
using ImageMagick;
using System.Diagnostics;
internal static class RevisionTests
{
    private static void Check(bool condition,string message) { if(!condition) throw new Exception(message); }
    public static async Task RunAsync(string path,string root)
    {
        using var vm = new WorkspaceViewModel();
        await vm.OpenAsync(path);
        await vm.ApplyAsync(new CropEdit(0,0,300,200));
        await vm.ApplyAsync(new RotateEdit(90));
        await vm.ApplyAsync(new FlipEdit(true));
        await vm.UndoAsync(); Check(vm.Edits.Count == 2 && vm.Width == 200,"Undo flip");
        await vm.UndoAsync(); Check(vm.Edits.Count == 1 && vm.Width == 300,"Undo rotate");
        await vm.RedoAsync(); Check(vm.Width == 200,"Redo rotate");
        await vm.ApplyAsync(null,true); Check(vm.Width==640 && vm.CanUndo,"Reset source");
        await vm.UndoAsync(); Check(vm.Width==200 && vm.Edits.Count==2,"Undo reset");
        await vm.UndoAsync(); await vm.ApplyAsync(new FlipEdit(false)); Check(!vm.CanRedo,"New edit clears redo");
        using(var expected=ImagePipeline.Render(vm.Document!,vm.Edits))
        using(var actual=new MagickImage(await vm.EncodeAsync()))
        { Check(expected.Width==actual.Width && expected.Height==actual.Height,"Cached dimensions"); Check(expected.Compare(actual,ErrorMetric.Absolute)==0,"Cached pixels differ"); }
        Console.WriteLine("PASS: operation history, redo invalidation, undoable reset and cached pixel equivalence");
        using(var mask=new MagickImage(MagickColors.Gray,32,32))
        {
            var background=new AxoVisuals.Core.AI.BackgroundEdit(mask.ToByteArray(MagickFormat.Png));
            await vm.ApplyAsync(background);
            await vm.ApplyAsync(new RotateEdit(90));
            await vm.UndoAsync(); await vm.UndoAsync();
            Check(!vm.Edits.Contains(background),"Background undo");
            await vm.RedoAsync(); Check(ReferenceEquals(vm.Edits[^1],background),"Background mask should be reused, not inferred");
            var fields=new MetadataFields(Author:"Undo test"); vm.SetMetadata(fields);
            await vm.ApplyAsync(null,true); await vm.UndoAsync(); Check(vm.Export.Fields?.Author=="Undo test","Reset must restore metadata state");
        }
        Console.WriteLine("PASS: background mask history reuse and metadata restoration after reset");
        using var image=new MagickImage(MagickColors.Coral,32,32);
        image.SetProfile(new XmpProfile(System.Text.Encoding.UTF8.GetBytes("<x:xmpmeta xmlns:x='adobe:ns:meta/'><rdf:RDF xmlns:rdf='http://www.w3.org/1999/02/22-rdf-syntax-ns#'><rdf:Description xmlns:custom='urn:test' custom:untouched='keep'/></rdf:RDF></x:xmpmeta>")));
        var extras=new Dictionary<string,string> { ["make"]="Test maker",["headline"]="Test headline",["city"]="Test city",["label"]="Test label" };
        MetadataService.Edit(image,new(Additional:extras));
        using(var encoding=(MagickImage)image.Clone())
        using(var reopened=new MagickImage(ImagePipeline.Encode(encoding,new("JPEG",Metadata:MetadataPolicy.Keep))))
            Check(MetadataService.ReadAdditional(reopened)["city"]=="Test city","Additional metadata roundtrip");
        MetadataService.Edit(image,new(Additional:new Dictionary<string,string>{["city"]=""}));
        Check(MetadataService.ReadAdditional(image)["city"]=="","Additional removal");
        Check(System.Text.Encoding.UTF8.GetString(image.GetXmpProfile()!.ToByteArray()).Contains("untouched=\"keep\""),"Unknown XMP lost");
        try { MetadataService.Edit(image,new(Additional:new Dictionary<string,string>{["orientation"]="8"})); throw new Exception("Structural metadata allowed"); } catch(InvalidOperationException) { }
        Check(ImagePipeline.Formats.Count(f=>f.Format==MagickFormat.Jpeg)==1,"Duplicate JPEG");
        using(var doc=ImageDocument.Open(path)) { var jpeg=Path.Combine(root,"alias.jpeg"); await ImagePipeline.ExportAsync(doc,[],new("JPEG"),jpeg); using var check=ImageDocument.Open(jpeg); Check(check.Info.Width==640,"JPEG alias"); }
        Console.WriteLine("PASS: safe extra EXIF/IPTC/XMP edit/removal, unknown XMP retention, structural rejection, JPEG extension alias");
        using(var missingModelImage = new MagickImage(MagickColors.Coral,32,32))
        using(var backgroundService = new AxoVisuals.Core.AI.BackgroundRemovalService())
        {
            try { backgroundService.CreateMask(missingModelImage,Path.Combine(root,"missing-model.onnx")); throw new Exception("Missing model accepted"); }
            catch(InvalidOperationException exception) when(exception.Message.Contains("bundled background model is missing",StringComparison.Ordinal)) { }
        }
        Console.WriteLine("PASS: missing background model reports a recoverable local error");
        // Same 6 MP textured pixels and command list for the old replay path and new cached path.
        var benchmark=Path.Combine(root,"benchmark.png");
        using(var sample=new MagickImage(MagickColors.Gray,3000,2000)) { sample.AddNoise(NoiseType.Gaussian); sample.Settings.SetDefine(MagickFormat.Png,"compression-level","1"); sample.Write(benchmark); }
        using var source=ImageDocument.Open(benchmark);
        ImageEdit[] history=[new ResizeEdit(2400,1600),new RotateEdit(90),new FlipEdit(true),new RotateEdit(-90)];
        using var cached=ImagePipeline.Render(source,history);
        var clock=Stopwatch.StartNew(); using(var replay=ImagePipeline.Render(source,[..history,new FlipEdit(false)])) { replay.Resize(new MagickGeometry(1600,1200){Greater=true}); _=replay.ToByteArray(MagickFormat.Png); }
        var replayMs=clock.Elapsed.TotalMilliseconds;
        clock.Restart(); using(var current=(MagickImage)cached.Clone()) { ImagePipeline.Apply(current,new FlipEdit(false)); current.Resize(new MagickGeometry(1600,1200){Greater=true}); current.Settings.SetDefine(MagickFormat.Png,"compression-level","1"); _=current.ToByteArray(MagickFormat.Png); }
        Console.WriteLine($"PROFILE: 6 MP replay + default PNG {replayMs:F0} ms; cached flip + fast preview {clock.Elapsed.TotalMilliseconds:F0} ms");
        await vm.OpenAsync(benchmark);
        foreach(var command in history) await vm.ApplyAsync(command);
        await vm.ApplyAsync(new FlipEdit(false));
        using(var expected=ImagePipeline.Render(source,[..history,new FlipEdit(false)]))
        using(var actual=new MagickImage(await vm.EncodeAsync()))
            Check(expected.Compare(actual,ErrorMetric.Absolute)==0,"Textured cached pixels differ from replay");
        Console.WriteLine("PASS: textured multi-operation cached rendering is pixel-identical to full replay");
    }
}

using AxoVisuals.Core.Imaging;
using ImageMagick;
ImagingRuntime.Initialize();
if(args is ["--verify-ui-export",var exported,var source])
{
    using var output = new MagickImage(exported);
    using var original = new MagickImage(source);
    var outputFields = AxoVisuals.Core.Metadata.MetadataService.ReadFields(output);
    var sourceFields = AxoVisuals.Core.Metadata.MetadataService.ReadFields(original);
    if(output.Width != original.Width || output.Height != original.Height || outputFields.Author.Length != 0 || outputFields.Latitude.Length != 0 || sourceFields.Author != "Test author" || sourceFields.Latitude != "41.01") throw new Exception("UI export verification failed");
    Console.WriteLine("PASS: UI export reopened; metadata cleaned; original metadata retained");
    return;
}
var root = Path.Combine(Path.GetTempPath(), "AxoVisualsTests-" + Guid.NewGuid());
Directory.CreateDirectory(root);
var path = Path.Combine(root, "source.png");
using (var image = new MagickImage(MagickColors.Coral, 640, 480)) image.Write(path);
using(var document = ImageDocument.Open(path)) { if (document.Info.Width != 640 || document.Preview().Length == 0) throw new Exception("Opening failed."); }
File.WriteAllText(Path.Combine(root, "corrupt.png"), "invalid image");
try { using var invalid = ImageDocument.Open(Path.Combine(root,"corrupt.png")); throw new Exception("Corrupt image accepted."); } catch(MagickException) { }
Console.WriteLine("PASS: opening, preview and corrupt-image handling");
using(var document = ImageDocument.Open(path))
{
    var originalHash = System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path));
    foreach(var format in ImagePipeline.Formats)
    {
        var output = Path.Combine(root, "converted" + format.Extension);
        await ImagePipeline.ExportAsync(document, [new ResizeEdit(320,240)], new ExportOptions(format.Name), output);
        using var check = ImageDocument.Open(output);
        if(check.Info.Width != 320 || check.Info.Height != 240) throw new Exception("Conversion dimensions failed: " + format.Name);
        Console.WriteLine("PASS: PNG -> " + format.Name + " reopened");
    }
    using var jpeg = ImageDocument.Open(Path.Combine(root,"converted.jpg"));
    await ImagePipeline.ExportAsync(jpeg, [], new ExportOptions("PNG"), Path.Combine(root,"roundtrip.png"));
    using var png = ImageDocument.Open(Path.Combine(root,"roundtrip.png"));
    if(png.Info.Format != "Png") throw new Exception("JPEG -> PNG failed");
    var fit = ImagePipeline.Fit(640,480,200,200);
    if(fit != (200u,150u)) throw new Exception("Aspect ratio failed");
    using var transformed = ImagePipeline.Render(document, [new RotateEdit(90), new FlipEdit(true), new CropEdit(10,20,100,200)]);
    if(transformed.Width != 100 || transformed.Height != 200) throw new Exception("Transform failed");
    try { await ImagePipeline.ExportAsync(document, [], new(), path); throw new Exception("Original overwrite accepted"); } catch(InvalidOperationException) { }
    if(!originalHash.SequenceEqual(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)))) throw new Exception("Original changed");
    Console.WriteLine("PASS: JPEG -> PNG, aspect ratio, rotate/flip/crop, original protection");
}
var metadataPath = Path.Combine(root, "metadata.jpg");
using(var image = new MagickImage(MagickColors.SkyBlue,320,240))
{
    AxoVisuals.Core.Metadata.MetadataService.Edit(image, new("Sample title","Sample description","Test author","Copyright test","2024-01-02 12:34:56","41.01","28.97"));
    image.SetAttribute("comment","private comment"); image.SetProfile(ColorProfiles.SRGB);
    image.Write(metadataPath);
}
using(var document = ImageDocument.Open(metadataPath))
{
    using var read = document.CloneOriginal();
    var fields = AxoVisuals.Core.Metadata.MetadataService.ReadFields(read);
    if(fields.Title != "Sample title" || fields.Author != "Test author" || fields.Latitude != "41.01") throw new Exception("Metadata read/edit failed");
    foreach(var policy in new[]{MetadataPolicy.RemoveGps,MetadataPolicy.PrivacyClean,MetadataPolicy.RemoveAll})
    {
        var output = Path.Combine(root, policy + ".jpg");
        await ImagePipeline.ExportAsync(document,[],new("JPEG",85,policy),output);
        using var clean = new MagickImage(output);
        var actual = AxoVisuals.Core.Metadata.MetadataService.ReadFields(clean);
        if(actual.Latitude != "" || actual.Longitude != "") throw new Exception("GPS leaked: " + policy);
        if(policy == MetadataPolicy.RemoveGps && actual.Author != "Test author") throw new Exception("Remove GPS removed author");
        if(policy != MetadataPolicy.RemoveGps && actual.Author != "") throw new Exception("Author leaked");
        if(policy == MetadataPolicy.PrivacyClean && clean.GetColorProfile() == null) throw new Exception("Color profile lost");
        if(policy == MetadataPolicy.RemoveAll && clean.ProfileNames.Any()) throw new Exception("Profile remained");
        Console.WriteLine("PASS: metadata " + policy);
    }
}
var batchDir = Path.Combine(root,"batch"); Directory.CreateDirectory(batchDir);
var batch = await AxoVisuals.Core.Batch.BatchProcessor.RunAsync([path,Path.Combine(root,"corrupt.png"),metadataPath],batchDir,new(new("PNG"),160,160));
if(batch.Succeeded != 2 || batch.Failed != 1) throw new Exception("Batch isolation failed");
foreach(var output in Directory.GetFiles(batchDir)) { using var check = ImageDocument.Open(output); if(check.Info.Width > 160 || check.Info.Height > 160) throw new Exception("Batch resize failed"); }
Console.WriteLine("PASS: batch failure isolation and reopening");
Console.WriteLine("Artifacts: " + root);
if(args.Length >= 1)
{
    var watch = System.Diagnostics.Stopwatch.StartNew();
    var subject = Path.Combine(root,"generated-subject.png");
    using(var fixture = new MagickImage(MagickColors.White,320,320))
    {
        fixture.Draw(new ImageMagick.Drawing.Drawables().FillColor(MagickColors.Coral).Circle(160,160,250,160).FillColor(MagickColors.Black).Circle(130,140,142,140).Circle(190,140,202,140));
        fixture.Write(subject);
    }
    using var document = ImageDocument.Open(args.Length >= 2 ? args[1] : subject);
    using var original = document.CloneOriginal();
    using var remover = new AxoVisuals.Core.AI.BackgroundRemovalService();
    var mask = remover.CreateMask(original,args[0]);
    using var checkMask = new MagickImage(mask);
    var values = checkMask.ToByteArray(MagickFormat.Gray);
    if(values.Min() > 10 || values.Max() < 240) throw new Exception("Degenerate segmentation mask");
    using var result = ImagePipeline.Render(document,[new AxoVisuals.Core.AI.BackgroundEdit(mask,0.5)]);
    var output = Path.Combine(root,"background-removed.png"); result.Write(output);
    using var reopened = new MagickImage(output);
    var rgba = reopened.ToByteArray(MagickFormat.Rgba);
    var alphaValues = rgba.Where((v,i)=>i%4 == 3).ToArray();
    if(alphaValues.Min()>10 || alphaValues.Max()<240) throw new Exception("Mask was not applied to alpha");
    if(!reopened.HasAlpha || reopened.Width != original.Width) throw new Exception("Background export failed");
    Console.WriteLine($"PASS: local background inference, nonempty mask and alpha export ({watch.Elapsed.TotalSeconds:F2}s)");
    Console.WriteLine("Background artifact: " + output);
}
if(args.Length >= 3)
{
    try { using var heic = ImageDocument.Open(args[2]); Console.WriteLine($"CAPABILITY: HEIC read works ({heic.Info.Width} x {heic.Info.Height})"); }
    catch(MagickException ex) { Console.WriteLine("CAPABILITY: HEIC unavailable: " + ex.GetType().Name); }
}
using(var image = new MagickImage(MagickColors.Coral,16,16))
{
    try { var bytes = image.ToByteArray(MagickFormat.Heic); using var decoded = new MagickImage(bytes); Console.WriteLine("CAPABILITY: HEIC encode works"); }
    catch(MagickException ex) { Console.WriteLine("CAPABILITY: HEIC encode unavailable: " + ex.GetType().Name); }
}
Console.WriteLine("CODECS: " + MagickNET.Delegates);
try { using var invalid = ImageDocument.Open(Path.Combine(root,"unsupported.svg")); throw new Exception("Unsupported extension accepted"); } catch(NotSupportedException) { }
Console.WriteLine("PASS: unsupported extension rejected");
var originalBatch = Directory.GetFiles(batchDir).Select(File.ReadAllBytes).Select(System.Security.Cryptography.SHA256.HashData).ToArray();
var repeated = await AxoVisuals.Core.Batch.BatchProcessor.RunAsync([path],batchDir,new(new("PNG")));
if(repeated.Succeeded != 1 || Directory.GetFiles(batchDir).Length != 3) throw new Exception("Batch collision protection failed");
using(var cancelled = new CancellationTokenSource())
{
    cancelled.Cancel();
    var result = await AxoVisuals.Core.Batch.BatchProcessor.RunAsync([path],batchDir,new(new()),token:cancelled.Token);
    if(!result.Cancelled || result.Succeeded != 0) throw new Exception("Batch cancellation failed");
}
Console.WriteLine("PASS: batch collision avoidance and cancellation");
using(var image = new MagickImage(MagickColors.Red,30,20))
{
    image.SetProfile(new XmpProfile(System.Text.Encoding.UTF8.GetBytes("<malformed>")));
    var bytes = ImagePipeline.Encode(image,new("PNG",85,MetadataPolicy.RemoveAll));
    using var cleaned = new MagickImage(bytes);
    if(cleaned.GetXmpProfile() != null) throw new Exception("Malformed XMP not removed");
}
Console.WriteLine("PASS: malformed metadata cleaning");
using(var image = new MagickImage(new MagickColor("#ff000080"),10,10))
using(var mask = new MagickImage(MagickColors.White,10,10))
{
    AxoVisuals.Core.AI.BackgroundRemovalService.Apply(image,new(mask.ToByteArray(MagickFormat.Png)));
    var rgba = image.ToByteArray(MagickFormat.Rgba);
    if(rgba[3] < 120 || rgba[3] > 135) throw new Exception("Existing alpha lost");
}
using(var image = new MagickImage(MagickColors.Red,10,10))
using(var mask = new MagickImage(MagickColors.Black,10,10))
{
    AxoVisuals.Core.AI.BackgroundRemovalService.Apply(image,new(mask.ToByteArray(MagickFormat.Png),0,"#00FF00"));
    var rgba = image.ToByteArray(MagickFormat.Rgba);
    if(rgba[0] != 0 || rgba[1] != 255 || rgba[3] != 255) throw new Exception("Solid background failed");
}
using(var image = new MagickImage(MagickColors.Red,10,10))
using(var mask = new MagickImage(MagickColors.Black,10,10))
using(var background = new MagickImage(MagickColors.Blue,20,10))
{
    AxoVisuals.Core.AI.BackgroundRemovalService.Apply(image,new(mask.ToByteArray(MagickFormat.Png),0,null,background.ToByteArray(MagickFormat.Png)));
    var rgba = image.ToByteArray(MagickFormat.Rgba);
    if(rgba[0] != 0 || rgba[2] != 255 || rgba[3] != 255) throw new Exception("Replacement background failed");
}
Console.WriteLine("PASS: preserved alpha, solid color and replacement background");
try { using var image = new MagickImage(); image.Read(System.Text.Encoding.UTF8.GetBytes("<svg xmlns='http://www.w3.org/2000/svg' width='10' height='10'></svg>")); throw new Exception("Non-raster coder was allowed"); } catch(MagickException) { }
Console.WriteLine("PASS: non-raster coder blocked by privacy policy");

await RevisionTests.RunAsync(path, root);

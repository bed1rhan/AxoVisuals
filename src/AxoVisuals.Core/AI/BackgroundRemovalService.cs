using System.Security.Cryptography;
using AxoVisuals.Core.Imaging;
using ImageMagick;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
namespace AxoVisuals.Core.AI;

public sealed record BackgroundEdit(byte[] Mask, double Feather = 0, string? Color = null, byte[]? Replacement = null) : ImageEdit;
public sealed class BackgroundRemovalService : IDisposable
{
    public const string ModelSha256 = "8D10D2F3BB75AE3B6D527C77944FC5E7DCD94B29809D47A739A7A728A912B491";
    private InferenceSession? session;
    public byte[] CreateMask(MagickImage image, string modelPath, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if(session == null)
        {
            if(!File.Exists(modelPath)) throw new InvalidOperationException("The bundled background model is missing. Restore the models folder from the application package.");
            using var file = File.OpenRead(modelPath);
            if(!Convert.ToHexString(SHA256.HashData(file)).Equals(ModelSha256,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("The background model is damaged. Restore the original model file.");
            OrtEnv.Instance().DisableTelemetryEvents();
            using var options = new SessionOptions { GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL, IntraOpNumThreads = Math.Clamp(Environment.ProcessorCount/2,1,4), InterOpNumThreads = 1, LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_FATAL };
            session = new InferenceSession(modelPath,options);
        }
        using var small = (MagickImage)image.Clone();
        small.ColorSpace = ColorSpace.sRGB; small.BackgroundColor = MagickColors.White; small.Alpha(AlphaOption.Remove);
        small.Resize(new MagickGeometry(320,320) { IgnoreAspectRatio = true });
        var rgb = small.ToByteArray(MagickFormat.Rgb);
        var maximum = Math.Max(1, (int)rgb.Max());
        var tensor = new DenseTensor<float>(new[] {1,3,320,320});
        float[] mean = [0.485f,0.456f,0.406f]; float[] std = [0.229f,0.224f,0.225f];
        for(int y=0;y<320;y++) for(int x=0;x<320;x++) for(int c=0;c<3;c++) tensor[0,c,y,x] = (rgb[(y*320+x)*3+c]/(float)maximum-mean[c])/std[c];
        using var run = new RunOptions();
        using var registration = token.Register(()=>run.Terminate = true);
        token.ThrowIfCancellationRequested();
        try
        {
            using var results = session.Run([NamedOnnxValue.CreateFromTensor(session.InputMetadata.Keys.First(),tensor)], [session.OutputMetadata.Keys.First()], run);
            var mask = results.First().AsTensor<float>().ToArray();
            var min = mask.Min(); var max = mask.Max(); var range = Math.Max(1e-8f,max-min);
            var pixels = mask.Select(v=>(byte)Math.Clamp(Math.Round((v-min)/range*255),0,255)).ToArray();
            using var result = new MagickImage();
            result.ReadPixels(pixels,new PixelReadSettings(320,320,StorageType.Char,"I"));
            token.ThrowIfCancellationRequested();
            return result.ToByteArray(MagickFormat.Png);
        }
        catch(OnnxRuntimeException) when(token.IsCancellationRequested) { throw new OperationCanceledException(token); }
    }
    public static void Apply(MagickImage image, BackgroundEdit edit)
    {
        using var mask = new MagickImage(edit.Mask);
        mask.Resize(new MagickGeometry(image.Width,image.Height) { IgnoreAspectRatio = true });
        if(edit.Feather > 0) mask.GaussianBlur(0,Math.Clamp(edit.Feather,0,10));
        using var alpha = (MagickImage)image.Clone();
        alpha.Alpha(AlphaOption.Extract);
        alpha.Alpha(AlphaOption.Off); mask.Alpha(AlphaOption.Off);
        alpha.Composite(mask,CompositeOperator.Multiply);
        alpha.Alpha(AlphaOption.Off);
        image.Alpha(AlphaOption.Set); image.Composite(alpha,CompositeOperator.CopyAlpha);
        if(edit.Color != null || edit.Replacement != null)
        {
            using var background = edit.Replacement != null ? new MagickImage(edit.Replacement) : new MagickImage(new MagickColor(edit.Color!),image.Width,image.Height);
            background.Resize(new MagickGeometry(image.Width,image.Height) { FillArea = true }); background.Crop(image.Width,image.Height,Gravity.Center); background.ResetPage();
            image.Composite(background,CompositeOperator.DstOver);
        }
    }
    public void Dispose() { session?.Dispose(); session = null; }
}


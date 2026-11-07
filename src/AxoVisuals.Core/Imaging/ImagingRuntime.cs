using ImageMagick;
using ImageMagick.Configuration;
namespace AxoVisuals.Core.Imaging;
public static class ImagingRuntime
{
    private static readonly Lazy<bool> Initialized = new(() =>
    {
        var config = ConfigurationFiles.Default;
        config.Policy.Data = """
        <policymap>
          <policy domain="delegate" rights="none" pattern="*" />
          <policy domain="filter" rights="none" pattern="*" />
          <policy domain="coder" rights="none" pattern="*" />
          <policy domain="coder" rights="read|write" pattern="{PNG*,JPEG,JPG,WEBP,AVIF,HEIC,TIFF,BMP*,RGB*,GRAY*,XC}" />
          <policy domain="path" rights="none" pattern="@*" />
          <policy domain="resource" name="memory" value="1GiB" />
          <policy domain="resource" name="map" value="0" />
          <policy domain="resource" name="disk" value="0" />
          <policy domain="resource" name="width" value="40KP" />
          <policy domain="resource" name="height" value="40KP" />
          <policy domain="resource" name="list-length" value="256" />
          <policy domain="resource" name="thread" value="4" />
        </policymap>
        """;
        MagickNET.Initialize(config);
        return true;
    });
    public static void Initialize() => _ = Initialized.Value;
}


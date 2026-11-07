# Dependency decisions

Reviewed 2026-09-17. Runtime packages are pinned in project files; package lock files record transitive resolution.

| Component | Selection | Reason / distribution notes |
|---|---|---|
| .NET | 10.0, SDK 10.0.401 | Current LTS generation, native Windows tooling, self-contained x64 publish. Framework notices are included in publish output. |
| Windows App SDK / WinUI 3 | 1.8.260804001 | Maintained 1.8 servicing release, native Fluent controls and Windows pickers. Microsoft redistributable license is included. No WebView, Electron or web server. |
| Windows SDK BuildTools | 10.0.26100.4654 | NuGet-contained build tools; does not require global Visual Studio installation on this machine. Build-time only. |
| Magick.NET-Q8-x64 | 14.16.0 | Maintained, broad codec support, transformations and EXIF/IPTC/XMP profile editing in one dependency. Apache 2.0 wrapper and ImageMagick license. Q8 keeps working memory lower than Q16/HDRI. |
| ONNX Runtime | 1.30.0 CPU | MIT; native local inference without a GPU or driver dependency. Session is reused, CPU threads bounded, telemetry events explicitly disabled. |
| U²-Net | Full U2Net ONNX | Apache 2.0 upstream project, established salient-object segmentation and approximately 176 MB model. Better default detail than the small U2NetP variant. Not intended as perfect hair matting. |

## Codec licenses

Magick.NET's Apache license does **not** cover every native codec. The exact NuGet package supplies `Notice.txt`; the complete, unmodified file is included as `licenses/MagickNET-NOTICE.txt`. It includes LGPL libraries such as libheif 1.23.1 and libde265 1.1.1, as well as other third-party notices. These licenses permit open-source redistribution subject to their notices, corresponding-source and relinking requirements; native libraries are distributed as replaceable files, not hidden inside an obfuscated or single-file executable.

This delivery is for local use, not a published third-party binary release. Before an external binary release, provide the matching native dependency source/build materials alongside the binary distribution and retain the LGPL replacement/relinking rights. Codec patent rights are distinct from copyright licenses; no claim of worldwide patent clearance is made. HEVC encoding is not enabled or offered. The project has not been published and no license has been assigned to the user's own source code on their behalf.

Source provenance: Magick.NET 14.16.0 package repository commit `474ebe6f28152415426a1791f6cc3b76c8067a39`. The package notices identify ImageMagick 7.1.2-29 and individual native dependency versions. See https://github.com/dlemstra/Magick.NET/releases/tag/14.16.0 and https://github.com/dlemstra/Magick.Native for corresponding native build sources.

Alternatives considered: ImageSharp has additional licensing conditions for some uses and does not cover this entire format set in one package. SkiaSharp is strong for rendering but would still require more codecs and metadata tooling. Windows Imaging Component depends on installed OS codecs for several requested formats. Magick.NET avoids those installation dependencies and adds predictable local metadata processing.

## Model provenance

- Original: https://github.com/xuebinqin/U-2-Net
- License: https://github.com/xuebinqin/U-2-Net/blob/master/LICENSE
- ONNX artifact: https://github.com/danielgatis/rembg/releases/download/v0.0.0/u2net.onnx
- Upstream reference checksum: MD5 `60024c5c889badc19c04ad937298a77b`
- Verified SHA-256: `8d10d2f3bb75ae3b6d527c77944fc5e7dcd94b29809d47a739a7a728a912b491`
- Input: 1×3×320×320 RGB, ImageNet channel normalization after max-pixel scaling; first saliency output, min/max normalized, Lanczos resized.
- Inference and model loading contain no downloader. A missing or changed model produces a local error. `scripts/fetch-model.ps1` is a separate, explicit development utility only.

BRIA/RMBG variants were not selected because model weight conditions vary and may impose noncommercial restrictions. Larger transformer models were not necessary for this utility's CPU/memory target.

Primary references: https://github.com/dlemstra/Magick.NET/blob/main/License.txt ; https://github.com/microsoft/onnxruntime/blob/main/LICENSE ; https://github.com/strukturag/libheif/blob/master/COPYING ; https://github.com/strukturag/libde265/blob/master/COPYING ; https://learn.microsoft.com/windows/apps/windows-app-sdk/release-notes/windows-app-sdk-1-8 .

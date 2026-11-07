# AxoVisuals

A local Windows image utility built with C#, .NET 10, and WinUI 3. Image processing, metadata handling, and background removal run on the local machine.

Designed & Developed by Axolotl Technology.

## Features

- Open, preview, zoom, pan, and inspect images.
- Resize, crop, rotate, flip, convert, and compress images without overwriting the source.
- Read and edit supported EXIF, IPTC, and XMP fields.
- Remove GPS data, privacy-sensitive metadata, or all removable metadata.
- Remove backgrounds locally with the U²-Net ONNX model; export transparency, a solid color, or a replacement image.
- Process batches with isolated per-file failures and progress reporting.

## Format support

| Format | Read | Export |
|---|---:|---:|
| PNG | Yes | Yes |
| JPEG (`.jpg`, `.jpeg`) | Yes | Yes |
| WebP | Yes | Yes |
| AVIF | Yes | Yes |
| HEIC / HEIF | Yes | No HEVC encoder |
| TIFF | Yes, first page | Yes, single page |
| BMP | Yes | Yes |

Codec variants that are unavailable or malformed fail with a local error. Animated and multi-page inputs use their first frame or page. JPEG and BMP flatten transparency onto white.

## Requirements

- Windows 10 version 1809 or later, x64. Windows 11 is the primary target.
- PowerShell.
- .NET SDK 10.0.401. The build scripts use a workspace-local `.tools/dotnet` SDK when present, otherwise the SDK on `PATH`.

## Build from source

The U²-Net model is intentionally excluded from Git because it is a large binary. Download and verify it before building:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/fetch-model.ps1
powershell -ExecutionPolicy Bypass -File scripts/build.ps1
```

`fetch-model.ps1` downloads the model only as an explicit development step, validates its SHA-256 checksum, and saves it as `models/u2net.onnx`. The application never downloads the model itself; inference is offline after the model is present.

The self-contained portable output is written to `artifacts/AxoVisuals-win-x64/`. Keep that folder intact when running `AxoVisuals.exe`.

Run the integration suite separately with:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/test.ps1
```

## Packaging

`scripts/package-msix.ps1` builds a self-contained MSIX for local development and validates its contents. It uses a locally generated test certificate, so its output is not a publicly trusted installer and is not attached to GitHub releases. Production distribution requires a certificate from a trusted code-signing provider and a separate signing/release process.

## Privacy

The application contains no accounts, telemetry, analytics, cloud processing, update checker, browser runtime, or network client. Input is limited to local files; network and UNC paths are rejected. ONNX Runtime telemetry is disabled, ImageMagick delegates are denied, and the native pixel cache is prevented from spilling to disk.

## Limits

Inputs are limited to 80 megapixels, 40,000 pixels per side, and 512 MB encoded size. Processing uses 8-bit channels and up to 1 GiB of native memory. Background segmentation is a local salient-object model; fine hair, translucent objects, and low-contrast boundaries may need another tool.

## Dependencies and notices

- Windows App SDK / WinUI 3
- Magick.NET and ImageMagick native codecs
- ONNX Runtime
- U²-Net model (Apache-2.0)

See [dependency decisions](docs/DEPENDENCIES.md), [architecture](docs/ARCHITECTURE.md), [privacy controls](docs/PRIVACY.md), and [verification record](docs/VERIFICATION.md). Third-party license and notice texts are in `licenses/`.

## License

Copyright © 2026 Bedirhan Bayram. Licensed under the MIT License; see [LICENSE](LICENSE). Third-party license files in `licenses/` apply only to their respective components.

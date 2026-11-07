# AxoVisuals v0.1.0 — Initial Release

This is the first public release of AxoVisuals, a local Windows image utility designed and developed by Axolotl Technology.

## Highlights

- Open, inspect, zoom, and preview local images.
- Resize, crop, rotate, flip, convert, and compress images without overwriting the original.
- Read and edit supported EXIF, IPTC, and XMP metadata.
- Remove GPS data, privacy-sensitive metadata, or all removable metadata.
- Run local U²-Net background removal with transparent, solid-color, or replacement-image backgrounds.
- Process batches with progress reporting and per-file error isolation.

## Supported formats

PNG, JPEG (`.jpg` and `.jpeg`), WebP, AVIF, TIFF, and BMP can be read and exported. HEIC/HEIF can be read when supported by the bundled codec stack; HEVC encoding is not available in this release.

## Technology

AxoVisuals is a native Windows application built with C#, .NET 10, WinUI 3, Windows App SDK, Magick.NET, and ONNX Runtime. Image processing and model inference run locally.

## Installation status

This release provides source code only. A public installer is not yet available because the current MSIX package is signed with a local test certificate and is not suitable for public distribution. Build the application from source using the instructions in the README.

## License

Copyright © 2026 Bedirhan Bayram. Licensed under the MIT License.

## Next releases

- v0.1.1: bug fixes, code cleanup, and stability work.
- v0.2.0: additional image-editing features.

# Verification record

Validated on the local Windows 11 x64 machine on 2026-09-17.

## Automated

- Clean Release build: zero warnings and errors with .NET SDK 10.0.401.
- PNG exports reopened as PNG, JPEG (.jpg and .jpeg), WebP, AVIF, TIFF and BMP; JPEG to PNG reopened.
- Aspect-preserving resize, crop, rotate, flip and original-file SHA preservation; overwriting rejected.
- Metadata editing, GPS removal, privacy cleaning with ICC retention, complete profile removal and malformed XMP handling.
- Batch failure isolation, reopened results, collision avoidance and cancellation.
- Local U2-Net inference on generated and photographic fixtures; non-degenerate mask, output alpha, original alpha preservation, solid and replacement backgrounds.
- HEIC sample decoded at 1280 x 854. HEIC encoding is unavailable and is not offered.
- Unsupported extensions and non-raster coder content rejected.
- Production source privacy audit passed: no networking/process-launch APIs, delegates disabled, no native disk pixel cache, ONNX telemetry disabled, model hash pinned.
- NuGet vulnerability audit reported no known vulnerable packages in the configured sources at validation time.

Logs are in the workspace under `artifacts/verification`. Run `scripts/test.ps1` to reproduce the offline suite. Optional upstream photo/HEIC fixtures are development assets and are not included in the application package.

## Desktop verification

- Open image through Windows picker, preview, properties and native controls inspected.
- Interactive crop selection applied and resulting dimensions inspected.
- Local photographic background removal visually inspected, including transparent result and edge quality.
- Metadata viewer and policy selection inspected.
- Native Save As exported a new PNG; that exact output was reopened by the test harness to verify dimensions and privacy cleaning while the original retained metadata.
- Published portable EXE launched successfully. The packaging script includes WinUI PRI/XBF resources; a missing PRI discovered during this check was fixed.
- Published batch UI selected a local folder and processed three files: two successful outputs and one isolated corrupt-file error. Both output files were created, and progress/status remained responsive.
- The published build loaded its bundled model and removed the photographic background successfully. Its batch metadata output was reopened and verified clean while the source retained its metadata. No additional runtime diagnostics appeared after the packaging fix.
- Process endpoint snapshots showed zero TCP and zero UDP endpoints. This is a point-in-time observation, not a packet-capture proof; the source/native policy audit provides the stronger architectural check.

## Scope and limitations

Windows 10, ARM64, alternate DPI settings, touch input, every codec variant and extreme-memory workloads were not manually certified. The package targets x64 and Windows 10 build 17763 or later, primarily Windows 11. Drag/drop is implemented but was not separately exercised with Explorer automation. Automated logic tests cover transformations more broadly than the manual UI pass.

The app is unsigned and portable, with bundled runtimes and model. Keep its complete directory together. It uses 8-bit channels and only the first frame/page, with 80 MP / 512 MB input limits. Segmentation is a local saliency model, not a guarantee of perfect fine hair or transparent-object extraction. See README and dependency notices for remaining feature and redistribution limitations.

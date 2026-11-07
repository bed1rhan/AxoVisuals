# Privacy verification

The application has no network client, login, analytics, telemetry collection, update checker, cloud processor, remote model loader or browser runtime. All processing uses local files and local native libraries.

Defense in depth:

- Input paths are opened as FileStreams, never passed as ImageMagick pseudo-protocol strings.
- Network/UNC paths are rejected; ordinary local filesystem permissions apply.
- Extensions and actual decoded raster format are both validated.
- ImageMagick policy is initialized before any codec use: delegates and filters are denied; all coders are denied except the explicit raster set, raw local pixel formats and the internal solid-color generator.
- Pixel disk cache and memory-mapped spill are disabled. Temporary export files are local to the chosen output directory and removed on failure.
- XMP uses an XML reader with DTD/entity resolution disabled and a character bound.
- ONNX Runtime telemetry events are explicitly disabled before session creation. No execution-provider downloads are used.
- Diagnostics include only time, operation category, exception type and HRESULT. They exclude exception messages, filenames, file paths, pixels and metadata. Logs are bounded and local under `%LOCALAPPDATA%/AxoVisuals`.
- No recent-file history or image-content cache is persisted.

Development-time SDK, NuGet packages and the fixed model artifact were downloaded separately. These are build/setup operations, not image-processing workflows. The distributed app does not execute the development scripts. Existing OS-level cloud-sync/backup services remain outside the application.

`scripts/privacy-audit.ps1` scans production source for network/process-launch APIs and confirms explicit policy/telemetry controls. Integration tests exercise the native coder deny policy. Runtime socket inspection is recorded separately in the verification report.

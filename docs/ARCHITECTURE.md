# Architecture

Two production projects and a dependency-light console integration-test executable.

- `AxoVisuals`: WinUI 3 window/XAML, contextual inspectors and Windows picker integration. Partial window files group UI interaction code by feature. `WorkspaceViewModel` owns a document, pending ordered edits, export options and a bounded preview.
- `AxoVisuals.Core`: no UI dependencies. Imaging holds immutable operation records and executes an ordered pipeline; metadata operates on known profiles; AI computes/reuses a mask with local ONNX; batch isolates per-file failures; infrastructure restricts local paths and redacts diagnostics.

An open document owns one normalized original MagickImage. Edits are records; native clones use ImageMagick copy-on-write behavior until changed. A candidate edit is committed only after rendering succeeds. UI work is asynchronous; image processing and inference run off the dispatcher. Metadata UI code only invokes metadata services. The preview is cached between zoom/pan interactions. Full-resolution processing occurs when an operation, 100% rendering or export actually needs it.

Export encodes fully before creating a temporary file in the destination directory, then renames it without replacement. A race with an existing destination fails; source and existing output files are never truncated. The native Windows Common Item Dialog uses FOS_NOTESTFILECREATE and returns a path without reserving a file, so the same atomic writer can be used by UI and batch. Testing showed that both Windows Storage pickers could leave a zero-byte reservation, which interfered with strict no-overwrite behavior; the explicit native save dialog avoids that ambiguity.

Batch runs sequentially to bound native memory. A cancellation token is checked between decode/render/write stages. ONNX cancellation also signals RunOptions.Terminate. An already-running native codec call cannot be forcibly interrupted; cancellation takes effect at the next safe boundary. Closing during work requests cancellation and preserves the window until the operation finishes.

No DI container, plugin framework, network client, background service, database or browser runtime is used. Source is deliberately small enough to navigate directly.


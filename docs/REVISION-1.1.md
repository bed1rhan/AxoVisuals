# AxoVisuals 1.1 revision verification

## Changed areas

- `ImagePipeline.cs`: one JPEG encoder entry; JPEG extension aliases; quality only for encoders that use it; explicit lossless PNG settings; reusable single-operation application.
- `WorkspaceViewModel.cs`: command snapshots, 32-step undo/redo, undoable reset and metadata changes, one current full-resolution cache, fast lossless previews. Background edits retain and reuse the existing mask; rotate/flip never invoke ONNX.
- `MainWindow.xaml`, `MainWindow.Chrome.cs`, `MainWindow.xaml.cs`, `MainWindow.Tools.cs`: title-only branding, native caption regions with colored circles, undo/redo buttons/shortcuts, contextual inspector, format-aware export controls, explicit preview switching and measured size changes, revised About copy.
- `MetadataService.cs`, `MetadataFields.cs`, `MainWindow.Metadata.cs`: allowlisted extra descriptive fields, removal by clearing, UTF-8 limits, structural-field rejection and unknown-XMP preservation.
- `SaveDialog.cs`: both JPEG extensions accepted by the native save filter.
- Project version/company metadata; test project and `RevisionTests.cs`.
- `build.ps1`, `clean-publish.ps1`, `package-msix.ps1`, `install-msix.ps1`, `verify-msix.ps1`, README and private-key ignore rules.

## Results

Release builds completed with zero warnings/errors. The original integration suite passed, including photographic local inference and actual HEIC decoding. Added tests cover undo/redo order, redo invalidation, undoable reset, metadata state restoration, shared background-mask history, cached raster equivalence, safe extra EXIF/IPTC/XMP round trips/removal, structural rejection, unknown XMP retention and .jpeg export.

On a generated textured 6 MP fixture, the old replay-plus-default-PNG preview path took 659 ms and the cached-flip-plus-fast-PNG path took 291 ms in the recorded run. These are local measurements, not a general speed guarantee. The unnecessary full command replay and PNG preview encoding were reduced. Final export still renders/encodes full-resolution pixels; the preview compression change is lossless.

Desktop checks: empty inspector collapsed; inspector restored with an image; rotation, Ctrl+Z and Ctrl+Y changed the image and enabled states correctly; PNG had no quality control; preview displayed original/encoded size and percentage increase; returning to the working image worked. Native title double-click produced Restore state, the green region restored the window, and title dragging moved the window by the requested offset. Windows+Z opened the native Snap Layout menu. Hover-triggered Snap and other DPI/Windows versions were not separately certified. The first title-drag implementation was replaced after its double-click test failed; the shipped implementation uses native caption hit testing.

Portable cleanup only removes known PDBs, the two ONNX linker import libraries, developer documentation and MUI-only locale folders outside English/Turkish. No runtime DLLs, PRI resources, codecs, notices or models were removed. Final package is approximately 400 MiB portable / 251 MiB MSIX.

MSIX MakeAppx validation succeeded; it is self-contained, contains the exact model hash and required runtime/codec files, and its CMS signature passed cryptographic verification. Package identity, display name, publisher, version and icons are in the validated manifest. Native WinRT activation declarations are merged from the exact bundled SDK framework manifest. No external package dependencies are declared.

## Not verified

This session is not elevated. The test certificate was generated in CurrentUser/My with a non-exportable key, but was not trusted in a machine store. Therefore MSIX installation, installed-app activation, Start/Settings registration and uninstall could not be exercised. The elevated installation script is supplied beside the MSIX; it trusts only this certificate in TrustedPeople, not Trusted Root. No production signing/Store publishing was attempted.

See `artifacts/verification/revision-tests.log`, `revision-release.log`, `release-cleanup.txt`, `msix-package.log`, and `msix-integrity.log` for evidence. Project documentation stays outside the user-facing distribution.

Primary platform references: [title bar customization](https://learn.microsoft.com/windows/apps/develop/title-bar), [self-contained deployment](https://learn.microsoft.com/windows/apps/package-and-deploy/self-contained-deploy/deploy-self-contained-apps), [MSIX signing](https://learn.microsoft.com/windows/msix/package/sign-msix-package-guide).


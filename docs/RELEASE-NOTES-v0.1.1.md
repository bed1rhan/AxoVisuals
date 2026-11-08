# AxoVisuals v0.1.1

## Distribution

- Added a self-contained Windows x64 portable ZIP for GitHub Releases.
- The ZIP includes the .NET runtime, Windows App SDK runtime files, native codecs, third-party notices, and the verified U²-Net model for offline background removal.
- Added a SHA-256 checksum file for the release ZIP.
- The test-signed MSIX remains a local-development artifact and is not published as a user installer.

## Continuous integration

- Added a Windows GitHub Actions workflow for pushes and pull requests targeting `main` and `develop`.
- CI uses the SDK pinned in `global.json`, restores the verified model, builds in Release configuration, and runs the existing integration suite.

## Notes

- No image-editing features were added in this maintenance release.
- Copyright © 2026 Bedirhan Bayram. Designed & Developed by Axolotl Technology. Licensed under the MIT License.

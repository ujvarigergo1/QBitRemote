# Releasing QBitRemote

This project is currently version **1.0.0**.

For every public release, update the version in both places before building the artifacts:

- `QBitRemote/QBitRemote.csproj` (`Version`, `AssemblyVersion`, `FileVersion`, and `InformationalVersion`)
- `installer/QBitRemote.wxs` (`Package Version`)

Then build and verify the Windows application, publish the portable executable, and build the MSI. Upload only the release artifacts to the GitHub Release; do not commit `publish/`, `.wix/`, `.tools/`, installer output, or build output to the repository.

Before publishing, review `git status --ignored` and make sure no local settings, logs, credentials, or generated binaries are staged.

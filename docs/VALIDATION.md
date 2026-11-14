# Migration Validation

## Local import checks

| Check | Result |
| --- | --- |
| Original tracked files compared | 1,377 |
| Byte-identical imported files | 1,371 |
| Deliberate replacements | Six repository metadata / English documentation files |
| Unexpected source or asset changes | Zero |
| Main English documents checked | 10 |
| Relative documentation links checked | 33 |
| Embedded credential pattern scan | No matches in the inspected tracked text files |

The inventory retains SHA-256 hashes for each original tracked file. Original runtime source, assets, and measured terrain were preserved. Additional build helpers and presentation/report deliverables are identified separately.

## Packaged builds

Existing verified builds are repackaged for distribution. The Windows archive includes all 179 runtime files, excluding local logs, plus start instructions and third-party notices. The Quest APK is unchanged from the previously installed package.

Exact package sizes and SHA-256 hashes are in [PACKAGES.json](PACKAGES.json). The release includes a separate `SHA256SUMS.txt`.

## Application acceptance

Migration does not add new terrain, movement, or radio implementation. Existing application test evidence is summarized in [HANDOFF.md](../HANDOFF.md#verification-and-open-work). The latest physical confirmation of the Quest zone crosshair and laptop-to-Quest voice fix remains pending feedback.

## Remote verification

Completed on 14 November 2026:

- A fresh, independent clone downloaded the repository from GitHub.
- All 1,377 source-inventory entries matched their recorded import hashes.
- All 356 Git LFS files were downloaded; `git lfs fsck --objects HEAD` passed.
- Main English documentation passed local-link and code-fence checks.
- The published Windows and Quest release assets matched local sizes and GitHub SHA-256 digests.
- Every one of the 179 archived Windows runtime files matched the existing build; archive CRC validation passed.
- The repository remained private. Its collaborator list contained only `Abdallah-Jibril`.

The owner completed GitHub's identity confirmation. The earlier `Abdallah-Jibril/MarsPath` remote was then deleted. An authenticated API check returned HTTP 404 for the retired repository while the new private repository remained accessible. The original working folder and verified local history bundle remain available independently of either remote.

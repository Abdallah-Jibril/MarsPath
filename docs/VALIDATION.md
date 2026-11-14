# Migration Validation

## Local import checks

| Check | Result |
| --- | --- |
| Original tracked files compared | 1,377 |
| Byte-identical imported files | 1,371 |
| Deliberate replacements | Six repository metadata / English documentation files |
| Unexpected source or asset changes | Zero |
| Main English documents checked | 10 |
| Relative documentation links checked | 32 before this validation link was added |
| Embedded credential pattern scan | No matches in the inspected tracked text files |

The inventory retains SHA-256 hashes for each original tracked file. Original runtime source, assets, and measured terrain were preserved. Additional build helpers and presentation/report deliverables are identified separately.

## Packaged builds

Existing verified builds are repackaged for distribution. The Windows archive includes all 179 runtime files, excluding local logs, plus start instructions and third-party notices. The Quest APK is unchanged from the previously installed package.

Exact package sizes and SHA-256 hashes are in [PACKAGES.json](PACKAGES.json). The release includes a separate `SHA256SUMS.txt`.

## Application acceptance

Migration does not add new terrain, movement, or radio implementation. Existing application test evidence is summarized in [HANDOFF.md](../HANDOFF.md#verification-and-open-work). The latest physical confirmation of the Quest zone crosshair and laptop-to-Quest voice fix remains pending feedback.

## Remote verification

Remote privacy, repository checkout, large-file availability, and release assets are checked before retiring the earlier remote. The final migration update records the outcome here.

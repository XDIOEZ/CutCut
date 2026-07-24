---
name: publish-cutcut-release
description: Directly package and publish an official CutCut release after the user explicitly requests packaging or publishing. This fast path skips standalone validation, candidate builds, pull requests, CI waits, and post-release verification.
---

# Publish CutCut Release

Use this direct-release workflow only when the user explicitly asks to package or publish.
Keep generated files under `artifacts/`; never update `Relase/`.

## 1. Select the version

Read `AGENTS.md` and `docs/project-memory.md`. Read the current project version and latest
public GitHub Release only to select the release number. Use the next patch version unless the
user selected another version.

## 2. Prepare the release commit

Update the main project version, version assertions, release documentation, and project memory.
Keep the stable asset names unchanged:

- `complete-lightweight-win-x64.zip`
- `complete-portable-win-x64.zip`
- `complete-lightweight-full-win-x64.zip`
- `complete-full-win-x64.zip`
- seven `*-addon-win-x64.zip` module packages
- `SHA256SUMS.txt`

Commit the current coherent worktree directly on `main` and push it to `origin/main`. Do not
create a release branch or pull request.

## 3. Package and publish directly

Do not run standalone format checks, logic tests, UI smoke tests, candidate builds,
`-ValidateOnly`, CI waits, download verification, or GitHub Pages inspection.

Generate the final assets once:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Publish-Release.ps1 `
  -OutputRoot ".\artifacts\release-v$Version-final"
```

The packaging and uploader scripts retain their built-in file-presence, size, and digest
handling because those steps are part of producing and uploading usable assets, not a separate
validation pass.

Write concise release notes and publish immediately:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File .\.agents\skills\publish-cutcut-release\scripts\Publish-GitHubRelease.ps1 `
  -Version $Version `
  -AssetRoot ".\artifacts\release-v$Version-final" `
  -NotesPath ".\artifacts\release-v$Version-notes.md"
```

If the uploader leaves a partial draft, rerun with `-ResumeDraft`. Never overwrite an existing
public release.

## 4. Report

Report the pushed commit, Release URL, and generated asset sizes returned by the scripts. Do
not perform an additional post-release verification pass unless the user explicitly requests
one.

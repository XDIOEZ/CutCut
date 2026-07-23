---
name: publish-cutcut-release
description: Perform official CutCut releases in this repository, including version bumping, validation, packaging, GitHub pull-request merge, GitHub Release upload, checksum verification, and GitHub Pages checks. Use when the user explicitly asks to package, publish, upload release assets, or create a new formal CutCut version. Do not publish merely because feature work is complete.
---

# Publish CutCut Release

Follow this sequence for formal releases of `XDIOEZ/CutCut`. Keep generated files under
`artifacts/`; never update `Relase/`. Stop before publish actions unless the user explicitly
requested packaging or release in the current request.

## 1. Establish scope

1. Read `AGENTS.md` and `docs/project-memory.md`.
2. Verify the project version, latest GitHub Release, `origin/main`, `gh auth status`, and
   worktree diff.
3. Start `codex/release-<version>` from current `origin/main`. Use the next patch version
   unless the user selected another version.
4. Preserve unrelated worktree changes; stage only release files.

## 2. Prepare the version

Update the project version, version assertions, release-page asset rules, documentation, and
project memory. Keep these stable Release assets unless an intentional contract change updates
every producer and consumer:

- `complete-lightweight-win-x64.zip`
- `complete-portable-win-x64.zip`
- `complete-lightweight-full-win-x64.zip`
- `complete-full-win-x64.zip`
- six `*-addon-win-x64.zip` module packages
- `SHA256SUMS.txt`

Keep software auto-update selection limited to the lightweight and portable packages. The two
all-plugin packages remain manual-download choices.

## 3. Validate before commit

Run:

```powershell
dotnet format .\ScreenshotTool.sln --verify-no-changes
dotnet run --project .\tests\ScreenshotTool.LogicTests\ScreenshotTool.LogicTests.csproj -c Release
node --check .\site\release.js
$verificationRoot = Join-Path (Resolve-Path .) "artifacts\build-verification-v$Version"
dotnet build .\ScreenshotTool.sln -c Release -p:UseArtifactsOutput=true "-p:ArtifactsPath=$verificationRoot"
```

Run relevant `ScreenshotTool.UiPreview` smoke modes and inspect their PNG output when UI or
version display changed. Test the release page locally in a browser when `site/**` changed.

Generate a candidate in a fresh directory:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Publish-Release.ps1 `
  -OutputRoot ".\artifacts\release-v$Version-candidate"
```

Record measured sizes in `docs/project-memory.md`. Run the bundled uploader with
`-ValidateOnly` against the candidate. Confirm every complete ZIP contains
`ScreenshotTool.exe` with the target version and all required modules.

## 4. Commit and rebuild exact assets

1. Commit the coherent release changes.
2. Re-run `Publish-Release.ps1` into a fresh `artifacts/release-v<version>-final` directory.
3. Re-run the bundled uploader with `-ValidateOnly`.
4. Confirm package `ProductVersion` includes the release commit SHA.
5. Push, create a ready PR when the user requested publishing, wait for checks, verify the PR
   file list, and merge to `main`.

Do not upload artifacts built from an uncommitted tree or a commit that becomes orphaned after
rebasing.

## 5. Publish

After the PR merge is visible on remote `main`, write release notes and run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File .\.agents\skills\publish-cutcut-release\scripts\Publish-GitHubRelease.ps1 `
  -Version $Version `
  -AssetRoot ".\artifacts\release-v$Version-final" `
  -NotesPath ".\artifacts\release-v$Version-notes.md"
```

The script creates a draft first, uploads all expected assets, compares GitHub size and digest
with local files, then publishes. If it stops with a partial draft, inspect it and rerun with
`-ResumeDraft`; never overwrite an existing public release.

## 6. Verify production

1. Confirm the tag commit equals remote `main`, the Release is public and not a prerelease, and
   all eleven assets have GitHub `sha256:` digests matching local files.
2. Re-download at least `SHA256SUMS.txt`, the lightweight package, and the portable package;
   compare sizes and SHA-256. Prefer all assets when bandwidth permits.
3. Wait for `.github/workflows/pages.yml`, then inspect the deployed page. Verify version text,
   button copy, exact `href`, `download` attributes, and all four complete-package choices.
4. Report the Release URL, PR URL, merge commit, asset sizes, checks, and any incomplete
   verification. Leave the worktree clean.

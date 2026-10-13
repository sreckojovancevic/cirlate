<p align="center"><img src="assets/cirlat.png" width="96" alt="CIRLAT icon"></p>

<h1 align="center">CIRLAT</h1>

<p align="center">Bulk-rename Serbian Cyrillic and Latin file names to plain ASCII Latin. Preview first, undo anytime, and never touches the system.</p>

<p align="center">
  <img alt="Windows 11" src="https://img.shields.io/badge/tested%20on-Windows%2011-0078D4">
  <img alt=".NET 10 WinForms" src="https://img.shields.io/badge/.NET%2010-WinForms-512BD4">
  <img alt="License: MIT" src="https://img.shields.io/badge/license-MIT-green">
</p>

---

## What it does

| Original | Result |
|---|---|
| `Извештај.docx` | `Izvestaj.docx` |
| `Ђорђе.docx` | `Djordje.docx` |
| `ЉУБАВ.txt` | `LJUBAV.txt` |
| `Џемпер.jpg` | `Dzemper.jpg` |
| `Čačak_Šabac.docx` | `Cacak_Sabac.docx` |
| `Đorđe_Ćirić.pdf` | `Djordje_Ciric.pdf` |

- Converts all 30 Serbian Cyrillic letters, and Latin `č ć š ž đ`, into ASCII Latin (`đ`/`ђ` → `dj`, `џ` → `dz`).
- Digraphs follow the case of the word: `Љубав` → `Ljubav`, `ЉУБАВ` → `LJUBAV`.
- Normalizes Unicode (NFC) first, so names created on a Mac or by web forms (`c` + combining caron) are converted too.
- **Changes names only, never file contents.** Extensions are kept as they are.

Why: Cyrillic and diacritic file names break in mail attachments, old systems, ZIP tools, scripts and web uploads. Plain ASCII names don't.

## Safety

**Protected locations are completely off-limits.** CIRLAT doesn't let you pick them, doesn't scan them and doesn't rename anything inside them. Nothing is hard-coded, because the system drive isn't always `C:`:

1. **Real system locations, asked from Windows:** Windows, Program Files (both), ProgramData, the profiles folder (even when it's `D:\Users`), and every user profile on the machine.
2. **The same system names at the root of every drive and share:** `Windows`, `Program Files`, `ProgramData`, `Users`, `$Recycle.Bin`, `System Volume Information`, `Recovery` and others. This covers dual boot, an old system disk attached as another letter, and a hand-made `D:\Program Files`.
3. **Folders of installed programs**, wherever they are (`InstallLocation` from the Uninstall registry keys).

Within your own profile, only the **contents** of Desktop, Documents, Downloads, Pictures, Music, Videos and OneDrive/SharePoint sync folders can be processed. Those folders themselves are never renamed, and everything else in the profile (`AppData`…) is protected. Drive roots, Hidden+System items and reparse points (junctions, symlinks) are skipped. SUBST drives and mapped network drives are resolved to their real path before any check. CIRLAT refuses to run elevated.

**Nothing is overwritten.** Every proposed name is checked first, and the item gets a status:

| Status | Meaning |
|---|---|
| Spreman | Ready to rename |
| Bez promene | Already plain ASCII |
| Konflikt naziva | Another item already has or would get that name. Case-insensitive, like Windows. Left as is by default, or resolved with a suffix (see below) |
| Nevažeći naziv | Non-Serbian non-ASCII characters remain (`é`, `–`, emoji…); a Windows-invalid or reserved name (`CON`, `LPT1`…); or the path would cross 260 characters. Unchecked by default |
| Greška pristupa | The folder could not be read |

**Undo.** Every successful rename is written immediately to a journal in `%LOCALAPPDATA%\Cirlat\journal\`. *Poništi poslednju operaciju* restores the original names, even after a restart or a crash. Anything that can't be restored stays in the journal so you can try again.

Folders are renamed bottom-up, so paths to their contents stay valid during the run.

## Usage

1. **Dodaj folder…** to add one or more folders. You can also drag them onto the list.
2. Choose whether to include subfolders and what to process (files, folders or both).
   Optionally tick **Konflikte reši sufiksom**. A name that is already taken then becomes `Sabac (2).txt`, `Sabac (3).txt`… instead of being skipped. The suffix shows up in the preview before anything is renamed.
3. **Analiziraj** shows a table with the original name, proposed name, status and check result.
4. Untick anything you want to leave out. You can also right-click to include or exclude selected rows.
5. **Izvrši preimenovanje** asks for confirmation, then renames and shows a report.
6. **Izvoz izveštaja…** saves the full table as CSV, which opens correctly in Excel.

> Renaming can break shortcuts (`.lnk`), links between Excel/Word files and Office's recent-files list. CIRLAT warns before every run.

## Download

Get `Cirlat.exe` from [Releases](../../releases). It's a single file, with no installation and no .NET required.

> The executable is not code-signed. On first run SmartScreen may show *"Windows protected your PC"* → **More info** → **Run anyway**.

## Build

Requirement: **.NET 10 SDK** (Visual Studio 2026, or the standalone SDK).

```
build.cmd
```

The script runs the tests, then publishes `publish\Cirlat.exe` (self-contained single file). WinForms doesn't support trimming or NativeAOT, so the executable is larger than a native tool would be.

## Tests

```
dotnet run --project tests\Cirlat.Tests
```

These cover all 30 Cyrillic letters in both cases, digraph capitalization, NFD input, ligature characters, extensions, Windows name rules, case-insensitive conflicts and path containment.

## Tested

Tested on **Windows 11** by the author, on real work folders:

- [x] Build with `build.cmd`: 52 tests pass, and the publish produces a single `Cirlat.exe`
- [x] Protected locations are refused: Program Files, `C:\Users`, `AppData`, folders of installed programs and drive roots
- [x] Renaming a real folder tree with mixed Cyrillic/Latin names: nested folders were renamed bottom-up, extensions (including `.PDF` in upper case) were kept, and all-caps digraphs came out right (`МЉМПДД` → `MLJMPDD`)
- [x] CSV report opens correctly in Excel
- [x] Undo restored every renamed item, including a renamed folder and the file inside it

Not tested yet:

- [ ] Conflict suffix option on real data
- [ ] OneDrive/SharePoint sync folders
- [ ] Windows 10

## Troubleshooting the build

**`MSB3030: Could not copy the file …\obj\…\apphost.exe because it was not found`**

Security software is deleting the freshly built executable. The SDK writes `apphost.exe`, and within milliseconds a static-analysis engine deletes it. On the author's machine this was **HP Sure Sense** (Deep Instinct engine, process `DeepStaticService.exe`). F-Secure and Defender exclusions made no difference. Process Monitor (filter: *Path contains `apphost`*) shows which process does the delete.

Fix: add an exclusion for the project folder in that product, or remove it from the development machine. The same engine may also delete `Cirlat.exe` on users' machines, so code-signing the executable is the long-term fix for distribution.

## Project layout

```
src/Cirlat.Core/
  Transliterator.cs   Cyrillic/Latin → ASCII, digraph case rules, NFC
  NameRules.cs        ASCII and Windows name checks, statuses
  ProtectedPaths.cs   protected locations (3 layers), user-folder whitelist
  PathResolver.cs     canonical paths: SUBST, mapped drives, 8.3 names
  Scanner.cs          folder walk, protection, siblings for conflict checks
  RenamePlan.cs       conflicts (case-insensitive), 260-char check
  Executor.cs         bottom-up rename, no overwrite, undo
  Journal.cs          persistent operation log
src/Cirlat/           WinForms UI
tests/Cirlat.Tests/   test runner, no external packages
docs/CIRLAT-specifikacija.md   specification (Serbian)
```

## Authors

CIRLAT was made by a person and an AI working together.

**[Srećko Jovančević](https://github.com/sreckojovancevic): idea, specification, decisions, testing**
- Came up with CIRLAT and wrote the original specification ([`docs/CIRLAT-specifikacija.md`](docs/CIRLAT-specifikacija.md)): mandatory ASCII names, the `đ → dj` and `џ → dz` rules, preview before rename, no overwriting, undo
- Set the core safety rule: *system folders, Program Files, user profiles and system files must be completely off-limits*, and pointed out that the system and profiles don't have to be on `C:`
- Asked for the optional conflict suffix
- Built and tested it on Windows 11 with real work folders, and tracked down the build failure with Process Monitor

**Claude (Anthropic): review, code, icon, documentation**
- Reviewed the specification and found the gaps: all-caps digraphs (`LJUBAV`, not `LjUBAV`), NFD names from Mac, the conflict between the ASCII criterion and "don't change other characters", case-insensitive conflicts, names getting longer (260 chars), broken links, persistent undo
- Designed the three-layer protection with no hard-coded paths
- Wrote all of the code, the tests and the build script
- Diagnosed the `apphost.exe` build failure from MSBuild and Process Monitor logs
- Designed the icon (an original drawing, `assets/cirlat.ico`)
- Wrote this README and the additions to the specification

## Acknowledgments

- **Claude (Anthropic)**: CIRLAT's code, tests, build script, icon and documentation were written by Claude, an AI model by Anthropic, working with Srećko in one conversation. Claude reviewed the specification, designed the protection model, wrote and tested the core, and diagnosed the build failure from MSBuild and Process Monitor logs. The idea, the specification, the decisions and the testing on real data are Srećko's.
- **[Sysinternals Process Monitor](https://learn.microsoft.com/sysinternals/downloads/procmon)**: it showed which process was deleting the build output.
- **.NET and Windows Forms**: the runtime and UI framework CIRLAT is built on.
- **Short**, the sister project ([sreckojovancevic/short](https://github.com/sreckojovancevic/short)): it shares CIRLAT's approach to protected paths and SUBST resolution.

## License

MIT. See [LICENSE](LICENSE).

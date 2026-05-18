# Solicen.UEExtractor

[**English**](/README.md) | [**Русский**](./docs/ru/README.ru.md)

Made with ❤️ for **all** translators and translation developers.

This a tool on **.NET 9.0** to extract text from any game on [Unreal Engine](https://www.unrealengine.com/) (4.0 - 5.8).<br>Using [CUE4Parse](https://github.com/FabianFG/CUE4Parse) to work with Unreal Engine archives **`.pak`** and **`.utoc`**.

With it, you will receive a `locresCSV` file for localization of the game based on its resources.

> [!IMPORTANT]
> Now available **fully functional extraction** of `DataTable`, `StringTable` and **direct `.locres` reading** for games that ship pre-compiled localization binaries.

## LocresCSV Structure:
Will be imported or converted to `.locres` file.
- key = unique string of 32 characters.
- source = string from decoded text data
- Translation = ***null*** from unpacked resources
```
key,source,Translation
4A6FDB1549E45F6C5D8D739129686E2F,Default,
```
*Importing a **Translation column** from CSV is possible via [UE4localizationsTool](https://github.com/amrshaheen61/UE4LocalizationsTool).*

## Preparation:
### For games running on Unreal Engine 4 before [ZenLoader](https://dev.epicgames.com/documentation/en-us/unreal-engine/zen-loader-in-unreal-engine).
Find out if your game requires an **AES key** or not, as it may not be needed and the steps can be skipped.

> [!TIP]
> **The engine version and game type are detected automatically** from the game executable.
> - For many well-known games (see [Supported games](#supported-games)) no `-v` flag is needed at all.
> - You can still override with `-v=UE5_1` if auto-detection gives wrong results.
> - All UE version values come from the CUE4Parse library; find the right one via FModel.

1. You will **need to have an AES key** to view the archives and retrieve data from them.
   - You can find it online if you don't have it, or find it yourself from the resources.
2. Place `aes.txt` in the main game directory with one line as 32-character hex string.
   - Or provide it with the argument `--aes=<key>`. Key must start with `0x`.

### For games running on Unreal Engine 4-5 with [ZenLoader](https://dev.epicgames.com/documentation/en-us/unreal-engine/zen-loader-in-unreal-engine).
First, follow the steps described above, and only then continue.

> [!IMPORTANT]
> You need to get the `.usmap` file to access the game's resources and archives.
> - As before, you can find this file on the Internet, if you don't have it.
> - Check [nexusmods](https://www.nexusmods.com/) and modding forums for its availability.

1. Place the `.usmap` file in the main directory of the game (or a subdir), and the tool will find it automatically.
2. The preparation for the work is completed.

## Supported games

The following games are **auto-detected by folder or executable name** — no `-v` flag required.
Game-specific pak formats, index offsets, and custom encryption are applied automatically via CUE4Parse.

| Game | Notes |
|------|-------|
| Neverness To Everness | Custom pak index offset; `.locres` files are encrypted (handled automatically) |
| Ash Echoes | Custom file provider |
| Wuthering Waves / KuroGames | Partial-encryption pak format |
| inZOI | Custom FPakInfo offset |
| Marvel Rivals | Custom encryption (auto-configured by CUE4Parse, no AES key needed) |
| Dead by Daylight | Custom encryption (auto-configured by CUE4Parse, no AES key needed) |
| FragPunk | Custom global IoStore handling |
| Infinity Nikki | Custom encryption (auto-configured by CUE4Parse, no AES key needed) |
| Snowbreak: Containment Zone | — |

> [!TIP]
> Detection works on the **game folder name** and the **main `.exe`** name (spaces, hyphens and underscores are ignored).
> If your game is not listed, use `-v=<UE_version>` to specify the version manually.

## Merging CSV
> [!NOTE]
> It happens automatically when you re-run the extraction of the same game (in the same folder).
> * **Very useful when the game is being updated and you need to get new lines without losing the translation.**
>
> **Exactly how it works:**
> - If the row contains values in only 2 columns (`key` & `source`) and the `Source` value in the `Key` does not match — the `Source` value from the past is added as a `Translation`. *(As an example when exporting from UE4localizationsTool)*
> - Otherwise, if it matches, the value from the `Translation` column is written to `Translation`.
> - Lines present in the previous file but not in the new one are preserved.

## Using:
* [Download](https://github.com/SolicenTEAM/UEExtractor/releases) and **drag & drop** a folder onto `UEExtractor.exe` to parse the whole game directory and get `<dir_name>.csv`.
* Or use `UEExtractor.exe` in the command line with arguments.

### UEExtractor - Unreal Engine (Text) Extractor
- **Drag & drop** the game folder onto `UEExtractor.exe` to get `<dir_name>_locres.csv`.
- **Drag & drop** a `locresCSV` file onto `UEExtractor.exe` to get a `.locres` file.
- Or use the advanced options below via CMD.

#### Extract *LocresCSV* from game:

```cmd
UEExtractor.exe <dir_path> [output_csv] [arguments...]
```

#### Create *.locres* from csv file:
```cmd
UEExtractor.exe <csv_path> <output_locres>
```

#### Translate with a local LLM (Ollama, LM Studio, vLLM, …):
```cmd
UEExtractor.exe <dir_path> --api:url=http://localhost:11434/v1/ --api:model=llama3 --lang:from=en --lang:to=it
```
Any server that exposes an OpenAI-compatible `/v1/chat/completions` endpoint works. No `--api:key` is required for local servers.

#### Extract to a directory — one CSV per locres file:
```cmd
UEExtractor.exe <dir_path> K:\output\ --path=HT/Content/Localization/Game/en/
```
When the second argument is a directory path (ends with `\` or `/`), each `.locres` file gets its own CSV named after it (e.g. `Game.csv`). If two locres files share the same name (from different pak chunks), the pak chunk name is appended: `Game_pakchunk0-Windows.csv`.

#### Scan only a known internal path (much faster):
```cmd
UEExtractor.exe <dir_path> --path=HT/Content/Localization
```
If you already know where the localization lives (e.g. from FModel), use `--path` to skip scanning the entire game.

### Arguments

| Argument | Short | Description |
|----------|-------|-------------|
| `--version=<ver>` | `-v` | Set the engine version (e.g. `-v=UE5_6`, `-v=Stalker2`). Auto-detected when omitted. |
| `--aes=<key>` | `-a` | 32-character hex AES key (must start with `0x`). |
| `--path=<virtual_path>` | `-path` | Restrict processing to assets under a specific internal path (e.g. `--path=HT/Content/Localization`). Case-insensitive substring match. |
| `--verbose` | `-vb` | Show per-file processing details and diagnostic info instead of the progress bar. |
| `--skip-uexp` | `-s:xp` | Skip `.uexp` files during processing. |
| `--skip-uasset` | `-s:et` | Skip `.uasset` files during processing. |
| `--locres` | | Write a `.locres` file after parsing. |
| `--all` | `-all` | Process all folders in the archive (including effects, meshes, sounds, etc.). |
| `--underscore` | `-un` | Do not skip lines with underscores: **ex_string** |
| `--upper-upper` | `-up` | Skip lines with ALL UPPERCASE: **EXAMPLE** |
| `--no-parallel` | `-n:p` | Disable parallel processing (slower; may surface additional data). |
| `--table-format` | `-tf` | Replace the standard `,` separator with `\|`. |
| `--headmark` | `-m` | Include header and footer in the `.csv`. |
| `--auto-exit` | `-exit` | Exit automatically after all processes complete. |
| `--invalid` | `-i` | Include invalid data in the output. |
| `--qmarks` | `-q` | Forcibly add quotation marks around text strings. |
| `--hash` | `-h` | Include hash in the key: `[key][hash],<string>`. |
| `--url` | `-url` | Include file path in the key: `[url][key],<string>`. |
| `--picky` | `-p` | Picky mode — displays more detailed per-file information. |
| `--table:only:key=<name>` | `-t:o:k` | Include only entries whose key/name matches the given value. |
| `--lang:from=<code>` | `-l:f` | Source language for translation (e.g. `en`). |
| `--lang:to=<code>` | `-l:t` | Target language for translation (e.g. `ru`). |
| `--api:key=<key>` | `-a:key` | API key for OpenRouter or any server that requires authentication. |
| `--api:url=<url>` | `-a:url` | Custom OpenAI-compatible base URL for a local model (e.g. `http://localhost:11434/v1/` for Ollama). Omit for OpenRouter. |
| `--api:model=<model>` | `-a:model` | Model name to use (e.g. `tngtech/deepseek-r1t2-chimera:free` for OpenRouter or `llama3` for Ollama). |
| `--batch-size=<n>` | `-bs` | Number of segments sent per translation request (default: 150). Lower for models with small context. |
| `--update` | | Check for a new version on GitHub and update if available. |
| `--help` | | Show help information. |

## Contributions:
* You can create your own fork of this project and contribute to its development.
* You can also contribute via the [Issues](https://github.com/SolicenTEAM/UEExtractor/issues) and [Pull Request](https://github.com/SolicenTEAM/UEExtractor/pulls) tabs by suggesting your code changes.

## Thanks:
- [Ambi](https://github.com/JunkBeat) for his original script and idea to research.
- [Saipan](https://github.com/Saipan0) for help in researching the creation of a locres file.
- [FabianFG](https://github.com/FabianFG) for **CUE4Parse** library and FModel code example.

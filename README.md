# Solicen.UEExtractor

[**English**](/README.md) | [**Русский**](./docs/ru/README.ru.md)

Made with ❤️ for **all** translators and translation developers.

This a tool on **.NET 8.0** to extract text from any game on [Unreal Enigne](https://www.unrealengine.com/) (4.0 - 5.1).<br>Using [CUE4Parse](https://github.com/FabianFG/CUE4Parse) to work with Unreal Engine archives **`.pak`** and **`.utoc`**.

With it, you will receive a `locresCSV` file for localization of the game based on its resources.

> [!CAUTION]
> You will not be able to get strings from files that contain a **DataTable** structure.
> - I can *get* strings, but *you* can't operate these via `.locres`. 
> - Do not open a *issue* to solve this problem, I will not be able to solve it. 
> - It's not my fault, thank you for understanding.

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
> Not necessarily, but **you can specify the Unreal Engine version** `-v=UE5_1` that is used in the game.
> - It will be found automatically, but priority will be given to the argument if you so wish.
> - All UE version values come from CUE4Parse library, you can find out what you need through FModel.

1. You will **need to have an AES key** to view the archives and retrieve data from them. 
   - You can find it online if you don't have it, or find it yourself from the resources.
2. Place `aes.txt` to main game directory with one line as 32-character hex string.
   - Or provide his with argument `--aes=<key>`. Key must start with `0x`.

###  For games running on Unreal Engine 4-5 wtih [ZenLoader](https://dev.epicgames.com/documentation/en-us/unreal-engine/zen-loader-in-unreal-engine).
First, follow the steps described above, and only then continue. 

> [!IMPORTANT]
> You need to get the `.usmap` file to access the game's resources and archives. 
> - As before, you can find this file on the Internet, if you don't have it.
> - Check the [nexusmods](https://www.nexusmods.com/) and modding forums for its availability, I'm sure you'll find it.

1. Place the `.usmap` file in the main directory of the game (or a subdir), and the tool will find it automatically. 
2. The preparation for the work is completed.


## Using:
* Or [download](https://github.com/SolicenTEAM/UEExtractor/releases) and **drag & drop** folder to a command tool to parse whole game directory and get `<dir_name>.csv`.
* Or use `UEExtractor.exe` in command line to parse directory with arguments.


### UEExtractor - Unreal Engine (Text) Extractor
- You can simply drag and drop directory with Unreal resources onto `UEExtractor.exe` to parse directory and get `<dir_name>.csv`. 
- Or you can simple drag and drop CSV with `locresCSV` structure onto `UEExtractor.exe`  to get `.locres` file.
- Or use more advanced options with CMD.

#### Extract *LocresCSV* from game:

```cmd
UEExtractor.exe <dir_path> <output_csv> 
```

#### Create *.locres* from csv file:
```cmd
UEExtractor.exe <csv_path> <output_locres>
```

| Argument | Description |
|----------|-------------|
| -v=UE5.1, --version=Stalker2 | specify the Unreal Engine version.
| -a=[key], --aes=[key] | 32-character hex string as AES key.
| --skip-uexp, --skip-uasset | skip `.uexp` or `.uasset` files during the process.
| --locres | write .locres file after parsing.
| --underscore | do not skip line with underscores: **ex_string**
| --upper-upper | do not skip line with upperupper: **EXAMPLE**.
| --table-format | replace standard separator **`,`** symbol to **`\|`**
| --headmark | include header and footer in the `csv`.
| --autoexit | automatically exit after execution all processes.
| --invalid | include invalid data in the output.
| --qmarks | forcibly adds quotation marks between text strings.
| --hash | inculde hash of string for locres ex: [key][hash],[string].
| --picky | picky mode, displays more annoying information.
| --url | include path to file, example: [url][key],[string].
| --help | show help information.

## Contributions:
* You can create your own fork of this project and contribute to its development.
* You can also contribute via the [Issues](https://github.com/SolicenTEAM/UEExtractor/issues) and [Pull Request](https://github.com/SolicenTEAM/UEExtractor/pulls) tabs by suggesting your code changes. And further development of the project. 

## Thanks:
- [Ambi](https://github.com/JunkBeat) for his original script and idea to research.
- [Saipan](https://github.com/Saipan0) for help in researching the creation of a locres file.
- [FabianFG](https://github.com/FabianFG) for **CUE4Parse** library and FModel code example.

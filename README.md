# Solicen.UEExtractor

[**English**](/README.md) | [**Русский**](./docs/ru/README.ru.md)

Made with ❤️ for **all** translators and translation developers.

This a script/tool to extract `locresCSV` from unpacked directory from the **`.pak` or `.utoc`** archive format [Unreal Enigne](https://www.unrealengine.com/) (4.0 - 5.1). 

With it, you will receive a `locresCSV` file for localization of the game based on its resources. However, `locresCSV` is not a special format, and this name is only used to separate from the regular CSV file, which it is anyway.

## LocresCSV Structure:
Will be imported or converted to `.locres` file.
- key = unique string of 32 characters.
- source = string from decoded text data
- Translation = ***null*** from unpacked resources
```
key,source,Translation
4A6FDB1549E45F6C5D8D739129686E2F,Default,
```

## Using:
* Or [download](https://github.com/SolicenTEAM/UEExtractor/releases) and **drag & drop** folder to a command tool to parse directory and get `<dir_name>.csv`.
* Or use `UEExtractor.exe` in command line to parse directory.

### UEExtractor - Unreal Engine (Text) Extractor
* You can simply drag and drop directory with Unreal resources onto `UEExtractor.exe` to parse directory and get `<dir_name>.csv`. 
* Or use more advanced options with CMD.

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
| --skip-uexp, --skip-uasset | skip `.uexp` or `.uasset` files during the process.
| --locres | write .locres file after parsing.
| --underscore | do not skip line with underscores: **ex_string**
| --upper-upper | do not skip line with upperupper: **EXAMPLE**.
| --no-parallel | disable parallel processing, slower, may output additional data.
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

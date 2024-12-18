# Solicen.UEExtractor

[**Englsih**](/README.md) | [**Русский**](./docs/ru/README.ru.md)

Made with ❤️ for **all** translators and translation developers.

This a script/tool to extract `LocresCSV` from unpacked directory from the **`.pak` or (`.utoc`|`.ucas`)** archive format from the [Unreal Enigne](https://www.unrealengine.com/). 

With it, you will receive a `LocresCSV` file for localization of the game based on its resources. However, `LocresCSV` is not a special format, and this name is only used to separate from the regular CSV file, which it is anyway.

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
| --skipuexp, --skipasset | skips `.uexp` or `.uasset` files during the process.
| --locres | write .locres file after parsing.
| --underscore | do not skips lines with underscores.
| --noparallel | disable parallel processing, slower, may output additional data.
| --invalid | include invalid data in the output.
| --qmarks | forcibly adds quotation marks between text strings.
| --help | Show help information.

## Contributions:
* You can create your own fork of this project and contribute to its development.
* You can also contribute via the [Issues](https://github.com/SolicenTEAM/UEExtractor/issues) and [Pull Request](https://github.com/SolicenTEAM/UEExtractor/pulls) tabs by suggesting your code changes. And further development of the project. 

## Thanks:
- [Ambi](https://github.com/JunkBeat) for his original script and idea to research.
- [Saipan](https://github.com/Saipan0) for help in researching the creation of a locres file.

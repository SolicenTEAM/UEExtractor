# Solicen.UEExtractor

[**Englsih**](/README.md) | [**Русский**](./docs/ru/README.ru.md)

Спасибо автору оригинального скрипта `Ambi` за его скрипт и идею для исследования. <br>
Сделано с ❤️ для **всех** переводчиков и разработчиков переводов.

Это скрипт/инструмент для извлечения `LocresCSV` из **`.pak` или (`.utoc`|`.ucas`)** файлов архивов [Unreal Enigne](https://www.unrealengine.com/).

С его помощью вы получите файл `LocresCSV` для локализации игры на основе ее ресурсов. Однако `LocresCSV` не является специальным форматом, и это название используется только для отделения от обычного файла CSV, которым он и является.

# Структура LocresCSV:
Может быть импортирована или преобразована в файл `.locres`.
- key = хэш (строка из 32 символов) 
- source = расшифрованная строка
- Translation = (Null из распакованных ресурсов)
```
# UnrealEngine .locres asset
key,source,Translation
4A6FDB1549E45F6C5D8D739129686E2F,Default,
# Extracted with UEExtractor & Solicen Translation Tool
```

## Использование:
* Или [загрузите](https://github.com/Szolicentrum/UEExtractor/releases) и **перетащите папку** на `UEExtractor.exe`, чтобы проанализировать каталог и получить `<имя>.csv`.
* Или используйте `UEExtractor.exe` в командной строке для полного анализа каталога.

### UEExtractor - Unreal Engine (текстовый) извлекатор
* **Перетащите папку** с ресурами игры на `UEExtractor.exe`, чтобы проанализировать каталог и получить `<имя>.csv`. 
* Или воспользоваться более расширенными параметрами через CMD.

```cmd
UEExtractor.exe <путь_каталога> <имя_csv> 
```

| Аргумент | Описание |
|----------|-------------|
| --skipuexp, --skipasset | пропускает файлы `.uexp` или `.uasset` во время процесса.
| --underscore | не пропускать строки с подчеркиванием.
| --noparallel | отключить параллельную обработку, медленее, может выдать дополнительные данные. 
| --invalid | включить невалдиные данные в вывод.
| --qmarks | принудительно добавляет кавычки между текстовыми строками.
| --help | отображает справочную информацию.


## Ваш вклад:
* Вы можете создать свой собственный форк этого проекта и внести свой вклад в его развитие.
* Вы также можете внести свой вклад через вкладки [Issues](https://github.com/SolicenTEAM/UEExtractor/issues) и [Pull Request](https://github.com/SolicenTEAM/UEExtractor/pulls), предложив свои изменения кода.<br>И дальнейшее развитие проекта. 
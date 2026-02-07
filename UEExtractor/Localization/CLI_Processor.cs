using Solicen.CLI;

namespace Solicen.Localization.UE4
{
	class CLI_Processor
	{
        private static bool ProgramAutoExit = false;
        private static readonly List<Argument> arguments;
		static CLI_Processor()
		{
			arguments = new List<Argument>
			{
                new Argument("--aes", "-a", "32-character hex string as AES key", (key) => UnrealLocres.AES = key),
                new Argument("--all", "-all", "processing all folders in archive", () => UnrealLocres.AllFolders = true),
				new Argument("--picky", "-p", "picky mode, displays more annoying information", () => UnrealLocres.PickyMode = true),
				new Argument("--url", "-url", "include path to file, ex: [url][key],<string>", () => UnrealLocres.IncludeUrlInKeyValue = true),
				new Argument("--headmark", "-m", "include header and footer of the csv.", () => UnrealLocres.ForceMark = true),
				new Argument("--hash", "-h","include hash of string for locres ex: [key][hash],<string>.", () => UnrealLocres.IncludeHashInKeyValue = true),

				/* Отключено - Повреждено создание Локреса - Pre Release 1.0.6.2 
				new Argument("--locres", "write .locres file.", () => UnrealLocres.WriteLocres = true),
				*/
				new Argument("--version", "-v", "Set the engine version for correct processing (e.g., -v=5.1).", ProcessVersion),
                new Argument("--skip-uexp", "-s:xp","skip files with `.uexp` during the process", () => UnrealLocres.SkipUexpFile = true),
				new Argument("--skip-uasset", "-s:et","skip files with `.uasset` during the process", () => UnrealLocres.SkipUassetFile = true),
				new Argument("--underscore", "-un","do not skip lines with underscores.", () => UnrealUepx.SkipUnderscore = false),
				new Argument("--upper-upper", "-up","skip lines with UpperUpper.", () => UnrealUepx.SkipUpperUpper = true),
				new Argument("--no-parallel", "-n:p","disable parallel processing, slower, may output additional data.", () => UnrealUasset.parallelProcessing = false),
				new Argument("--invalid", "-i","include invalid data in the output.", () => UnrealUepx.IncludeInvalidData = false),
				new Argument("--qmarks", "-q", "forcibly adds quotation marks between text strings.", () => UnrealLocres.ForceQmarksOutput = true),
				new Argument("--table-format", "-tf", "replace standard separator , symbol to | ", () => UnrealLocres.TableSeparator = true),
				new Argument("--auto-exit", "-exit", "Exit automatically after execution", () => ProgramAutoExit = true),
				new Argument("--help", "-h", "Show help information", () => Argumentor.ShowHelp(arguments))
			};
		}

		static void ProcessVersion(string version)
        {
			if (string.IsNullOrWhiteSpace(version)) return;
			UnrealLocres.UEVersion = version.Replace(".", "_");
            UnrealLocres.EngineSpecified = true;
			UnrealArchiveReader.EngineSpecified = true;
		}

        public static void ProcessProgram(string[] args)
		{
            // 1. Разбираем аргументы и настраиваем конфигурацию
            var originalArgs = Argumentor.SplitArgs(args);
            var onlyArgs = Argumentor.Process(originalArgs, arguments);

            if (onlyArgs.Length > 0)
			{
                string? locres = null;
                if (onlyArgs.Contains(".csv")) // Обработка для получения .locres файла
				{
					return; // Отключено - Повреждено создание Локреса - Pre Release 1.0.6.2(3)
					string LocresCSV = args[0];
					if (args.Length > 1)			
                        locres = args.FirstOrDefault(x => x.Contains(".locres"));
                    
					if (!string.IsNullOrEmpty(locres)) // Если .locres
					{

						// Инициализируем новый экземпляр Хелпера
                        var lHelper = new LocresHelper();
						if (UnrealLocres.TableSeparator)                       
                            lHelper.Separator = '|'; // Если TableFormat,
													 // установить другой разделитель.

                        var Result = lHelper.LoadCSV(LocresCSV); // Прочитать результат из LocresCSV
                        UnrealLocres.WriteToLocres(Result, locres); // Записать .locres файл
                    }
				}
				else // Обычная обработка папки для получения LocresCSV
				{
					string folderPath = onlyArgs[0]; string fileName = "";
					locres = args.FirstOrDefault(x => x.Contains(".locres"));

					if (args.Length > 1)
					{
						fileName = onlyArgs[1] != locres ? onlyArgs[1] : "";
					}

					if (Directory.Exists(folderPath) && !string.IsNullOrEmpty(onlyArgs[0]))
					{
						CLI_Processor.ProcessFolder(folderPath, fileName, locres);
					}
				}
			}
		}


		public static void ProcessFolder(string folderPath, string fileName = "", string locresPath = "")
		{
			var exePath = new FileInfo(typeof(CLI_Processor).Assembly.Location).Directory;
			var csvPath = string.IsNullOrWhiteSpace(fileName)
				? $"{Path.GetFileName(folderPath)}_locres.csv"
				: $"{Path.ChangeExtension(fileName, ".csv")}";

			csvPath =  Path.Combine(exePath + "\\", csvPath);

			// CSV for skipped_lines during parsing lines to locresCSV.
			UnrealLocres.SkippedCSV = new CSV.Writer(Path.ChangeExtension(csvPath,"_skipped_lines.csv"));

			// Parsing and his result
			var Result = UnrealLocres.ProcessDirectory(folderPath);

			// If found previous CSV file load and analyze all rows and columns
			if (File.Exists(csvPath))
			{
				Console.WriteLine("\nFound previous CSV file, analyzing, that take a while...");
                var oldCSV = UnrealLocres.LoadFromCSV(csvPath);
				//Console.WriteLine($"Rows: [New:{Result.Count}] | [Old:{oldCSV.Length}]");
				int LinesToMergeInt = oldCSV.Where(x => Result.Any(r => r.Key == x.Key)).ToArray().Length;
				int NewLinesInt = oldCSV.Length - LinesToMergeInt;
				int TotalInt = Result.Count + NewLinesInt;

                Console.WriteLine($" - Extracted : {Result.Count}");
                Console.WriteLine($" - To merge  : {LinesToMergeInt}");
                Console.WriteLine($" - New rows  : {NewLinesInt}");
                Console.WriteLine($" - Total     : {TotalInt}");
		
                foreach (var line in oldCSV)
				{
					if (Result.Any(x => x.Key == line.Key))
					{
						var rLine = Result.ToList().FirstOrDefault(x => x.Key == line.Key);
						if (rLine.Key != null)
						{
							// Adds Translation value from CSV [Source] Column.
							if (rLine.Value.Source != line.Source && line.Translation == string.Empty)
                                Result[rLine.Key].Translation = line.Source;

                            // Adds Translation value from CSV [Translation] Column.
                            else if (line.Translation != string.Empty)
                                Result[rLine.Key].Translation = line.Translation;                        
                        }
					}
					else
					{
                        // If OldCSV line contains other keys add him to Result
                        Result.TryAdd(line.Key, new LocresResult(line.Key, line.Source, line.Translation, line.Namespace));
					}
				}
			}

			UnrealLocres.WriteToCsv(Result, csvPath);
			Console.WriteLine($"\nCompleted! File saved to: {csvPath}");

			if (UnrealLocres.WriteLocres && locresPath != null) 
				UnrealLocres.WriteToLocres(Result, locresPath);   
			if (ProgramAutoExit) Environment.Exit(0);
		}

	}
}

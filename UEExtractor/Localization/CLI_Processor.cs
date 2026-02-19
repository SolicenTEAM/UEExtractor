using Solicen.CLI;
using Solicen.Translator;
using System.Collections.Concurrent;

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

				new Argument("--locres", null, "Write .locres file after process.", () => UnrealLocres.WriteLocres = true),
			
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
				new Argument("--help", "-h", "Show help information", () => Argumentor.ShowHelp(arguments)),

                new Argument("--lang:from", "-l:f", "Set the source language for translation (e.g., --lang:from=en).", (lang) => UberTranslator.LanguageFrom = lang),
                new Argument("--lang:to", "-l:t", "Set the target language for translation (e.g., --lang:to=ru).", (lang) => UberTranslator.LanguageTo = lang),
                new Argument("--api:model", "-a:model", "Set model for OpenRouter (e.g, -a:model=tngtech/deepseek-r1t2-chimera:free)", (model) => UberTranslator.OpenRouterModel = model),
                new Argument("--api", null, "Set API key for OpenRouter.", (key) => UberTranslator.OpenRouterApiKey = key),
            };
		}

		static void ProcessVersion(string version)
        {
			if (string.IsNullOrWhiteSpace(version)) return;
			version = char.IsDigit(version[0]) ? "UE" + version : version;	
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
                if (onlyArgs[0].Contains(".csv")) 
				{
                    // Обработка для получения .locres файла
                    string LocresCSV = args[0];
                    var Result = UnrealLocres.LoadFromCSV(LocresCSV); // Прочитать результат из LocresCSV
                    if (UberTranslator.OpenRouterApiKey != string.Empty)
					{
                        CLI.Console.WriteLine();
                        UnrealLocres.ProcessTranslator(ref Result);
						UnrealLocres.WriteToCsv(Result.ToConcurrent(), LocresCSV);
                        CLI.Console.WriteLine($"[Green]Completed! Changes saved to: {LocresCSV}");
                    }

                    LocresWriter.LocresCompactWriter.WriteToFile($"{Path.GetFileNameWithoutExtension(LocresCSV)}.locres", Result);
                    CLI.Console.WriteLine($"[Green]Completed! File saved to: {Path.GetFileNameWithoutExtension(LocresCSV)}.locres");


                }
				else // Обычная обработка папки для получения LocresCSV
				{
					string folderPath = onlyArgs[0]; string fileName = "";
					locres = args.FirstOrDefault(x => x.Contains(".locres"));

					if (onlyArgs.Length > 1)
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
                CLI.Console.WriteLine("\n[Yellow]Found previous CSV file, analyzing, that take a while...");
                var oldCSV = UnrealLocres.LoadFromCSV(csvPath);
				//Console.WriteLine($"Rows: [New:{Result.Count}] | [Old:{oldCSV.Length}]");
				int LinesToMergeInt = oldCSV.Where(x => Result.Any(r => r.Key == x.Key)).ToArray().Length;
				int NewLinesInt = oldCSV.Length - LinesToMergeInt;
				int TotalInt = Result.Count + NewLinesInt;

                CLI.Console.WriteLine($" - Extracted : {Result.Count}");
                CLI.Console.WriteLine($" - To merge  : {LinesToMergeInt}");
                CLI.Console.WriteLine($" - New rows  : {NewLinesInt}");
                CLI.Console.WriteLine($" - Total     : {TotalInt}");
		
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

			if (UberTranslator.OpenRouterApiKey != string.Empty)
			{
				var tempRes = Result.Select(x => x.Value).ToArray();
				UnrealLocres.ProcessTranslator(ref tempRes);
				Result = tempRes.ToConcurrent();
			}

			UnrealLocres.WriteToCsv(Result, csvPath);
            CLI.Console.WriteLine($"\n[Green]Completed! File saved to: {csvPath}");

			if (UnrealLocres.WriteLocres && locresPath != null) 
				LocresWriter.LocresCompactWriter.WriteToFile(locresPath,Result.FromConcurrent().ToList());   
			if (ProgramAutoExit) Environment.Exit(0);
		}

	}
}

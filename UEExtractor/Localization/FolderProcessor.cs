using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Solicen.Localization.UE4
{
	class FolderProcessor
	{
		static List<Argument> arguments = new List<Argument>
		{
			new Argument("--picky", "picky mode, displays more annoying information", () => UnrealLocres.PickyMode = true),
			new Argument("--url", "include path to file, ex: [url][key],<string>", () => UnrealLocres.IncludeUrlInKeyValue = true),
			new Argument("--headmark", "include header and footer of the csv.", () => UnrealLocres.ForceMark = true),
			new Argument("--hash", "inculde hash of string for locres ex: [key][hash],<string>.", () => UnrealLocres.IncludeHashInKeyValue = true),
			new Argument("--locres", "write .locres file.", () => UnrealLocres.WriteLocres = true),
			new Argument("--skip-uexp", "skip files with `.uexp` during the process", () => UnrealLocres.SkipUexpFile = true),
			new Argument("--skip-uasset", "skip files with `.uasset` during the process", () => UnrealLocres.SkipUassetFile = true),
			new Argument("--underscore", "do not skip lines with underscores.", () => UnrealUepx.SkipUnderscore = false),
			new Argument("--upper-upper", "do not skip lines with upperupper.", () => UnrealUepx.SkipUpperUpper = false),
			new Argument("--no-parallel", "disable parallel processing, slower, may output additional data.", () => UnrealUasset.parallelProcessing = false),
			new Argument("--invalid", "include invalid data in the output.", () => UnrealUepx.IncludeInvalidData = false),
			new Argument("--qmarks", "forcibly adds quotation marks between text strings.", () => UnrealLocres.ForceQmarksOutput = true),
			new Argument("--table-format", "replace standard separator , symbol to | ", () => UnrealLocres.TableSeparator = true),
			new Argument("--autoexit", "Exit automatically after execution", () => _autoExit = true),
			new Argument("--help", "Show help information", () => ShowHelp(arguments))
		};

		class Argument
		{
			public string Name { get; }
			public string Description { get; }
			public Action Action { get; }

			public Argument(string name, string description, Action action)
			{
				Name = name;
				Description = description;
				Action = action;
			}
		}

		static void ShowHelp(System.Collections.Generic.List<Argument> arguments)
		{
			Console.WriteLine("Available arguments:");
			foreach (var argument in arguments)
			{
				Console.WriteLine($"{argument.Name}: {argument.Description}");
			}
		}

		private static bool _autoExit = false;
		static void ProcessArgs(string[] args)
		{
			foreach (var arg in args)
			{
				if (!arg.StartsWith("--")) continue;
				var argument = arguments.Find(a => a.Name.Equals(arg, StringComparison.OrdinalIgnoreCase));
				if (argument != null)
				{
					argument.Action();
				}
				else if (!arg.Contains("="))
				{
					Console.WriteLine($"Unknown argument: {arg}");
					ShowHelp(arguments);
					return;
				}
			}
		}

		static void ProcessVersion(string[] args)
		{
			var arg = args.FirstOrDefault(x => x.StartsWith("--version") || x.StartsWith("-v"));
			if (string.IsNullOrWhiteSpace(arg)) return;
			if (!arg.Contains("=")) return;

			var UEVersion = arg.Split('=')[1];
			UEVersion = UEVersion.Replace(".", "_");
			UnrealLocres.UEVersion = UEVersion;
		}

        static void ProcessAES(string[] args)
        {
            var arg = args.FirstOrDefault(x => x.StartsWith("--aes") || x.StartsWith("-a"));
            if (string.IsNullOrWhiteSpace(arg)) return;
            if (!arg.Contains("=")) return;

            var AESKey = arg.Split('=')[1];
			if (AESKey.Length < 66)
			{
				Console.WriteLine("Invalid AES key, must contain 32 HEX characters. But it doesn't contain it.\nThe process will continue without the AES key.");
				return;
			}
            UnrealLocres.AES = AESKey;
        }

        public static void ProcessProgram(string[] args)
		{
			if (args.Length > 0)
			{
                string locres = null;
                if (args[0].Contains(".csv")) // Обработка для получения .locres файла
				{					
					string LocresCSV = args[0];
					if (args.Length > 1)			
                        locres = args.FirstOrDefault(x => x.Contains(".locres"));
                    
					if (!string.IsNullOrEmpty(locres)) // Если .locres
					{
                        // Учитывать дополнительные аргументы при сборке
                        ProcessArgs(args); 

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
					string folderPath = args[0]; string fileName = "";
					locres = args.FirstOrDefault(x => x.Contains(".locres"));

					if (args.Length > 1)
					{
						fileName = !args[1].StartsWith("-") && args[1] != locres ? args[1] : "";
					}

					ProcessArgs(args);
					ProcessVersion(args);
					ProcessAES(args);

					if (Directory.Exists(folderPath) && !string.IsNullOrEmpty(args[0]))
					{
						FolderProcessor.ProcessFolder(folderPath, fileName, locres);
					}
				}
			}
		}
		public static void ProcessFolder(string folderPath, string fileName = "", string locresPath = "")
		{
			var exePath = new FileInfo(typeof(FolderProcessor).Assembly.Location).Directory;
			var csvPath = string.IsNullOrWhiteSpace(fileName)
				? $"{Path.GetFileName(folderPath)}_locres.csv"
				: $"{fileName.Replace(".csv", "")}.csv";

			csvPath =  Path.Combine(exePath + "\\", csvPath);

			// CSV for skipped_lines during parsing lines to locresCSV.
			UnrealLocres.SkippedCSV = new CSVWriter(csvPath.Replace(".csv", "") + "_skipped_lines.csv");

			// Parsing and his result
			var Result = UnrealLocres.ProcessDirectory(folderPath);

			UnrealLocres.WriteToCsv(Result, csvPath);
			Console.WriteLine($"\nCompleted! File saved to: {csvPath}");

			if (UnrealLocres.WriteLocres && locresPath != null)
				UnrealLocres.WriteToLocres(Result, locresPath);
            
			if (_autoExit) Environment.Exit(0);
		}

	}
}

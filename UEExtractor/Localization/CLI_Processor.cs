﻿using Solicen.CLI;
using Solicen.GitHub.Updater;
using Solicen.Translator;
using System.Collections.Concurrent;

namespace Solicen.Localization.UE4
{
	class CLI_Processor
	{
        private static bool ProgramAutoExit = false;
        private static bool TranslateOnly = false;
        private static readonly List<Argument> arguments;

		static CLI_Processor()
		{
			GitProvider.RepoName = "UEExtractor";
			GitProvider.UserName = "SolicenTEAM";

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
				new Argument("--verbose", "-vb", "Enable verbose output: show per-file processing details and diagnostics.", () => UnrealLocres.VerboseOutput = true),
				new Argument("--path", "-path", "Restrict processing to assets under a specific virtual path (e.g. --path=HT/Content/Localization).", (p) => UnrealLocres.FilterPath = p),
				new Argument("--help", "-h", "Show help information", () => Argumentor.ShowHelp(arguments)),
                new Argument("--update", null, "Check for a new version on GitHub and update if available.", async () => await GitProvider.CheckForUpdatesAsync()),
                new Argument("--table:only:key", "-t:o:k", "If key/name matches then include only this value to output (e.g., --table:only:key=ENG).", (key) => UnrealLocres.SearchKeyName = key),

                new Argument("--lang:from", "-l:f", "Set the source language for translation (e.g., --lang:from=en).", (lang) => UberTranslator.LanguageFrom = lang),
                new Argument("--lang:to", "-l:t", "Set the target language for translation (e.g., --lang:to=ru).", (lang) => UberTranslator.LanguageTo = lang),
                new Argument("--api:model", "-a:model", "Set model for OpenRouter (e.g, -a:model=tngtech/deepseek-r1t2-chimera:free)", (model) => UberTranslator.OpenRouterModel = model),
                new Argument("--api:key", "-a:key", "Set API key for OpenRouter (or any server that requires authentication).", (key) => UberTranslator.OpenRouterApiKey = key),
                new Argument("--api:url", "-a:url", "Set a custom OpenAI-compatible base URL (e.g. http://localhost:11434/v1/ for Ollama). Overrides OpenRouter.", (url) => UberTranslator.ApiBaseUrl = url),
                new Argument("--batch-size", "-bs", $"Number of segments per translation request (default: {UberTranslator.BatchSize}).", (v) => { if (int.TryParse(v, out int n) && n > 0) UberTranslator.BatchSize = n; }),
                new Argument("--parallel", "-par", "Number of concurrent translation requests (default: 1). Increase for faster translation.", (v) => { if (int.TryParse(v, out int n) && n > 0) UberTranslator.MaxParallel = n; }),
                new Argument("--translate-only", "-t:o", "Skip extraction and translate existing CSV file(s) from previous run.", () => TranslateOnly = true),
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
                    if (UberTranslator.IsConfigured)
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


		public static void ProcessFolder(string folderPath, string? fileName = "", string? locresPath = "")
		{
			var exePath = new FileInfo(typeof(CLI_Processor).Assembly.Location).Directory;

			// If fileName is a directory path, write per-locres CSVs into it
			bool outputIsDirectory = !string.IsNullOrWhiteSpace(fileName) &&
				(fileName.EndsWith("\\") || fileName.EndsWith("/") || Directory.Exists(fileName));

			if (outputIsDirectory)
			{
				ProcessFolderToDirectory(folderPath, fileName!.TrimEnd('\\', '/'), locresPath);
				return;
			}

			var csvPath = string.IsNullOrWhiteSpace(fileName)
				? $"{Path.GetFileName(folderPath)}_locres.csv"
				: $"{Path.ChangeExtension(fileName, ".csv")}";

			csvPath =  Path.Combine(exePath + "\\", csvPath);

			UnrealLocres.SkippedCSV = new CSV.Writer(Path.ChangeExtension(csvPath, "_skipped_lines.csv"));

			ConcurrentDictionary<string, LocresResult> Result;
			if (TranslateOnly)
			{
				if (!File.Exists(csvPath))
				{
                    CLI.Console.WriteLine($"[Red][Error] --translate-only requires an existing CSV: {csvPath}");
					return;
				}
                CLI.Console.WriteLine($"[Yellow]--translate-only: loading {csvPath}...");
				Result = UnrealLocres.LoadFromCSV(csvPath).ToConcurrent();
			}
			else
			{
				Result = UnrealLocres.ProcessDirectory(folderPath);
				MergeOldCsv(csvPath, Result);
			}

			if (UberTranslator.IsConfigured)
			{
				var journalPath = csvPath + ".journal";
				var tempRes = Result.Select(x => x.Value).ToArray();
				UnrealLocres.ProcessTranslator(ref tempRes, journalPath: journalPath);
				Result = tempRes.ToConcurrent();
				if (File.Exists(journalPath)) File.Delete(journalPath);
			}

			UnrealLocres.WriteToCsv(Result, csvPath);
            CLI.Console.WriteLine($"\n[Green]Completed! File saved to: {csvPath}");

			if (UnrealLocres.WriteLocres && locresPath != null)
				LocresWriter.LocresCompactWriter.WriteToFile(locresPath,Result.FromConcurrent().ToList());
			if (ProgramAutoExit) Environment.Exit(0);
		}

		// Writes one CSV per locres file into outputDir, merging with any existing CSV.
		private static void ProcessFolderToDirectory(string folderPath, string outputDir, string? locresPath)
		{
			Directory.CreateDirectory(outputDir);

			List<(string BaseName, ConcurrentDictionary<string, LocresResult> Result)> groups;

			if (TranslateOnly)
			{
				var existing = Directory.GetFiles(outputDir, "*.csv")
					.Where(f => !f.EndsWith("_skipped_lines.csv"))
					.ToList();
				if (existing.Count == 0)
				{
                    CLI.Console.WriteLine($"[Red][Error] --translate-only: no CSV files found in {outputDir}");
					return;
				}
                CLI.Console.WriteLine($"[Yellow]--translate-only: found {existing.Count} CSV(s) in {outputDir}");
				groups = existing
					.Select(f => (Path.GetFileNameWithoutExtension(f), UnrealLocres.LoadFromCSV(f).ToConcurrent()))
					.ToList();
			}
			else
			{
				groups = UnrealLocres.ProcessLocresGrouped(folderPath);
				if (groups.Count == 0)
				{
                    CLI.Console.WriteLine("[Yellow]No locres data found.");
					return;
				}
				// Merge with existing CSVs (O(1) lookup)
				foreach (var (baseName, result) in groups)
					MergeOldCsv(Path.Combine(outputDir, $"{baseName}.csv"), result);
			}

			foreach (var (baseName, finalResult) in groups)
			{
				var csvPath = Path.Combine(outputDir, $"{baseName}.csv");
				UnrealLocres.SkippedCSV = new CSV.Writer(Path.ChangeExtension(csvPath, "_skipped_lines.csv"));

				if (UberTranslator.IsConfigured)
				{
					var journalPath = csvPath + ".journal";
					var tempRes = finalResult.Select(x => x.Value).ToArray();
					UnrealLocres.ProcessTranslator(ref tempRes, journalPath: journalPath);
					var translated = tempRes.ToConcurrent();
					if (File.Exists(journalPath)) File.Delete(journalPath);
					UnrealLocres.WriteToCsv(translated, csvPath);
                    CLI.Console.WriteLine($"\n[Green]Saved: {csvPath}  ({translated.Count} entries)");
				}
				else
				{
					UnrealLocres.WriteToCsv(finalResult, csvPath);
                    CLI.Console.WriteLine($"\n[Green]Saved: {csvPath}  ({finalResult.Count} entries)");
				}
			}

			if (ProgramAutoExit) Environment.Exit(0);
		}

		// O(1) merge using ConcurrentDictionary's built-in lookup
		private static void MergeOldCsv(string csvPath, ConcurrentDictionary<string, LocresResult> result)
		{
			if (!File.Exists(csvPath)) return;
            CLI.Console.WriteLine("\n[Yellow]Found previous CSV file, merging...");
			var oldCSV = UnrealLocres.LoadFromCSV(csvPath);
			int merged = 0, added = 0;
			foreach (var line in oldCSV)
			{
				if (result.TryGetValue(line.Key, out var existing))
				{
					if (existing.Source != line.Source.Escape() && line.Translation == string.Empty)
						existing.Translation = line.Source;
					else if (line.Translation != string.Empty)
						existing.Translation = line.Translation;
					merged++;
				}
				else
				{
					result.TryAdd(line.Key, new LocresResult(line.Key, line.Source, line.Translation, line.Namespace));
					added++;
				}
			}
            CLI.Console.WriteLine($" - Extracted: {result.Count - added}  Merged: {merged}  New from old: {added}  Total: {result.Count}");
		}

	}
}

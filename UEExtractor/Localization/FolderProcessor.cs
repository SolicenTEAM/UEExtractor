using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Solicen.Localization.UE4
{
    class FolderProcessor
    {
        static List<Argument> arguments = new List<Argument>
        {
            new Argument("--skipuexp", "skip files with `.uexp` during the process", () => UnrealLocres.SkipUexpFile = true),
            new Argument("--skipasset", "skip files with `.uassets` during the process", () => UnrealLocres.SkipUassetFile = true),
            new Argument("--underscore", "do not skip lines with underscores.", () => UnrealUepx.SkipUnderscore = false),
            new Argument("--upperupper", "do not skip lines with upperupper.", () => UnrealUepx.SkipUpperUpper = false),
            new Argument("--noparallel", "disable parallel processing, slower, may output additional data.", () => UnrealUasset.parallelProcessing = false),
            new Argument("--invalid", "include invalid data in the output.", () => UnrealUepx.IncludeInvalidData = false),
            new Argument("--qmarks", "forcibly adds quotation marks between text strings.", () => UnrealLocres.ForceQmarksOutput = true),
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
                var argument = arguments.Find(a => a.Name.Equals(arg, StringComparison.OrdinalIgnoreCase));
                if (argument != null)
                {
                    argument.Action();
                }
                else
                {
                    Console.WriteLine($"Unknown argument: {arg}");
                    ShowHelp(arguments);
                    return;
                }
            }
        }

        public static void ProcessProgram(string[] args)
        {
            if (args.Length > 0)
            {
                string folderPath = args[0];
                string fileName = "";
                if (args.Length > 1) {
                    fileName = !args[1].Contains("--") ? args[1] : "";
                }
                ProcessArgs(args);
                if (Directory.Exists(folderPath) && !string.IsNullOrEmpty(args[0]))
                {
                    FolderProcessor.ProcessFolder(folderPath, fileName);
                }
            }
        }
        public static void ProcessFolder(string folderPath, string fileName = "")
        {
            var exePath = new FileInfo(Assembly.GetExecutingAssembly().Location).Directory;
            var csvPath = string.IsNullOrWhiteSpace(fileName)
                ? $"{Path.GetFileName(folderPath)}_locres.csv"
                : $"{fileName.Replace(".csv", "")}.csv";

            csvPath =  Path.Combine(exePath + "\\", csvPath);
            var Result = UnrealLocres.ProcessDirectory(folderPath);

            UnrealLocres.WriteToCsv(Result, csvPath);
            Console.WriteLine($"\nCompleted! File saved to: {csvPath}\n");

            if (_autoExit) Environment.Exit(0);
        }

    }
}

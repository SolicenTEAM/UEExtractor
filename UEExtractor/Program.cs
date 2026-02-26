using Solicen.Localization.UE4;
using Solicen.Utils;
using System.Diagnostics;
using System.Reflection;
namespace UEExtractor
{
    class Program
    {
        const string title = $"UEExtractor VER : https://github.com/SolicenTEAM/UEExtractor";
        static void Main(string[] args)
        {
            var Version = $"{Assembly.GetExecutingAssembly().GetName().Version}";
            Console.Title = "UEExtractor • Solicen";
            Solicen.CLI.Console.WriteLine($"{title.Replace("VER", Version)}", ConsoleColor.Cyan);
            Solicen.CLI.Console.WriteLine("Author: Denis Solicen : https://github.com/DenisSolicen", ConsoleColor.Cyan);
            Solicen.CLI.Console.WriteLine("\nThis cool tool will collect and extract the .locres file from the resources of your Unreal Engine game folder.");
            Solicen.CLI.Console.WriteLine("Usage: Drag and drop whole the game folder that contain unreal archives: '.pak' or '.ucas' files.");
            Thread.Sleep(100);
            Stopwatch stopwatch = new Stopwatch(); stopwatch.Start();
            TimeSpan timeTaken = new TimeSpan();

            MemoryManager.Start();
            var backgroundThread = new Thread(() =>
            {
                CLI_Processor.ProcessProgram(args);
                stopwatch.Stop(); timeTaken = stopwatch.Elapsed;
            });
            backgroundThread.Start(); backgroundThread.Join();
            MemoryManager.Stop();

            Solicen.CLI.Console.WriteLine($"Operation completed in: {timeTaken.TotalSeconds} seconds");
            Solicen.CLI.Console.WriteLine("\nIf my program was useful to you, please put a star on its GitHub page, thank you!", ConsoleColor.Yellow);
            Solicen.CLI.Console.WriteLine("Toss a coin: https://boosty.to/denissolicen/donate", ConsoleColor.Yellow);
            Console.ReadLine();
        }
    }
}

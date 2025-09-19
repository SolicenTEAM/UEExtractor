using System.Diagnostics;using System.Reflection;
namespace UEExtractor
{
    class Program
    {
        const string title = $"UEExtractor VER : https://github.com/SolicenTEAM/UEExtractor";
        static void Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            var Version = $"{FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion}";
            Console.Title = "UEExtractor • Solicen";
            Console.WriteLine($"{title.Replace("VER", Version)}");
            Console.WriteLine("Author: Denis Solicen : https://github.com/DenisSolicen");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("\nThis cool tool will collect and extract the .locres file from the resources of your Unreal Engine game folder.");
            Console.WriteLine("Usage: Drag and drop whole the game folder that contain unreal archives: '.pak' or '.utoc' files.");
            Thread.Sleep(100);
            Stopwatch stopwatch = new Stopwatch(); stopwatch.Start();
            TimeSpan timeTaken = new TimeSpan();
            var backgroundThread = new Thread(() =>
            {
                Solicen.Localization.UE4.FolderProcessor.ProcessProgram(args);
                stopwatch.Stop(); timeTaken = stopwatch.Elapsed;
            });
            backgroundThread.Start(); backgroundThread.Join();
            Console.WriteLine($"Operation completed in: {timeTaken.TotalSeconds} seconds");
            Console.WriteLine(
                "\nIf my program was useful to you, please put a star on its GitHub page, thank you!");

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Toss a coin: https://boosty.to/denissolicen/donate");
            Console.ReadLine();
        }
    }
}

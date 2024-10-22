using System;
using System.Diagnostics;
using System.Threading;

namespace UEExtractor
{
    class Program
    {
        private static readonly string title = "UEExtractor by Solicen : https://github.com/SolicenTEAM/UEExtractor";
        static void Main(string[] args)
        {
            Console.WriteLine($"{title}\n");
            Console.WriteLine("This cool tool will collect and extract the .locres file from the resources of your Unreal Engine game folder.");
            Console.WriteLine("Usage: Drag and drop the game folder obtained during extraction '.pak' or '.ucas|.utoc' file."); 
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
            Console.ReadLine();
        }
    }
}

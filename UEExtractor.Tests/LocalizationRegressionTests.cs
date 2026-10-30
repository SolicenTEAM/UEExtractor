using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using Solicen.Localization.UE4;
using Xunit;

namespace UEExtractor.Tests;

public sealed class LocalizationRegressionTests
{
    private static readonly object StaticStateLock = new();

    [Fact]
    public void CsvRoundTripPreservesCommasAndQuotesAcrossFormats()
    {
        lock (StaticStateLock)
        {
            var directory = Path.Combine(Path.GetTempPath(), $"UEExtractorTests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            var previousTableSeparator = UnrealLocres.TableSeparator;
            var previousForceQmarks = UnrealLocres.ForceQmarksOutput;
            var previousForceMark = UnrealLocres.ForceMark;

            try
            {
                var source = "A \"quoted\" value,with comma";
                var translation = "5,000,000$ and \"\"";
                var results = new ConcurrentDictionary<string, LocresResult>();
                results["TheWall&Trenx::EAF500CA472991FC43E27991E197B2F9"] =
                    new LocresResult("TheWall&Trenx::EAF500CA472991FC43E27991E197B2F9", source, translation, "TheWall&Trenx");
                results["NullTranslation"] = new LocresResult("NullTranslation", "source");
                results["NullSource"] = new LocresResult("NullSource", null!);

                UnrealLocres.TableSeparator = false;
                UnrealLocres.ForceQmarksOutput = true;
                UnrealLocres.ForceMark = true;
                var commaPath = Path.Combine(directory, "quoted.csv");
                UnrealLocres.WriteToCsv(results, commaPath);

                var commaRoundTrip = UnrealLocres.LoadFromCSV(commaPath);
                var commaResult = Array.Find(commaRoundTrip, result => result.Key == "TheWall&Trenx::EAF500CA472991FC43E27991E197B2F9");
                Assert.NotNull(commaResult);
                Assert.Equal(source, commaResult!.Source);
                Assert.Equal(translation, commaResult!.Translation);
                Assert.Contains(commaRoundTrip, result => result.Key == "NullTranslation" && result.Translation == string.Empty);
                Assert.DoesNotContain(commaRoundTrip, result => result.Key == "NullSource");

                UnrealLocres.TableSeparator = true;
                UnrealLocres.ForceQmarksOutput = false;
                UnrealLocres.ForceMark = false;
                var pipePath = Path.Combine(directory, "pipe.csv");
                UnrealLocres.WriteToCsv(results, pipePath);

                UnrealLocres.TableSeparator = false;
                var pipeRoundTrip = UnrealLocres.LoadFromCSV(pipePath);
                var pipeResult = Array.Find(pipeRoundTrip, result => result.Key == "TheWall&Trenx::EAF500CA472991FC43E27991E197B2F9");
                Assert.NotNull(pipeResult);
                Assert.Equal(source, pipeResult!.Source);
                Assert.Equal(translation, pipeResult!.Translation);
            }
            finally
            {
                UnrealLocres.TableSeparator = previousTableSeparator;
                UnrealLocres.ForceQmarksOutput = previousForceQmarks;
                UnrealLocres.ForceMark = previousForceMark;
                Directory.Delete(directory, true);
            }
        }
    }

    [Fact]
    public void ExportTypeScannerFindsTextPropertyBeyondLegacyProbeAndRestoresPosition()
    {
        var signature = Encoding.ASCII.GetBytes("TextProperty");
        var bytes = new byte[70_000];
        signature.CopyTo(bytes, 65_548);
        using var stream = new MemoryStream(bytes);
        stream.Position = 17;

        var exportType = UnrealLocres.GetExportType(stream);

        Assert.Equal(UnrealLocres.ExportType.TextProperty, exportType);
        Assert.Equal(17, stream.Position);
    }
}

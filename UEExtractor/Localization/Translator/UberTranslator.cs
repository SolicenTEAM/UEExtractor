namespace Solicen.Translator
{
    internal class UberTranslator
    {
        private OpenRouterApiClient OpenRouterClient;

        public static string LanguageTo = "ru", LanguageFrom = "auto";
        public static string OpenRouterApiKey = string.Empty;
        public static string OpenRouterModel = "tngtech/deepseek-r1t2-chimera:free";
        // Custom base URL for any OpenAI-compatible endpoint (Ollama, LM Studio, vLLM, etc.)
        // Example: http://localhost:11434/v1/  or  http://localhost:1234/v1/
        public static string ApiBaseUrl = string.Empty;
        public static int BatchSize = 150;
        public static int MaxParallel = 1;

        public static bool IsConfigured =>
            !string.IsNullOrEmpty(OpenRouterApiKey) || !string.IsNullOrEmpty(ApiBaseUrl);

        public UberTranslator()
        {
            bool hasKey = !string.IsNullOrEmpty(OpenRouterApiKey);
            bool hasUrl = !string.IsNullOrEmpty(ApiBaseUrl);

            if (hasKey || hasUrl)
            {
                if (hasKey && File.Exists(OpenRouterApiKey))
                    OpenRouterApiKey = File.ReadAllLines(OpenRouterApiKey)[0];

                var url = hasUrl ? ApiBaseUrl : "https://openrouter.ai/api/v1/";
                OpenRouterClient = new OpenRouterApiClient(OpenRouterApiKey, url);
            }
        }

        // onBatchComplete receives only the pairs translated in that batch (for efficient journaling)
        public void TranslateLines(ref Dictionary<string, string> values, IProgress<Tuple<int, int>> progress = null,
            bool showWaringMsg = false, int delayBetweenMsg = 150,
            Action<List<(string Source, string Translation)>>? onBatchComplete = null)
        {
            if (OpenRouterClient == null)
            {
                CLI.Console.WriteLine("[Red][Error] No API key or URL configured. Use --api:key=<key> for OpenRouter or --api:url=<url> for a local model.");
                values = new Dictionary<string, string>(values);
                return;
            }

            var translatedBatch = TranslateBatchWithOpenRouterAsync(values, progress, onBatchComplete).GetAwaiter().GetResult();
            values = translatedBatch;
        }

        private async Task<Dictionary<string, string>> TranslateBatchWithOpenRouterAsync(
            Dictionary<string, string> values,
            IProgress<Tuple<int, int>>? progress,
            Action<List<(string Source, string Translation)>>? onBatchComplete = null)
        {
            const string separator = "|||";
            int batchSize = BatchSize;
            var result = new ConcurrentDictionary<string, string>(values);
            var toTranslate = values.Where(kvp => string.IsNullOrWhiteSpace(kvp.Value)).ToList();
            if (toTranslate.Count == 0) return new Dictionary<string, string>(result);

            int totalTranslated = 0;
            var semaphore = new SemaphoreSlim(MaxParallel);
            var endpoint = string.IsNullOrEmpty(ApiBaseUrl) ? "OpenRouter" : ApiBaseUrl;

            // Split into chunks and process with bounded parallelism
            var chunks = Enumerable.Range(0, (toTranslate.Count + batchSize - 1) / batchSize)
                .Select(i => toTranslate.Skip(i * batchSize).Take(batchSize).ToList())
                .ToList();

            var tasks = chunks.Select(async (chunk, chunkIndex) =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var combinedText = string.Join(separator, chunk.Select(kvp => kvp.Key.Replace(separator, "")));
                    var systemPrompt =
                        $"You are an expert language translator. Translate the following text from '{LanguageFrom}' to '{LanguageTo}'. " +
                        $"The texts are separated by '{separator}'. Maintain the exact same separation in your output. Keep the same punctuation. " +
                        $"Provide only the translated text in a literary style, without any additional explanations or context. ";

                    var request = new OpenRouterRequest
                    {
                        Model = OpenRouterModel,
                        Messages = new List<OpenRouterMessage>
                        {
                            new OpenRouterMessage { Role = "system", Content = systemPrompt },
                            new OpenRouterMessage { Role = "user", Content = $"Please translate this:\n\n{combinedText}" }
                        }
                    };

                    CLI.Console.StartProgress($"Batch {chunkIndex + 1}/{chunks.Count} ({chunk.Count} segments) → {endpoint} [{OpenRouterModel}]...");
                    var response = await OpenRouterClient.ChatAsync(request);
                    CLI.Console.StopProgress();

                    if (response != null && response.Choices.Any())
                    {
                        var translated = response.Choices.First().Message?.Content ?? string.Empty;
                        var segments = translated.Split(new[] { separator }, StringSplitOptions.None);

                        if (segments.Length == chunk.Count)
                        {
                            var batchPairs = new List<(string Source, string Translation)>(chunk.Count);
                            for (int j = 0; j < chunk.Count; j++)
                            {
                                var src = chunk[j].Key;
                                var tgt = segments[j].Trim();
                                result[src] = tgt;
                                batchPairs.Add((src, tgt));
                                int done = Interlocked.Increment(ref totalTranslated);
                                progress?.Report(new Tuple<int, int>(done, toTranslate.Count));
                                CLI.Console.WriteLine($"[DarkGray][{done}/{toTranslate.Count}] [White]'{src.Escape()}' => '{tgt.Escape()}'");
                            }
                            onBatchComplete?.Invoke(batchPairs);
                        }
                        else
                        {
                            Console.WriteLine($"[Red][Error] [White]Batch {chunkIndex + 1}: expected {chunk.Count} segments, got {segments.Length}. Skipped.");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[Red][Error] [White]Batch {chunkIndex + 1} failed: {OpenRouterClient.LastError}");
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
            return new Dictionary<string, string>(result);
        }

    }
}

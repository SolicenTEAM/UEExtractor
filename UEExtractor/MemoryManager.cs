using System.Diagnostics;
using System.Runtime;

namespace Solicen.Utils
{
    /// <summary>
    /// Управляет использованием памяти, периодически запуская сборку мусора при превышении порога.
    /// </summary>
    public static class MemoryManager
    {
        private static Timer? _timer;
        private static readonly long _memoryThresholdBytes;
        private static bool _isCollecting = false;
        private static readonly object _lock = new object();

        static MemoryManager()
        {
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;

            // Устанавливаем порог в 15% от общего объема физической памяти.
            // Это значение можно настроить.
            var totalMemory = GetTotalPhysicalMemory();
            if (totalMemory > 0)
            {
                _memoryThresholdBytes = (long)(totalMemory * 0.15); // 15%
            }
            else
            {
                // Запасной вариант, если не удалось получить объем памяти
                _memoryThresholdBytes = 8L * 1024 * 1024 * 1024; // 8 GB
            }
        }

        /// <summary>
        /// Запускает фоновый мониторинг памяти.
        /// </summary>
        public static void Start()
        {
            if (_timer == null)
            {
                // Проверяем память каждые 5 секунд.
                _timer = new Timer(CheckMemory, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
                // CLI.Console.WriteLine($"[MemoryManager] Started. Threshold set to ~{_memoryThresholdBytes / (1024 * 1024 * 1024)} GB.");
            }
        }

        /// <summary>
        /// Останавливает фоновый мониторинг памяти.
        /// </summary>
        public static void Stop()
        {
            _timer?.Dispose();
            _timer = null;
            // CLI.Console.WriteLine("[MemoryManager] Stopped.");
        }

        private static void CheckMemory(object state)
        {
            lock (_lock)
            {
                if (_isCollecting)
                {
                    return; // Пропускаем проверку, если сборка мусора уже идет.
                }

                var currentUsage = Process.GetCurrentProcess().WorkingSet64;
                if (currentUsage > _memoryThresholdBytes)
                {
                    _isCollecting = true;
                    // CLI.Console.WriteLine($"[MemoryManager] Memory usage ({currentUsage / (1024 * 1024)} MB) exceeds threshold. Forcing aggressive GC...");       
                    // Запускаем сборку мусора в отдельном потоке, чтобы не блокировать таймер.
                    Task.Run(() =>
                    {
                        try
                        {
                            // Устанавливаем агрессивный режим для LOH (кучи больших объектов)
                            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;

                            // Запускаем принудительную, блокирующую, уплотняющую сборку всех поколений.
                            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);

                            // Даем команду ОС высвободить неиспользуемую память.
                            // В .NET Core это делается автоматически после уплотняющей сборки, но для надежности можно оставить.
                            // CLI.Console.WriteLine($"[MemoryManager] GC finished. New memory usage: {Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024)} MB.");
                        }
                        finally
                        {
                            _isCollecting = false;
                        }
                    });
                }
            }
        }

        private static long GetTotalPhysicalMemory()
        {
            try
            {
                // Этот способ работает на Windows, Linux и macOS с .NET 6+
                var gcMemoryInfo = GC.GetGCMemoryInfo();
                return gcMemoryInfo.TotalAvailableMemoryBytes;
            }
            catch
            {
                return 0; // Возвращаем 0, если не удалось получить информацию.
            }
        }
    }
}

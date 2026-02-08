using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Solicen.CLI
{
    internal static class Console
    {
        class Color
        {
            public static readonly ConsoleColor PrimaryColor = ConsoleColor.Cyan;
            public static readonly ConsoleColor SuccessColor = ConsoleColor.Green;
            public static readonly ConsoleColor WarningColor = ConsoleColor.Yellow;
            public static readonly ConsoleColor ErrorColor = ConsoleColor.Red;
            public static readonly ConsoleColor InfoColor = ConsoleColor.White;
            public static readonly ConsoleColor GrayColor = ConsoleColor.DarkGray;
        }

        private static CancellationTokenSource _progressCts;
        private static Task _progressTask;

        public static async Task<int> ShowMenuAsync(string title, params string[] options)
        {
            try
            {
                int maxLength = options.Max(opt => opt.Length) + 6;
                int selectedIndex = 0;
                ConsoleKey key;

                System.Console.CursorVisible = false;
                int menuStartLine = System.Console.CursorTop;

                do
                {
                    // Возвращаемся к началу меню
                    System.Console.SetCursorPosition(0, menuStartLine);


                    // Очищаем только нужную область
                    for (int i = 0; i < options.Length + 1; i++)
                    {
                        System.Console.Write(new string(' ', System.Console.WindowWidth));
                        System.Console.SetCursorPosition(0, menuStartLine + i + 1);
                    }

                    System.Console.SetCursorPosition(0, menuStartLine);

                    // Выводим заголовок без лишних отступов
                    System.Console.ForegroundColor = Color.PrimaryColor;
                    System.Console.Write(title);
                    System.Console.ResetColor();
                    System.Console.Write(new string(' ', System.Console.WindowWidth - title.Length));
                    System.Console.SetCursorPosition(0, menuStartLine + 1);

                    // Выводим options
                    for (int i = 0; i < options.Length; i++)
                    {
                        System.Console.SetCursorPosition(0, menuStartLine + i + 1);
                        string paddedOption = options[i].PadRight(maxLength - 3);

                        if (i == selectedIndex)
                        {
                            System.Console.ForegroundColor = ConsoleColor.Black;
                            System.Console.BackgroundColor = ConsoleColor.Black;
                            System.Console.Write(" ");
                            System.Console.BackgroundColor = ConsoleColor.White;
                            System.Console.Write($"   {paddedOption}");
                            System.Console.ResetColor();
                        }
                        else
                        {
                            System.Console.ForegroundColor = Color.InfoColor;
                            System.Console.Write($"   {paddedOption}");
                            System.Console.ResetColor();
                        }
                    }

                    key = System.Console.ReadKey(true).Key;

                    if (key == ConsoleKey.UpArrow)
                    {
                        selectedIndex = (selectedIndex - 1 + options.Length) % options.Length;
                    }
                    else if (key == ConsoleKey.DownArrow)
                    {
                        selectedIndex = (selectedIndex + 1) % options.Length;
                    }

                } while (key != ConsoleKey.Enter);

                System.Console.CursorVisible = true;

                // Очищаем область меню после выбора
                System.Console.SetCursorPosition(0, menuStartLine);
                for (int i = 0; i < options.Length + 1; i++)
                {
                    System.Console.Write(new string(' ', System.Console.WindowWidth));
                    System.Console.SetCursorPosition(0, menuStartLine + i + 1);
                }

                System.Console.SetCursorPosition(0, menuStartLine);

                return selectedIndex;
            }
            finally
            {
                // Блокировка больше не используется
            }
        }

        public static void WriteLine(string message = "", ConsoleColor color = ConsoleColor.White, bool curLine = false)
        {
            WriteLineAsync(message, color, 0, curLine).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Асинхронно выводит отформатированную строку с поддержкой цветовых тегов, например: "[Cyan]Processing: [White]file.txt".
        /// </summary>
        /// <param name="message">Строка с цветовыми тегами.</param>
        /// <param name="defaultColor">Цвет по умолчанию для текста без тегов.</param>
        /// <param name="delayMs">Задержка для эффекта "печатания". Установите 0 для мгновенного вывода.</param>
        public static async Task WriteLineAsync(string message, ConsoleColor color = ConsoleColor.White, int delayMs = 2, bool onLine = false)
        {
            // Останавливаем индикатор прогресса, чтобы он не мешал выводу
            StopProgress();

            var originalColor = System.Console.ForegroundColor;
            System.Console.ForegroundColor = color;
            System.Console.CursorVisible = false;
            IndexOnLine = 0;
            for (int i = 0; i < message.Length; i++)
            {
                char c = message[i];
                if (c == '[')
                {
                    // Ищем закрывающую скобку
                    int closingBracketIndex = message.IndexOf(']', i);
                    if (closingBracketIndex > i)
                    {
                        // Извлекаем имя цвета
                        string colorName = message.Substring(i + 1, closingBracketIndex - i - 1);
                        if (Enum.TryParse(colorName, true, out ConsoleColor newColor))
                        {
                            // Если это валидный цвет, меняем его и перескакиваем через тег
                            System.Console.ForegroundColor = newColor;
                            i = closingBracketIndex;
                            continue;
                        }
                    }

                }

                // Печатаем обычный символ
                System.Console.Write(c);
                if (delayMs > 0) await Task.Delay(delayMs); // Небольшая задержка для плавности
                IndexOnLine++;
            }
            System.Console.CursorVisible = true;
            if (!onLine) System.Console.WriteLine();
            System.Console.ForegroundColor = originalColor;
        }

        public static void Write(string message, ConsoleColor color = ConsoleColor.White)
        {
            WriteLine(message, color, curLine: true);
        }

        /// <summary>
        /// Рисует горизонтальный разделитель во всю ширину консоли.
        /// </summary>
        /// <param name="color">Цвет разделителя. По умолчанию DarkGray.</param>
        public static void Separator(int maxWidth = 0, bool onCurrentLine = false, ConsoleColor color = ConsoleColor.DarkGray)
        {
            // Останавливаем индикатор прогресса, чтобы он не мешал выводу
            StopProgress();
            var originalColor = System.Console.ForegroundColor;
            System.Console.ForegroundColor = color;
            // Рисуем линию, занимающую всю ширину окна консоли
            if (!onCurrentLine)
            {
                var newString = new string('─', maxWidth == 0 ? System.Console.WindowWidth - 1 : maxWidth);   
                System.Console.WriteLine(newString);
                IndexOnLine = 0;
            }
            else
            {
                if (maxWidth == 0) maxWidth = System.Console.WindowWidth-1;
                for(int i = 0; i < maxWidth - IndexOnLine; i++)
                {
                    System.Console.Write('─');
                }
                System.Console.Write('\n');
                IndexOnLine = 0;
            }

            System.Console.ForegroundColor = originalColor;
        }
        private static int IndexOnLine = 0;

        public static void WriteEnd(string endKey = "", int width = 1, ConsoleColor color = ConsoleColor.DarkGray)
        {
            // Останавливаем индикатор прогресса, чтобы он не мешал выводу
            StopProgress();
            Write(endKey);
        }

        /// <summary>
        /// Запускает анимированный индикатор выполнения в текущей строке консоли.
        /// </summary>
        /// <param name="message">Сообщение, отображаемое рядом с индикатором.</param>
        public static void StartProgress(string message)
        {
            // Останавливаем предыдущий индикатор, если он был запущен
            StopProgress();
            System.Console.WriteLine();
            _progressCts = new CancellationTokenSource();
            var token = _progressCts.Token;
            char[] spinner = { '|', '/', '-', '\\' };
            int spinnerIndex = 0;

            _progressTask = Task.Run(async () =>
            {
                // Если курсор в начале строки, скорее всего, мы на новой строке после WriteLine.
                // В этом случае мы хотим перерисовывать предыдущую строку.
                int originalTop = System.Console.CursorLeft == 0 && System.Console.CursorTop > 0 
                    ? System.Console.CursorTop - 1 
                    : System.Console.CursorTop;

                System.Console.CursorVisible = false;
                while (!token.IsCancellationRequested)
                {
                    // Очищаем строку перед отрисовкой, чтобы избежать артефактов от предыдущего текста
                    System.Console.Write(new string(' ', System.Console.WindowWidth > 0 ? System.Console.WindowWidth - 1 : 0));
                    System.Console.SetCursorPosition(0, originalTop);

                    // Рисуем спиннер
                    System.Console.ForegroundColor = Color.PrimaryColor;
                    System.Console.Write(spinner[spinnerIndex]);
                    System.Console.ResetColor();

                    // Пишем сообщение
                    System.Console.Write($" {message}");
                    spinnerIndex = (spinnerIndex + 1) % spinner.Length;
                    try
                    {
                        await Task.Delay(100, token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }

                // Очищаем строку после завершения
                System.Console.CursorVisible = true;
                System.Console.SetCursorPosition(0, originalTop);
                System.Console.Write(new string(' ', System.Console.WindowWidth > 0 ? System.Console.WindowWidth - 1 : 0));
                System.Console.SetCursorPosition(0, originalTop);

            }, token);
        }

        /// <summary>
        /// Останавливает и очищает индикатор выполнения.
        /// </summary>
        /// <param name="completionMessage">Сообщение, которое будет выведено вместо индикатора прогресса.</param>
        public static void StopProgress(string completionMessage = null)
        {
            if (_progressCts != null && !_progressCts.IsCancellationRequested)
            {
                _progressCts.Cancel();
                _progressTask?.Wait(); // Дожидаемся завершения задачи

                if (!string.IsNullOrEmpty(completionMessage))
                {
                    // Выводим завершающее сообщение, которое уже поддерживает форматирование
                    WriteLine(completionMessage);
                }

            }
            _progressCts = null;
            _progressTask = null;
        }
    }

}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Solicen.CLI
{
    /// <summary>
    /// Обрабатывает аргументы командной строки, настраивает конфигурацию и выполняет связанные действия.
    /// </summary>
    public static class Argumentor
    {
        #region Расширенное управление терминалом
        public static void RunTerminal(string command) => System.Diagnostics.Process.Start("CMD.exe", "/c " + command);
        public static string[] SplitArgs(string[] args)
        {
            // Новый, более надежный подход. Мы не объединяем аргументы в одну строку,
            // а обрабатываем их как есть, чтобы сохранить пути с пробелами.
            var processedArgs = new List<string>();
            for (int i = 0; i < args.Length; i++)
            {
                // Специальная обработка для --run=[...], чтобы захватить всю команду.
                var runMatch = Regex.Match(args[i], @"^--run\s*=?\s*\[(.*)", RegexOptions.IgnoreCase);
                if (runMatch.Success)
                {
                    var commandBuilder = new StringBuilder(runMatch.Groups[1].Value);
                    // Если команда не заканчивается в этом же аргументе, ищем ']' в следующих.
                    while (!args[i].EndsWith("]") && i + 1 < args.Length)
                    {
                        i++;
                        commandBuilder.Append(" ").Append(args[i]);
                    }
                    string finalCommand = commandBuilder.ToString().TrimEnd(']');
                    processedArgs.Add($"--run={finalCommand}");
                }
                else
                {
                    processedArgs.Add(args[i]);
                }
            }
            return processedArgs.ToArray();
        }
        #endregion

        /// <summary>
        /// Разбирает массив аргументов командной строки, выполняет действия и возвращает оставшиеся аргументы (пути к файлам).
        /// </summary>
        public static string[] Process(string[] args, List<Argument> definedArguments)
        {
            List<string> remainingArgs = new List<string>();
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (!arg.StartsWith("-"))
                {
                    remainingArgs.Add(arg);
                    continue;
                }

                var argument = definedArguments.Find(a => a.Matches(arg));
                if (argument == null)
                {
                    remainingArgs.Add(arg); // Неизвестный аргумент, возможно, это путь к файлу
                    continue;
                }

                if (argument.HasValue)
                {
                    string value = null;
                    // Ищем разделитель '=', чтобы отделить ключ от значения
                    int separatorIndex = arg.IndexOf('=');
                    if (separatorIndex != -1)
                    {
                        value = arg.Substring(separatorIndex + 1);
                    }
                    // Если разделителя нет, но следующий аргумент не начинается с '-', считаем его значением
                    else if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                    {
                        value = args[i + 1];
                        i++; // Пропускаем следующий элемент, так как он является значением
                    }

                    // Если значение так и не найдено, передаем null
                    argument.ActionWithValue?.Invoke(value);
                }
                else
                {
                    argument.ActionWithoutValue?.Invoke();
                }
            }
            return remainingArgs.ToArray();
        }

        /// <summary>
        /// Отображает справочную информацию по всем доступным аргументам.
        /// </summary>
        public static void ShowHelp(List<Argument> definedArguments)
        {
            System.Console.WriteLine("Available arguments:");
            foreach (var argument in definedArguments)
            {
                Console.WriteLine($"  {argument.Key,-15} {argument.ShortKey,-7} {argument.Description}");
            }
            System.Console.ReadLine();
        }
    }
}
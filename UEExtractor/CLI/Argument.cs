using System;

namespace Solicen.CLI
{
    public class Argument
    {
        public string Key { get; }
        public string ShortKey { get; }
        public string Description { get; }
        public bool HasValue { get; }
        public Action ActionWithoutValue { get; }
        public Action<string> ActionWithValue { get; }

        // Конструктор для флагов (без значения)
        public Argument(string key, string shortKey, string description, Action action)
        {
            Key = key;
            ShortKey = shortKey;
            Description = description;
            HasValue = false;
            ActionWithoutValue = action;
        }

        // Конструктор для аргументов со значением
        public Argument(string key, string shortKey, string description, Action<string> action)
        {
            Key = key;
            ShortKey = shortKey;
            Description = description;
            HasValue = true;
            ActionWithValue = action;
        }

        // Проверяет, соответствует ли строковый аргумент этому правилу
        public bool Matches(string arg)
        {
            // Проверяем полное совпадение для флагов (аргументов без значений)
            if (!HasValue)
            {
                return Key.Equals(arg, StringComparison.OrdinalIgnoreCase) || (!string.IsNullOrEmpty(ShortKey) && ShortKey.Equals(arg, StringComparison.OrdinalIgnoreCase));
            }
            // Проверяем, начинается ли аргумент с ключа для аргументов со значением
            return arg.StartsWith(Key, StringComparison.OrdinalIgnoreCase) ||
                   (!string.IsNullOrEmpty(ShortKey) && arg.StartsWith(ShortKey, StringComparison.OrdinalIgnoreCase));
        }
    }
}
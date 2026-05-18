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
            // Для флагов — только точное совпадение
            if (!HasValue)
            {
                return Key.Equals(arg, StringComparison.OrdinalIgnoreCase) ||
                       (!string.IsNullOrEmpty(ShortKey) && ShortKey.Equals(arg, StringComparison.OrdinalIgnoreCase));
            }
            // Для аргументов со значением: совпадение точное или с '=' (--key=value / --key value)
            // StartsWith(key) alone would match --api:url when key is --api, so we require
            // the next char (if any) to be '=' to avoid prefix collisions.
            return MatchesKey(arg, Key) ||
                   (!string.IsNullOrEmpty(ShortKey) && MatchesKey(arg, ShortKey));
        }

        private static bool MatchesKey(string arg, string key)
        {
            if (arg.Equals(key, StringComparison.OrdinalIgnoreCase)) return true;
            if (arg.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
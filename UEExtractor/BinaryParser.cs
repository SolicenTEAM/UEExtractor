namespace Solicen
{
    internal static class BinaryParser
    {
        public static byte[] ToByteArray(this string s)
        {
            return s.ToCharArray().Select(c => (byte)c).ToArray();
        }

        public static int FindSequence(Span<byte> data, byte[] pattern, int startIndex = 0)
            => FindSequence(data.ToArray(), pattern, startIndex);

        /// <summary>
        /// Находит последовательность байтов в массиве.
        /// </summary>
        public static int FindSequence(byte[] data, byte[] pattern, int startIndex = 0)
        {
            if (pattern.Length == 0) return 0;

            // Предварительная обработка шаблона (префикс-функция)
            int[] lps = ComputeLPSArray(pattern);

            int i = startIndex; // Индекс в data[]
            int j = 0;          // Индекс в pattern[]

            while (i < data.Length)
            {
                if (data[i] == pattern[j])
                {
                    i++;
                    j++;

                    if (j == pattern.Length)
                        return i - j; // Нашли совпадение!
                }
                else
                {
                    if (j != 0)
                        j = lps[j - 1];
                    else
                        i++;
                }
            }

            return -1; // Не найдено
        }
        public static int FindSequenceCaseInsensitive(byte[] data, byte[] pattern, int startIndex = 0)
        {
            if (pattern.Length == 0) return 0;

            // Нормализуем маркер к нижнему регистру (для ASCII)
            byte[] lowerPattern = pattern.Select(b => (byte)char.ToLower((char)b)).ToArray();

            int i = startIndex;
            while (i <= data.Length - pattern.Length)
            {
                bool match = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    byte dataByte = (byte)char.ToLower((char)data[i + j]);
                    if (dataByte != lowerPattern[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match) return i;
                i++;
            }

            return -1;
        }

        // Вычисляет префикс-функцию для KMP
        private static int[] ComputeLPSArray(byte[] pattern)
        {
            int[] lps = new int[pattern.Length];
            int len = 0;
            int i = 1;

            while (i < pattern.Length)
            {
                if (pattern[i] == pattern[len])
                {
                    len++;
                    lps[i] = len;
                    i++;
                }
                else
                {
                    if (len != 0)
                        len = lps[len - 1];
                    else
                    {
                        lps[i] = 0;
                        i++;
                    }
                }
            }

            return lps;
        }
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

public static class MemoryStreamExtensions
{
    public static void Append(this MemoryStream stream, byte value)
    {
        stream.Append(new[] { value });
    }

    public static void Append(this MemoryStream stream, byte[] values)
    {
        stream.Write(values, 0, values.Length);
    }
}

namespace Solicen.Localization.UE4
{
    internal class LocresHelper
    {
        public char Separator = ','; 
        public LocresResult[] LoadCSV(string csvPath)
        {
            List<LocresResult> results = new List<LocresResult>();
            var lines = System.IO.File.ReadAllLines(csvPath); int index = 0; lines.Reverse();
            foreach (var line in lines)
            {
                if (line.StartsWith("#") || Regex.IsMatch(line, @"key.*source")) continue;

                // Separate Values by Regex
                var separateBy = $@"[{Separator}](?=(?:[^""]*""[^""]*"")*[^""]*$)";
                var allValues = Regex.Split(line, separateBy);

                var key = allValues[0];
                var source = allValues[1];

                #region Translation Column
                // It will slow down the creation of the locres file a little,
                // but otherwise we will not read the translation column normally if it contains commas.
                var translation = Regex.Split(line, separateBy)
                    .FirstOrDefault(x => x != source && x != key);
                #endregion

                results.Add(new LocresResult(key, source, translation));
                index++;
            }
            return results.ToArray();
        }

        public static string EscapeKey(string str)
        {
            return str
                .Replace("\n\n", "\\n\\n") 
                .Replace("\n", "\\n")      
                .Replace("\r", "\\r")     
                .Replace("\t", "\\t");     
        }

        public static string UnEscapeKey(string str)
        {
            return str
                .Replace("\\n\\n", "\n\n") 
                .Replace("\\n", "\n")      
                .Replace("\\r", "\r")      
                .Replace("\\t", "\t");     
        }
   
        public static int GetDataCount(byte[] data)
        {
            byte[] buffer = new byte[4];
            Array.Copy(data, 33, buffer, 0, 4);
            return BitConverter.ToInt32(buffer, 0);
        }

        public static int GetLocresOffset(byte[] data, string fString)
        {
            byte[] searchPattern = Encoding.UTF8.GetBytes(fString);
            int patternLength = searchPattern.Length;

            // 1. Подсчитываем байты от начала данных
            int patternIndex = FindPatternIndex(data, searchPattern);
            if (patternIndex == -1)
            {
                throw new InvalidOperationException("Locres pattern not found in data.");
            }
            int X = patternIndex - 8;
            return X;
        }

        public static byte[] WriteNewOffset(byte[] data, int offset)
        {
            byte[] locresOffset = BitConverter.GetBytes((UInt32)offset);
            Array.Copy(locresOffset, 0, data, 17, 4);
            return data;
        }

        public static byte[] GetReadablyHeader(int stringsAll, string filePath)
        {
            byte[] buffer = new byte[34];
            if (File.Exists(filePath))
            {
                // Чтение первых 33 байтов из файла
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    fs.Read(buffer, 0, 33);
                }
            }
            else
            {
                buffer = new byte[] { 0x0E, 0x14, 0x74, 0x75, 0x67, 
                    0x4A, 0x03, 0xFC, 0x4A, 0x15, 0x90, 0x9D, 0xC3, 
                    0x37, 0x7F, 0x1B, 0x01, 0x03, 0x04, 0x00, 0x00,
                    0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00,
                    0x00, 0x00, 0x00, 0x00, 0x16, 0x00, 0x00, 0x00 }
                ;
            }

            // Чтение первых 18 байтов
            byte[] first17Bytes = new byte[17];
            Array.Copy(buffer, 0, first17Bytes, 0, 17);

            // Зануление байтов с 18 по 24
            for (int i = 18; i <= 24; i++)
            {
                buffer[i] = 0x00;
            }

            // Чтение байта на позиции 25
            byte byte26 = buffer[25];

            // Зануление байтов с 26 по 33
            for (int i = 26; i <= 32; i++)
            {
                buffer[i] = 0x00;
            }

            // Запись числа stringsAll в байты с 33 по 34 (uint)
            byte[] stringsAllBytes = BitConverter.GetBytes((uint)stringsAll);
            Array.Copy(stringsAllBytes, 0, buffer, 33, 4);

            // Возвращаем измененный буфер
            return buffer;
        }

        private static int FindPatternIndex(byte[] data, byte[] pattern)
        {
            for (int i = 0; i <= data.Length - pattern.Length; i++)
            {
                if (IsPatternMatch(data, i, pattern))
                {
                    return i;
                }
            }
            return -1; // Шаблон не найден
        }

        private static bool IsPatternMatch(byte[] data, int index, byte[] pattern)
        {
            for (int i = 0; i < pattern.Length; i++)
            {
                if (data[index + i] != pattern[i])
                {
                    return false;
                }
            }
            return true;
        }
    }
}

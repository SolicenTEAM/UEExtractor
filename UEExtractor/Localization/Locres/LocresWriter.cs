using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Solicen.Localization.UE4
{
    class LocresWriter
    {
        string outputPath = string.Empty;
        string locresPath = string.Empty;
        string csvPath = string.Empty;

        public LocresWriter(string outputPath, string locresPath)
        {
            this.outputPath = outputPath; this.locresPath = locresPath;
        }

        private static readonly byte[] separatorHeader = new byte[] { 0x21, 0x00, 0x00, 0x00 };
        private static readonly byte[] separatorSequence = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00 };

        public void Write(LocresResult[] locres)
        {
            var byteStream  // Определяем MemoryStream для записи массива байтов
                = new MemoryStream();

            #region Документация Offset 
            // Первые 16 байтов это заголовок
            // 17 - 20 байты = смещение до текстовых данных
            // 26 = просто 0x01 первый байт
            // 34 = количество строк в файле 

            // Следовательно, мы забираем из Engine.locres первые 16 байтов, после чего нулим до 26 байта,
            // опять нулим до 33 и на 34 байт устанавливаем количество строк в .locres файле.
            #endregion 

            var locresHeader // Получаем читаемый заголовок locres файла версии Compact
                = LocresHelper.GetReadablyHeader(locres.Length, locresPath);

            byteStream.Append(locresHeader); // Добавляем сам заголовок | Add Locres header 

            #region Запись Ключей/Хешей | Write All KeyHash 
            for (int i = 0; i < locres.Length; i++)
            {
                byteStream.Append(separatorHeader);
                var key = locres[i].Key;
                var source = LocresHelper.UnEscapeKey(locres[i].Source);
                var hash = LocresSharp.Crc.StrCrc32(source);

                byteStream.Append(Encoding.UTF8.GetBytes(key));
                byteStream.Append(new byte[] { 0x00 });
                byteStream.Append(BitConverter.GetBytes(hash));

                byte[] indexOf = BitConverter.GetBytes(i);
                byteStream.Append(indexOf);
            }
            #endregion
            
            byteStream // Добавление записи о количестве строк
                .Append(BitConverter.GetBytes(locres.Length));

            #region Запись всех строк | Write all strings
            for (int i = 0; i < locres.Length; i++)
            {
                var source = LocresHelper.UnEscapeKey(locres[i].Source);
                int defaultLength = source.Length + 1;
                var sourceByte = BitConverter.GetBytes(defaultLength);

                byteStream.Append(sourceByte);
                byteStream.Append(Encoding.UTF8.GetBytes(source));
                byteStream.Append(0x00);
            }
            #endregion

            var data = byteStream.ToArray(); // Получаем байты перед финальной записью
            var offset                      // Получаем новое смещение для массива
                = LocresHelper.GetLocresOffset(data, locres.FirstOrDefault().Source);
            data = LocresHelper.WriteNewOffset(data, offset); // Записываем новое смещение в массив 
            File.WriteAllBytes(outputPath, data); // Записываем сам файл

            var info = new FileInfo(outputPath).FullName; // Опционально: получаем полный путь до файла
            Console.WriteLine($"\nCompleted! locres saved to: {info}\n");

        }

    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Solicen.Localization.UE4.CustomSignature
{
    internal class LocresLikeSignature
    {
        private static byte[] GetBytes(byte[] source, int index, int count)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (index < 0 || index >= source.Length) throw new ArgumentOutOfRangeException(nameof(index));
            if (count < 0 || index + count > source.Length) throw new ArgumentOutOfRangeException(nameof(count));

            byte[] result = new byte[count];
            Array.Copy(source, index, result, 0, count);
            return result;
        }

        public static List<LocresResult> ExtractDataFromArray(byte[] bArray)
        {
            var results = new List<LocresResult>();
            int index = 0; int startIndex = 0;

            while (bArray.Length - 1 > index)
            {
                if (bArray[index] == 0x01 && bArray[index+1] > 0x00)
                {
                    index += 1;
                    int valueLenght = 0;
                    startIndex = index + 1;
                    while (true)
                    {
                        if (bArray[index] == 0x01 && bArray[index+1] > 0x00)
                        {
                            valueLenght -= 4; break;
                        }
                        index++; valueLenght++;
                    }
                    if (valueLenght > 0)
                    {

                        var res = GetBytes(bArray, startIndex, valueLenght);
                        var hashCandidate = Encoding.UTF8.GetString(res).Escape();

                        Console.WriteLine($"Hash candidate is \"{hashCandidate}\"");
                        results.Add(new LocresResult { Key = hashCandidate });
                    }

                }
                index++;
            }

            return results;

        }
    }
}

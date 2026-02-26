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
        /// <summary>
        /// Simplified string between quotation marks.
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string SimplifyQMarksInStr(string str)
        {
            if (str == null) return null;
            return str.StartsWith("\"") && str.Contains(',') && str.EndsWith("\"") ? str.Trim('\"') : str;
        }

        public static string EscapeKey(string str)
        {
            return str
                .Replace("\n\n", "\\n\\n") 
                .Replace("\n", "\\n")      
                .Replace("\r", "\\r")     
                .Replace("\t", "\\t") 
                .Replace("\\r\\n", "<cf>")
                .Replace("\"", "\"\"");            
        }

        public static string UnEscapeKey(string str)
        {
            return str
                .Replace("<cf>", "\\r\\n")
                .Replace("\\n\\n", "\n\n")
                .Replace("\\n", "\n")
                .Replace("\\r", "\r")
                .Replace("\\t", "\t")
                .Replace("\"\"","\"");          
        }
    }
}

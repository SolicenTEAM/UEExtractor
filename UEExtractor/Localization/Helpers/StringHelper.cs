using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

static internal class StringHelper
{
    /// <summary>
    /// Simplified string between quotation marks.
    /// </summary>
    /// <param name="str"></param>
    /// <returns></returns>
    public static string SimplifyQMarksInStr(this string str)
    {
        if (str == null) return null;
        return str.StartsWith("\"") && str.Contains(',') && str.EndsWith("\"") ? str.Trim('\"') : str;
    }

    public static string Escape(this string str)
    {
        return str

            .Replace("\n\n", "\\n\\n")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t")
            .Replace("\\r\\n", "<cf>")
            .Replace("\"", "\"\"");
    }

    public static string Unescape(this string str)
    {
        return str
            .Replace("<cf>", "\\r\\n")
            .Replace("\\n\\n", "\n\n")
            .Replace("\\n", "\n")
            .Replace("\\r", "\r")
            .Replace("\\t", "\t")
            .Replace("\"\"", "\"");
    }
    public static string UE_FolderWithFileName(this string unrealFile)
    {
        var match = new Regex(@"\\([^\\]+)\\Content(?!.*Paks)\\.+").Match(unrealFile).Value.TrimEnd('\"');
        return string.IsNullOrWhiteSpace(match) ? unrealFile : match;
    }
    public static string UE_FolderWithoutFileName(this string unrealFile)
    {
        var match = new Regex(@"\\([^\\]+)\\Content(?!.*Paks)\\.+").Match(unrealFile).Value.TrimEnd('\"');
        return string.IsNullOrWhiteSpace(match) ? unrealFile : match.Replace(Path.GetFileName(unrealFile), "");
    }

    public static bool IsAllNumber(this string str)
    {
        return str.All(x => char.IsNumber(x) == true);
    }
    public static bool IsLower(this string str)
    {
        var letters = str.Where(c => char.IsLetter(c));
        return letters.All(c => char.IsLower(c) == true);
    }
    public static bool IsBoolean(this string str)
    {
        bool.TryParse(str, out bool result);
        if (str == "True" || str == "False") return true;
        return result;
    }
    public static bool IsPath(this string str)
    {
        return Regex.Match(str, @"(.*[\\].*[\\])|(.*[\/].*[\/])").Success;
    }

    public static bool IsStringDigit(this string str)
    {
        return Regex.Match(str, @"^\w*?[\d]").Success;
    }
    public static bool IsUpper(this string str)
    {
        var letters = str.Where(c => char.IsLetter(c));
        return letters.All(c => char.IsUpper(c) == true);
    }
    public static bool IsAllOne(this string str)
    {
        return str.All(c => c == str[0] == true);
    }
    public static bool IsAllDot(this string str)
    {
        return str.All(c => c == '.' == true);
    }
    public static bool IsUpperLower(this string str)
    {
        var space = str.Where(c => char.IsWhiteSpace(c)).Count();
        if (space > 0) return false;
        else
        {
            var upperChars = str.Where(c => char.IsUpper(c)).Count();
            var lowerChars = str.Where(c => char.IsLower(c)).Count();
            if (upperChars >= 2 && lowerChars > upperChars)
                return true;
        }
        return false;
    }

    public static bool IsGUID(this string str)
    {
        if (string.IsNullOrEmpty(str)) return false;
        // Формат без дефисов (N).
        return Guid.TryParseExact(str, "N", out _);
    }
}

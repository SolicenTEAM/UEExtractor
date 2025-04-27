using Solicen.Localization.UE4;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class CSVWriter
{
    public string FilePath { get; }
    public CSVWriter(string filePath)
    {
        if (File.Exists(filePath))
            File.Delete(filePath);

        FilePath = filePath;
    }

    public void WriteLine(string text)
    {
        if (FilePath == string.Empty) return;
        text = LocresHelper.EscapeKey(text);
        File.AppendText(FilePath).WriteLine(text+"\n");
    }
}

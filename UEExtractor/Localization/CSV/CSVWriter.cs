using Solicen.Localization.UE4;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class CSVWriter
{
    public StreamWriter Writer;
    public string FilePath { get; }
    public CSVWriter(string filePath)
    {
        if (filePath == string.Empty) return;
        if (File.Exists(filePath))
            File.Delete(filePath);

        FilePath = filePath;
        Writer = new StreamWriter(FilePath, true);
    }

    public void WriteLine(string text)
    {
        if (FilePath == string.Empty) return;
        text = LocresHelper.EscapeKey(text);
        Writer.WriteLine(text + "\n");
        //File.AppendText(FilePath).WriteLine(text + "\n");
    }
}

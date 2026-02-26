using Solicen.Localization.UE4;
using System.Text;

public class CSV
{
    public static string EscapeCsvField(string field)
    {
        if (field == null) return field;                      // Return empty string
        if (field.Contains("\"") && !field.Contains("\"\""))
            field = field.Replace("\"", "\"\"");
        if (field.Contains(',') || field.Contains('\"'))      // Return QMarks between " string " if detect comma symbol in line. 
            field = $"\"{field}\"";
        return field;
    }

    public class Table
    {
        public class Row
        {
            public readonly List<string> Columns;
            private readonly int _maxColumns;
            public Row(List<string> columns, int maxColumns)
            {
                Columns = columns;
                _maxColumns = maxColumns;
                // Добиваем колонки до максимального количества пустыми значениями
                while (Columns.Count < _maxColumns) { Columns.Add(string.Empty); }
            }

            public string this[int index]
            {
                get
                {
                    if (index < 0 || index >= _maxColumns)
                        return string.Empty;
                    return Columns.Count > index ? Columns[index] : string.Empty;
                }
            }
            public int Count => _maxColumns;

            // Новые методы для безопасной проверки значений
            public bool ColumnEquals(int index, string expectedValue, StringComparison comparison = StringComparison.Ordinal)
            {
                if (index < 0 || index >= _maxColumns)
                    return false;

                string actualValue = index < Columns.Count ? Columns[index] : string.Empty;
                return string.Equals(actualValue, expectedValue, comparison);
            }

            public bool ColumnContains(int index, string searchValue, StringComparison comparison = StringComparison.Ordinal)
            {
                if (index < 0 || index >= _maxColumns)
                    return false;

                string actualValue = index < Columns.Count ? Columns[index] : string.Empty;
                return actualValue.Contains(searchValue, comparison);
            }

            public bool ColumnStartsWith(int index, string prefix, StringComparison comparison = StringComparison.Ordinal)
            {
                if (index < 0 || index >= _maxColumns)
                    return false;

                string actualValue = index < Columns.Count ? Columns[index] : string.Empty;
                return actualValue.StartsWith(prefix, comparison);
            }

            public bool ColumnEndsWith(int index, string suffix, StringComparison comparison = StringComparison.Ordinal)
            {
                if (index < 0 || index >= _maxColumns)
                    return false;

                string actualValue = index < Columns.Count ? Columns[index] : string.Empty;
                return actualValue.EndsWith(suffix, comparison);
            }

            public bool IsColumnEmpty(int index)
            {
                if (index < 0 || index >= _maxColumns)
                    return true;

                string value = index < Columns.Count ? Columns[index] : string.Empty;
                return string.IsNullOrEmpty(value);
            }


            public bool TryGetColumn(int index, out string value)
            {
                value = string.Empty;

                if (index < 0 || index >= _maxColumns)
                    return false;

                if (index < Columns.Count)
                {
                    value = Columns[index];
                    return true;
                }

                return true; // Возвращаем true, но value будет пустым
            }

            public override string ToString()
            {
                return string.Join(" | ", Columns);
            }

            public IEnumerable<string> GetColumns()
            {
                return Columns;
            }
        }
        private readonly char _delimiter;
        private readonly List<Row> _rows = new List<Row>();
        private int _maxColumns = 0;
        public Table(char delimiter = ',')
        {
            _delimiter = delimiter;
        }

        public IReadOnlyList<Row> Rows => _rows.AsReadOnly();
        public void Parse(string filePath, Action<Row> processRow = null)
        {
            using (var reader = new StreamReader(filePath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    var row = ParseLine(line);
                    _rows.Add(row);
                    processRow?.Invoke(row);
                }
            }
        }
        private Row ParseLine(string line)
        {
            var columns = new List<string>();
            var currentColumn = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char currentChar = line[i];
                //if (inQuotes && currentChar == ',') i++;
                if (currentChar == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        // Экранированная кавычка внутри кавычек
                        currentColumn.Append('"');
                        i++; // Пропускаем следующую кавычку
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (currentChar == _delimiter && !inQuotes)
                {
                    // Конец колонки
                    columns.Add(currentColumn.ToString());
                    currentColumn.Clear();
                }
                else
                {
                    currentColumn.Append(currentChar);
                }
            }

            // Добавляем последнюю колонку
            columns.Add(currentColumn.ToString());
            // Обновляем максимальное количество колонок
            _maxColumns = Math.Max(_maxColumns, columns.Count);
            return new Row(columns, _maxColumns);
        }

        // Метод для получения всех строк с одинаковым количеством колонок
        public IEnumerable<Row> GetNormalizedRows()
        {
            return _rows.Select(row => new Row(
                row.GetColumns().ToList(),
                _maxColumns
            ));
        }
    }

    // Статические методы для удобного использования
    public static Table Parse(string filePath, Action<Table.Row> processRow = null, char delimiter = ',')
    {
        var table = new Table(delimiter);
        table.Parse(filePath, processRow);
        return table;
    }

    public class Writer
    {
        public StreamWriter _Writer;
        public string FilePath { get; }
        public Writer(string? filePath, bool append = false)
        {
            if (filePath == string.Empty) return;
            if (!append && File.Exists(filePath))
                File.Delete(filePath);

            FilePath = filePath;
            _Writer = new StreamWriter(FilePath, true);
        }

        public void WriteLine(string text)
        {
            if (FilePath == string.Empty) return;
            text = LocresHelper.EscapeKey(text);
            _Writer.WriteLine(text + "\r");
        }
    }
}




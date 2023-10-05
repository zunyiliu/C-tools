public class CsvHelper
{
    // data -- the data that will be written into bytes
    // columns -- name of perperties, will use T's properties if is null
    // headers -- name of csv file headers, will use T's properties if is null
    public static byte[] WriteDataToCsvByte<T>(IEnumerable<T> data, IEnumerable<string> columns = null, IEnumerable<string> headers = null)
    {          
        var type = typeof(T);
        if (columns == null)
        {
            // only .NET 7 and later has the ordering based upon the metadata ordering in the assembly
            // use OrderBy MetadataToken to fix this issue
            columns = type.GetProperties().OrderBy(x => x.MetadataToken).Select(p => p.Name);
        }

        StringBuilder csv = new StringBuilder();
        csv.AppendLine(headers == null ? string.Join(",", columns) : string.Join(",", headers));

        foreach (var item in data)
        {
            List<string> row = new List<string>();

            foreach (var column in columns)
            {
                var property = type.GetProperty(column);
                var cell = string.Empty;
                if (property != null)
                {
                    var propertyValue = property.GetValue(item);
                    cell = propertyValue?.ToString() ?? string.Empty;
                }
                row.Add(cell);
            }
            csv.AppendLine(string.Join(",", row));
        }

        return Encoding.UTF8.GetBytes(csv.ToString());
    }

    // data -- the raw csv data that will be written into Object T
    // each line in csv is an Object T, T can't be a value type
    public static List<T> ReadCsv<T>(byte[] data)
    {
        if (typeof(T).IsValueType || typeof(T) == typeof(string))
        {
            throw new InvalidOperationException("T can't be a value type");
        }

        string csvText = Encoding.UTF8.GetString(data);
        string[] lines = csvText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        
        var list = new List<T>();
        if (lines.Length <= 1)
        {
            return list;
        }

        var type = typeof(T);

        // read header line
        var headers = lines[0].Split(',');
        var properties = new PropertyInfo[headers.Length];
        for (int i = 0; i < headers.Length; i++)
        {
            properties[i] = type.GetProperty(headers[i]);
        }

        // read content lines
        for (int i = 1; i < lines.Length; i++)
        {
            var values = lines[i].Split(',');
            T obj = (T) Activator.CreateInstance(typeof(T));

            for (int j = 0; j < values.Length; j++)
            {
                // read jth cell in ith line
                if (properties[j] != null && properties[j].CanWrite)
                {
                    object convertedValue = ParseValue(values[j], properties[j].PropertyType);
                    properties[j].SetValue(obj, convertedValue);
                }
            }

            list.Add(obj);
        }

        return list;
    }

    private static object ParseValue(string value, Type type)
    {
        if (string.IsNullOrEmpty(value))
        {
            return default;
        }

        // Use TypeConverter for non-string types
        if (type != typeof(string))
        {
            var converter = System.ComponentModel.TypeDescriptor.GetConverter(type);
            if (converter.CanConvertFrom(typeof(string)))
            {
                return converter.ConvertFrom(value);
            }
        }

        // For string or unsupported types, use Convert.ChangeType
        return Convert.ChangeType(value, type);
    }
}

using System.Text.Json;

namespace ReportesAPI.Compartido.Exporte;

public static class CsvExporter
{
    public static string ToCsv<T>(IEnumerable<T> data)
    {
        var json = JsonSerializer.Serialize(data);
        var rows = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json);
        if (rows is null || rows.Count == 0) return string.Empty;

        var headers = rows[0].Keys;
        var csv = new System.Text.StringBuilder();
        csv.AppendLine(string.Join(",", headers));

        foreach (var row in rows)
        {
            csv.AppendLine(string.Join(",", headers.Select(h => row.GetValueOrDefault(h)?.ToString() ?? "")));
        }

        return csv.ToString();
    }
}

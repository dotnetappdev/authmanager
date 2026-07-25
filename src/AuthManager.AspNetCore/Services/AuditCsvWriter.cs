using System.Text;
using AuthManager.Core.Models;

namespace AuthManager.AspNetCore.Services;

/// <summary>Shared CSV formatting for audit log exports.</summary>
internal static class AuditCsvWriter
{
    private static readonly string[] Columns =
        ["Timestamp", "Action", "EntityType", "EntityId", "EntityName", "PerformedBy", "IpAddress", "Success", "ErrorMessage"];

    public static byte[] Write(IEnumerable<AuditEntry> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", Columns));

        foreach (var e in entries)
        {
            sb.AppendLine(string.Join(",",
                Csv(e.Timestamp.ToString("O")),
                Csv(e.Action),
                Csv(e.EntityType),
                Csv(e.EntityId),
                Csv(e.EntityName ?? string.Empty),
                Csv(e.PerformedByUserName ?? e.PerformedByUserId ?? string.Empty),
                Csv(e.IpAddress ?? string.Empty),
                Csv(e.Success.ToString()),
                Csv(e.ErrorMessage ?? string.Empty)
            ));
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string Csv(string value)
        => value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}

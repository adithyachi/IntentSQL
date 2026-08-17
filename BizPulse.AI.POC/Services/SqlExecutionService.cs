using BizPulse.AI.POC.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace BizPulse.AI.POC.Services;

public class SqlExecutionService
{
    private readonly AppDbContext _dbContext;

    public SqlExecutionService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<Dictionary<string, object?>>> ExecuteAsync(
        string sql,
        CancellationToken cancellationToken = default)
    {
        ValidateSql(sql);

        var connection = _dbContext.Database.GetDbConnection();

        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();

        command.CommandText = sql;
        command.CommandTimeout = 60;

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);

        var results = new List<Dictionary<string, object?>>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>(
                StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < reader.FieldCount; i++)
            {
                var columnName = reader.GetName(i);

                var value = await reader.IsDBNullAsync(
                    i,
                    cancellationToken)
                    ? null
                    : reader.GetValue(i);

                row[columnName] = value;
            }

            results.Add(row);
        }

        return results;
    }

    private static void ValidateSql(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            throw new SqlValidationException(
                "Generated SQL is empty.");
        }

        var normalized = sql.Trim();

        if (normalized.EndsWith(";"))
        {
            normalized = normalized[..^1].Trim();
        }

        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new SqlValidationException(
                "Generated SQL is empty.");
        }

        var isSelect =
            normalized.StartsWith(
                "SELECT",
                StringComparison.OrdinalIgnoreCase);

        var isCte =
            normalized.StartsWith(
                "WITH",
                StringComparison.OrdinalIgnoreCase);

        if (!isSelect && !isCte)
        {
            throw new SqlValidationException(
                "Only SELECT statements are allowed.");
        }

        // No additional semicolons = no multiple statements.
        if (normalized.Contains(';'))
        {
            throw new SqlValidationException(
                "Multiple SQL statements are not allowed.");
        }

        var forbiddenKeywords = new[]
        {
        "INSERT",
        "UPDATE",
        "DELETE",
        "DROP",
        "ALTER",
        "TRUNCATE",
        "CREATE",
        "GRANT",
        "REVOKE",
        "MERGE"
    };

        foreach (var keyword in forbiddenKeywords)
        {
            if (Regex.IsMatch(
                normalized,
                $@"\b{Regex.Escape(keyword)}\b",
                RegexOptions.IgnoreCase))
            {
                throw new SqlValidationException(
                    $"SQL contains forbidden operation: {keyword}");
            }
        }
    }
}
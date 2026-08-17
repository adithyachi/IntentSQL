using BizPulse.AI.POC.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Data;

namespace BizPulse.AI.POC.Services;

public class DataExplorerService
{
    private readonly AppDbContext _dbContext;

    private static readonly Dictionary<string, string[]> TableColumns =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["categories"] =
            [
                "Id",
                "Name"
            ],

            ["customers"] =
            [
                "Id",
                "Name",
                "Email",
                "City",
                "Country"
            ],

            ["orders"] =
            [
                "Id",
                "CustomerId",
                "OrderDate",
                "Status"
            ],

            ["order_items"] =
            [
                "Id",
                "OrderId",
                "ProductId",
                "Quantity",
                "UnitPrice"
            ],

            ["products"] =
            [
                "Id",
                "Name",
                "CategoryId",
                "Price",
                "CostPrice"
            ]
        };

    public DataExplorerService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IReadOnlyList<string> GetTables()
    {
        return TableColumns.Keys.ToList();
    }

    public IReadOnlyList<string> GetColumns(string table)
    {
        var normalizedTable = NormalizeTable(table);

        return TableColumns[normalizedTable];
    }

    public async Task<DataExplorerResult> GetDataAsync(
        string table,
        int page,
        int pageSize,
        string? sortColumn,
        string? sortDirection,
        Dictionary<string, string>? filters,
        CancellationToken cancellationToken = default)
    {
        var normalizedTable = NormalizeTable(table);

        var columns = TableColumns[normalizedTable];

        page = Math.Max(page, 1);

        pageSize = pageSize switch
        {
            10 => 10,
            25 => 25,
            50 => 50,
            100 => 100,
            _ => 25
        };

        if (string.IsNullOrWhiteSpace(sortColumn) ||
            !columns.Contains(
                sortColumn,
                StringComparer.OrdinalIgnoreCase))
        {
            sortColumn = columns[0];
        }

        var normalizedSortColumn =
            columns.First(
                x => x.Equals(
                    sortColumn,
                    StringComparison.OrdinalIgnoreCase));

        var normalizedSortDirection =
            string.Equals(
                sortDirection,
                "desc",
                StringComparison.OrdinalIgnoreCase)
                    ? "DESC"
                    : "ASC";

        var connection =
            _dbContext.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        var whereParts = new List<string>();

        var parameters = new List<NpgsqlParameter>();

        if (filters != null)
        {
            foreach (var filter in filters)
            {
                if (string.IsNullOrWhiteSpace(filter.Value))
                {
                    continue;
                }

                var actualColumn =
                    columns.FirstOrDefault(
                        x => x.Equals(
                            filter.Key,
                            StringComparison.OrdinalIgnoreCase));

                if (actualColumn == null)
                {
                    continue;
                }

                var parameterName =
                    $"@filter_{parameters.Count}";

                whereParts.Add(
                    $"CAST(\"{actualColumn}\" AS TEXT) ILIKE {parameterName}");

                parameters.Add(
                    new NpgsqlParameter(
                        parameterName,
                        $"%{filter.Value.Trim()}%"));
            }
        }

        var whereClause =
            whereParts.Count > 0
                ? "WHERE " + string.Join(
                    " AND ",
                    whereParts)
                : string.Empty;

        var countSql = $"""
            SELECT COUNT(*)
            FROM "{normalizedTable}"
            {whereClause};
            """;

        await using var countCommand =
            connection.CreateCommand();

        countCommand.CommandText = countSql;
        countCommand.CommandTimeout = 60;

        foreach (var parameter in parameters)
        {
            countCommand.Parameters.Add(
                new NpgsqlParameter(
                    parameter.ParameterName,
                    parameter.Value));
        }

        var countResult =
            await countCommand.ExecuteScalarAsync(
                cancellationToken);

        var totalRows =
            Convert.ToInt32(countResult);

        var offset =
            (page - 1) * pageSize;

        var columnList =
            string.Join(
                ", ",
                columns.Select(
                    column => $"\"{column}\""));

        var dataSql = $"""
            SELECT {columnList}
            FROM "{normalizedTable}"
            {whereClause}
            ORDER BY "{normalizedSortColumn}" {normalizedSortDirection}
            LIMIT @pageSize
            OFFSET @offset;
            """;

        await using var command =
            connection.CreateCommand();

        command.CommandText = dataSql;
        command.CommandTimeout = 60;

        foreach (var parameter in parameters)
        {
            command.Parameters.Add(
                new NpgsqlParameter(
                    parameter.ParameterName,
                    parameter.Value));
        }

        command.Parameters.Add(
            new NpgsqlParameter(
                "@pageSize",
                pageSize));

        command.Parameters.Add(
            new NpgsqlParameter(
                "@offset",
                offset));

        await using var reader =
            await command.ExecuteReaderAsync(
                cancellationToken);

        var rows =
            new List<Dictionary<string, object?>>();

        while (await reader.ReadAsync(
            cancellationToken))
        {
            var row =
                new Dictionary<string, object?>(
                    StringComparer.OrdinalIgnoreCase);

            for (var i = 0;
                 i < reader.FieldCount;
                 i++)
            {
                var value =
                    await reader.IsDBNullAsync(
                        i,
                        cancellationToken)
                        ? null
                        : reader.GetValue(i);

                row[reader.GetName(i)] = value;
            }

            rows.Add(row);
        }

        return new DataExplorerResult(
            normalizedTable,
            columns.ToList(),
            rows,
            totalRows,
            page,
            pageSize,
            normalizedSortColumn,
            normalizedSortDirection.ToLowerInvariant());
    }

    private static string NormalizeTable(
        string table)
    {
        if (string.IsNullOrWhiteSpace(table) ||
            !TableColumns.ContainsKey(table))
        {
            return "customers";
        }

        return TableColumns.Keys.First(
            x => x.Equals(
                table,
                StringComparison.OrdinalIgnoreCase));
    }
}

public record DataExplorerResult(
    string Table,
    List<string> Columns,
    List<Dictionary<string, object?>> Rows,
    int TotalRows,
    int Page,
    int PageSize,
    string SortColumn,
    string SortDirection);
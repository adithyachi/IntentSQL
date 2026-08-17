using Npgsql;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
namespace BizPulse.AI.POC.Services;

public class DatabaseSchemaService
{
    private readonly IConfiguration _configuration;
    private const string SchemaCacheKey = "BizPulse.DatabaseSchemaContext";
    private readonly IMemoryCache _memoryCache;
    public DatabaseSchemaService(IConfiguration configuration,
        IMemoryCache memoryCache)
    {
        _configuration = configuration;
        _memoryCache = memoryCache;
    }

    public async Task<string> GetSchemaContextAsync(
        CancellationToken cancellationToken = default)
    {
        if (_memoryCache.TryGetValue(SchemaCacheKey, out string? cachedSchema))
        {
            return cachedSchema!;
        }

        var connectionString =
            _configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "DefaultConnection connection string is not configured.");
        }

        await using var connection =
            new NpgsqlConnection(connectionString);

        await connection.OpenAsync(cancellationToken);

        var tables = await GetTablesAsync(
            connection,
            cancellationToken);

        var columns = await GetColumnsAsync(
            connection,
            cancellationToken);

        var primaryKeys = await GetPrimaryKeysAsync(
            connection,
            cancellationToken);

        var relationships = await GetRelationshipsAsync(
            connection,
            cancellationToken);

        var uniqueConstraints = await GetUniqueConstraintsAsync(
            connection,
            cancellationToken);

        var indexes = await GetIndexesAsync(
            connection,
            cancellationToken);

        string schemaContext = BuildContext(
            tables,
            columns,
            primaryKeys,
            relationships,
            uniqueConstraints,
            indexes);

        _memoryCache.Set(SchemaCacheKey,schemaContext);

        return schemaContext;
    }

    private static async Task<List<string>> GetTablesAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = 'public'
              AND table_type = 'BASE TABLE'
              AND table_name <> '__EFMigrationsHistory'
            ORDER BY table_name;
            """;

        await using var command =
            new NpgsqlCommand(sql, connection);

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        var result = new List<string>();

        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(reader.GetString(0));
        }

        return result;
    }

    private static async Task<List<ColumnInfo>> GetColumnsAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                table_name,
                column_name,
                data_type,
                is_nullable,
                column_default,
                ordinal_position
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name <> '__EFMigrationsHistory'
            ORDER BY table_name, ordinal_position;
            """;

        await using var command =
            new NpgsqlCommand(sql, connection);

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        var result = new List<ColumnInfo>();

        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new ColumnInfo(
                TableName: reader.GetString(0),
                ColumnName: reader.GetString(1),
                DataType: reader.GetString(2),
                IsNullable: reader.GetString(3) == "YES",
                DefaultValue: reader.IsDBNull(4)
                    ? null
                    : reader.GetString(4),
                OrdinalPosition: reader.GetInt32(5)));
        }

        return result;
    }

    private static async Task<List<PrimaryKeyInfo>> GetPrimaryKeysAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                tc.table_name,
                kcu.column_name
            FROM information_schema.table_constraints tc
            JOIN information_schema.key_column_usage kcu
                ON tc.constraint_name = kcu.constraint_name
               AND tc.table_schema = kcu.table_schema
               AND tc.table_name = kcu.table_name
            WHERE tc.constraint_type = 'PRIMARY KEY'
              AND tc.table_schema = 'public'
            ORDER BY
                tc.table_name,
                kcu.ordinal_position;
            """;

        await using var command =
            new NpgsqlCommand(sql, connection);

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        var result = new List<PrimaryKeyInfo>();

        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new PrimaryKeyInfo(
                reader.GetString(0),
                reader.GetString(1)));
        }

        return result;
    }

    private static async Task<List<RelationshipInfo>>
        GetRelationshipsAsync(
            NpgsqlConnection connection,
            CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                tc.constraint_name,
                tc.table_name AS source_table,
                kcu.column_name AS source_column,
                ccu.table_name AS target_table,
                ccu.column_name AS target_column
            FROM information_schema.table_constraints tc
            JOIN information_schema.key_column_usage kcu
                ON tc.constraint_name = kcu.constraint_name
               AND tc.table_schema = kcu.table_schema
               AND tc.table_name = kcu.table_name
            JOIN information_schema.constraint_column_usage ccu
                ON tc.constraint_name = ccu.constraint_name
               AND tc.table_schema = ccu.table_schema
            WHERE tc.constraint_type = 'FOREIGN KEY'
              AND tc.table_schema = 'public'
            ORDER BY
                tc.table_name,
                kcu.column_name;
            """;

        await using var command =
            new NpgsqlCommand(sql, connection);

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        var result = new List<RelationshipInfo>();

        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new RelationshipInfo(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4)));
        }

        return result;
    }

    private static async Task<List<UniqueConstraintInfo>>
        GetUniqueConstraintsAsync(
            NpgsqlConnection connection,
            CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                tc.constraint_name,
                tc.table_name,
                kcu.column_name
            FROM information_schema.table_constraints tc
            JOIN information_schema.key_column_usage kcu
                ON tc.constraint_name = kcu.constraint_name
               AND tc.table_schema = kcu.table_schema
               AND tc.table_name = kcu.table_name
            WHERE tc.constraint_type = 'UNIQUE'
              AND tc.table_schema = 'public'
            ORDER BY
                tc.table_name,
                tc.constraint_name,
                kcu.ordinal_position;
            """;

        await using var command =
            new NpgsqlCommand(sql, connection);

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        var result = new List<UniqueConstraintInfo>();

        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new UniqueConstraintInfo(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2)));
        }

        return result;
    }

    private static async Task<List<IndexInfo>> GetIndexesAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                tablename,
                indexname,
                indexdef
            FROM pg_indexes
            WHERE schemaname = 'public'
              AND tablename <> '__EFMigrationsHistory'
            ORDER BY tablename, indexname;
            """;

        await using var command =
            new NpgsqlCommand(sql, connection);

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        var result = new List<IndexInfo>();

        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new IndexInfo(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2)));
        }

        return result;
    }

    private static string BuildContext(
        List<string> tables,
        List<ColumnInfo> columns,
        List<PrimaryKeyInfo> primaryKeys,
        List<RelationshipInfo> relationships,
        List<UniqueConstraintInfo> uniqueConstraints,
        List<IndexInfo> indexes)
    {
        var builder = new StringBuilder();

        builder.AppendLine("DATABASE STRUCTURE");
        builder.AppendLine("==================");
        builder.AppendLine();

        foreach (var table in tables)
        {
            builder.AppendLine($"TABLE: {table}");

            var tableColumns = columns
                .Where(x => x.TableName.Equals(
                    table,
                    StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.OrdinalPosition);

            foreach (var column in tableColumns)
            {
                var nullable =
                    column.IsNullable
                        ? "NULLABLE"
                        : "NOT NULL";

                var defaultValue =
                    string.IsNullOrWhiteSpace(column.DefaultValue)
                        ? string.Empty
                        : $" DEFAULT {column.DefaultValue}";

                builder.AppendLine(
                    $"  - {column.ColumnName} " +
                    $"({column.DataType}) " +
                    $"{nullable}{defaultValue}");
            }

            var tablePrimaryKeys = primaryKeys
                .Where(x => x.TableName.Equals(
                    table,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (tablePrimaryKeys.Count > 0)
            {
                builder.AppendLine(
                    $"  PRIMARY KEY: {string.Join(
                        ", ",
                        tablePrimaryKeys.Select(x => x.ColumnName))}");
            }

            builder.AppendLine();
        }

        builder.AppendLine("FOREIGN KEY RELATIONSHIPS");
        builder.AppendLine("=========================");
        builder.AppendLine();

        if (relationships.Count == 0)
        {
            builder.AppendLine("No foreign-key relationships found.");
        }
        else
        {
            foreach (var relationship in relationships)
            {
                builder.AppendLine(
                    $"  {relationship.SourceTable}." +
                    $"{relationship.SourceColumn} " +
                    $"→ {relationship.TargetTable}." +
                    $"{relationship.TargetColumn}");
            }
        }

        builder.AppendLine();

        builder.AppendLine("UNIQUE CONSTRAINTS");
        builder.AppendLine("==================");
        builder.AppendLine();

        if (uniqueConstraints.Count == 0)
        {
            builder.AppendLine("No unique constraints found.");
        }
        else
        {
            foreach (var constraint in uniqueConstraints)
            {
                builder.AppendLine(
                    $"  {constraint.TableName}." +
                    $"{constraint.ColumnName} " +
                    $"(constraint: {constraint.ConstraintName})");
            }
        }

        builder.AppendLine();

        builder.AppendLine("INDEXES");
        builder.AppendLine("=======");
        builder.AppendLine();

        foreach (var index in indexes)
        {
            builder.AppendLine(
                $"  {index.TableName}: {index.IndexName}");
        }

        builder.AppendLine();

        builder.AppendLine("BUSINESS SEMANTICS");
        builder.AppendLine("==================");
        builder.AppendLine();

        builder.AppendLine(
            "Revenue is calculated as:");
        builder.AppendLine(
            "  order_items.Quantity * order_items.UnitPrice");
        builder.AppendLine();

        builder.AppendLine(
            "Revenue calculations should include only:");
        builder.AppendLine(
            "  orders.Status = 'Completed'");
        builder.AppendLine();

        builder.AppendLine(
            "Historical order revenue must use:");
        builder.AppendLine(
            "  order_items.UnitPrice");
        builder.AppendLine();

        builder.AppendLine(
            "Do NOT use products.Price to calculate historical order revenue.");
        builder.AppendLine();

        builder.AppendLine(
            "products.Price represents the current product selling price.");
        builder.AppendLine();

        builder.AppendLine(
            "products.CostPrice represents the product cost.");
        builder.AppendLine();

        builder.AppendLine(
            "order_items.Quantity represents the quantity purchased.");
        builder.AppendLine();

        builder.AppendLine(
            "order_items.UnitPrice represents the price actually charged " +
            "for that order item.");
        builder.AppendLine();

        builder.AppendLine("SQL GENERATION RULES");
        builder.AppendLine("====================");
        builder.AppendLine();

        builder.AppendLine(
            "1. Use only tables and columns defined above.");
        builder.AppendLine(
            "2. Never invent tables or columns.");
        builder.AppendLine(
            "3. Use foreign-key relationships when joining tables.");
        builder.AppendLine(
            "4. Never invent relationships.");
        builder.AppendLine(
            "5. Do not assume similarly named columns are related.");
        builder.AppendLine(
            "6. Qualify columns with table names.");
        builder.AppendLine(
            "7. Return exactly one SELECT statement.");
        builder.AppendLine(
            "8. Never modify database data.");
        builder.AppendLine(
            "9. Never use INSERT, UPDATE, DELETE, DROP, ALTER, " +
            "CREATE, TRUNCATE, MERGE, or similar statements.");

        return builder.ToString();
    }

    public void InvalidateSchemaCache()
    {
        _memoryCache.Remove(SchemaCacheKey);
    }

    private record ColumnInfo(
        string TableName,
        string ColumnName,
        string DataType,
        bool IsNullable,
        string? DefaultValue,
        int OrdinalPosition);

    private record PrimaryKeyInfo(
        string TableName,
        string ColumnName);

    private record RelationshipInfo(
        string ConstraintName,
        string SourceTable,
        string SourceColumn,
        string TargetTable,
        string TargetColumn);

    private record UniqueConstraintInfo(
        string ConstraintName,
        string TableName,
        string ColumnName);

    private record IndexInfo(
        string TableName,
        string IndexName,
        string IndexDefinition);
}
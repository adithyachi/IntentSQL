using BizPulse.AI.POC.Models;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;

namespace BizPulse.AI.POC.Services;

public class SqlGenerationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly DatabaseSchemaService _databaseSchemaService;

    public SqlGenerationService(
        IServiceProvider serviceProvider,
        DatabaseSchemaService databaseSchemaService)
    {
        _serviceProvider = serviceProvider;
        _databaseSchemaService = databaseSchemaService;
    }

    private IAiTextGenerationService GetAiService(
    AiProvider provider)
    {
        return provider switch
        {
            AiProvider.Ollama =>
                _serviceProvider.GetRequiredService<OllamaService>(),

            AiProvider.TogetherAI =>
                _serviceProvider.GetRequiredService<TogetherAiService>(),

            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(provider),
                    provider,
                    "Unsupported AI provider.")
        };
    }

    public async Task<SqlGenerationResult> GenerateSqlAsync(
        string question,
        AiProvider provider,
        bool thinkEnabled = false,
        CancellationToken cancellationToken = default)
    {
        var schema =
            await _databaseSchemaService.GetSchemaContextAsync(
                cancellationToken);

        var prompt = BuildGenerationPrompt(
            question,
            schema);

        var aiService =
            GetAiService(provider);

        var response =
            await aiService.GenerateAsync(
                prompt,
                thinkEnabled,
                cancellationToken);

        return new SqlGenerationResult
        {
            Sql = CleanSql(response.Content),
            AiGeneration = response
        };
    }

    public async Task<SqlGenerationResult> CorrectSqlAsync(
        string question,
        IReadOnlyList<SqlAttemptViewModel> previousAttempts,
        int correctionAttempt,
        AiProvider provider,
        bool thinkEnabled = false,
        CancellationToken cancellationToken = default)
    {
        if (previousAttempts == null ||
            previousAttempts.Count == 0)
        {
            throw new ArgumentException(
                "Previous SQL attempts are required for correction.",
                nameof(previousAttempts));
        }

        var schema =
            await _databaseSchemaService.GetSchemaContextAsync(
                cancellationToken);

        var prompt = BuildCorrectionPrompt(
            question,
            schema,
            previousAttempts,
            correctionAttempt);

        var aiService =
            GetAiService(provider);

        var response =
            await aiService.GenerateAsync(
                prompt,
                thinkEnabled,
                cancellationToken);

        return new SqlGenerationResult
        {
            Sql = CleanSql(response.Content),
            AiGeneration = response
        };
    }

    public async Task<AiGenerationResult> GenerateConversationAsync(
        string question,
        AiProvider provider,
        bool thinkEnabled = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            throw new ArgumentException(
                "A conversation message is required.",
                nameof(question));
        }

        var prompt = BuildConversationPrompt(question);

        var aiService = GetAiService(provider);

        return await aiService.GenerateAsync(
            prompt,
            thinkEnabled,
            cancellationToken);
    }

    private static string BuildConversationPrompt(string question)
    {
        return $"""
        You are IntentSQL, an AI assistant for a business intelligence application.

        GENERAL CONVERSATION MODE
        ==========================

        The user is having a normal conversation with the assistant.

        Your job is to respond naturally and helpfully to the user's message.

        IMPORTANT RULES
        ==============

        - This is NOT SQL generation mode.
        - Do NOT generate SQL.
        - Do NOT generate PostgreSQL statements.
        - Do NOT execute or suggest database queries.
        - Do NOT pretend to have database results.
        - Answer the user's conversational question directly.
        - Be concise and clear.
        - If the user asks about IntentSQL, explain that IntentSQL can answer
          business-data questions using its SQL mode.
        - If the user asks a business-data question while in conversation mode,
          explain that they should switch to Strict SQL mode for a database-backed
          answer.

        USER MESSAGE
        ============

        {question}
        """;
    }

    private static string BuildGenerationPrompt(
        string question,
        string schema)
    {
        return $"""
        You are a PostgreSQL SQL generation engine.

        Your ONLY job is to convert the user's business question
        into ONE valid PostgreSQL SELECT statement.

        The database schema below is the authoritative source of truth.

        DATABASE CONTEXT
        =================

        {schema}

        SQL GENERATION RULES
        ====================

        - Use ONLY tables and columns defined in DATABASE CONTEXT.
        - Never invent tables.
        - Never invent columns.
        - Never invent relationships.
        - Follow ONLY foreign-key relationships defined in DATABASE CONTEXT.
        - Do not assume similarly named columns are related.
        - Use valid PostgreSQL syntax.
        - Qualify columns with table names.
        - Use the exact table and column names from the schema.
        - Use aggregation when the question requires totals,
          averages, counts, rankings, or similar metrics.
        - Apply the business semantics defined in DATABASE CONTEXT.
        - Return exactly ONE SELECT statement.

        IDENTIFIER RULES
        ================

        PostgreSQL identifiers in this database are case-sensitive.

        ALL table names and column names MUST use double quotes.

        Correct:

            "orders"."Id"
            "orders"."CustomerId"
            "orders"."OrderDate"
            "orders"."Status"

            "order_items"."Id"
            "order_items"."OrderId"
            "order_items"."ProductId"
            "order_items"."Quantity"
            "order_items"."UnitPrice"

            "products"."Id"
            "products"."Name"
            "products"."CategoryId"
            "products"."Price"
            "products"."CostPrice"

        Incorrect:

            orders.id
            orders.customerid
            orders.orderdate
            orders.status

            order_items.id
            order_items.orderid
            order_items.quantity
            order_items.unitprice

        SAFETY RULES
        ============

        - Return exactly ONE SELECT statement.
        - Never use INSERT.
        - Never use UPDATE.
        - Never use DELETE.
        - Never use DROP.
        - Never use ALTER.
        - Never use CREATE.
        - Never use TRUNCATE.
        - Never use MERGE.
        - Do not return explanations.
        - Do not return markdown.
        - Do not return code fences.
        - Do not return comments.

        FINAL VALIDATION
        ================

        Before returning the SQL, verify internally:

        1. Every table exists.
        2. Every column exists.
        3. Every identifier has the correct case.
        4. Every identifier is double quoted.
        5. Every JOIN follows an actual foreign-key relationship.
        6. GROUP BY is valid.
        7. Aggregate expressions are valid.
        8. ORDER BY is valid.
        9. Date filtering is valid.
        10. The SQL answers the user's exact question.

        USER QUESTION
        =============

        {question}

        Return ONLY the PostgreSQL SELECT statement.
        """;
    }

    private static string BuildCorrectionPrompt(
        string question,
        string schema,
        IReadOnlyList<SqlAttemptViewModel> previousAttempts,
        int correctionAttempt)
    {
        var attemptHistory =
            BuildAttemptHistory(previousAttempts);

        var correctionStrategy =
            correctionAttempt == 1
                ? """
                  This is the FIRST correction.

                  Analyze the failed SQL and PostgreSQL error carefully.

                  Correct the SQL based on the actual database schema.

                  Do not blindly copy the failed SQL.
                  """
                : """
                  This is a SUBSEQUENT correction.

                  Multiple previous attempts have already failed.

                  You MUST NOT repeat any previously failed SQL.

                  You MUST NOT repeat the same invalid SQL strategy.

                  Re-evaluate the original question from scratch.

                  Re-check the database schema.

                  Re-check ALL previous PostgreSQL errors.

                  Use the complete attempt history below to understand
                  what has already failed.

                  If a previous SQL used an invalid identifier,
                  JOIN, GROUP BY, ORDER BY, date expression, aggregate,
                  or other construct, do not reproduce that construct
                  in the same invalid form.

                  Produce a genuinely corrected SQL statement.
                  """;

        return $"""
        You are a PostgreSQL SQL correction engine.

        A previously generated SQL statement was executed against
        the REAL PostgreSQL database and failed.

        Your task is to generate a NEW valid PostgreSQL SELECT statement
        that correctly answers the ORIGINAL USER QUESTION.

        IMPORTANT
        =========

        The DATABASE CONTEXT is authoritative.

        The ORIGINAL USER QUESTION is authoritative.

        PostgreSQL errors are authoritative evidence of what failed.

        Previously generated SQL is NOT authoritative.

        {correctionStrategy}

        DATABASE CONTEXT
        =================

        {schema}

        ORIGINAL USER QUESTION
        ======================

        {question}

        PREVIOUS ATTEMPT HISTORY
        ========================

        {attemptHistory}

        CURRENT CORRECTION ATTEMPT
        ==========================

        {correctionAttempt}

        IDENTIFIER RULES
        ================

        PostgreSQL identifiers in this database are case-sensitive.

        ALL table names and column names MUST use double quotes.

        Correct:

            "orders"."Id"
            "orders"."CustomerId"
            "orders"."OrderDate"
            "orders"."Status"

            "order_items"."Id"
            "order_items"."OrderId"
            "order_items"."ProductId"
            "order_items"."Quantity"
            "order_items"."UnitPrice"

            "products"."Id"
            "products"."Name"
            "products"."CategoryId"
            "products"."Price"
            "products"."CostPrice"

        Incorrect:

            orders.id
            orders.customerid
            orders.orderdate
            orders.status

            order_items.id
            order_items.orderid
            order_items.quantity
            order_items.unitprice

        CORRECTION PROCESS
        ==================

        Before generating the SQL, reason through these steps internally:

        1. Identify exactly what PostgreSQL rejected.

        2. Locate the relevant tables and columns in DATABASE CONTEXT.

        3. Verify the exact identifier names and case.

        4. Verify every JOIN against the foreign-key relationships.

        5. Verify every GROUP BY expression.

        6. Verify every aggregate expression.

        7. Verify every ORDER BY expression.

        8. Verify every date filter.

        9. Verify that the SQL answers the ORIGINAL USER QUESTION.

        10. Verify that the new SQL does not repeat a previously
            failed SQL or invalid strategy.

        IMPORTANT

        Do not make a superficial change merely to avoid the
        PostgreSQL error.

        Reconstruct the SQL logic when necessary.

        SQL SAFETY RULES
        ================

        - Return exactly ONE SELECT statement.
        - Use only tables defined in DATABASE CONTEXT.
        - Use only columns defined in DATABASE CONTEXT.
        - Use only relationships defined in DATABASE CONTEXT.
        - Never invent tables.
        - Never invent columns.
        - Never invent relationships.
        - Use valid PostgreSQL syntax.
        - Use double quotes around table and column identifiers.
        - Never use INSERT.
        - Never use UPDATE.
        - Never use DELETE.
        - Never use DROP.
        - Never use ALTER.
        - Never use CREATE.
        - Never use TRUNCATE.
        - Never use MERGE.
        - Do not return explanations.
        - Do not return markdown.
        - Do not return code fences.
        - Do not return comments.

        FINAL VALIDATION
        ================

        Before returning the answer, verify internally:

        1. Every table exists.
        2. Every column exists.
        3. Every identifier has the correct case.
        4. Every identifier is double quoted.
        5. Every JOIN is valid.
        6. GROUP BY is valid.
        7. ORDER BY is valid.
        8. All aggregates are valid.
        9. Date filtering is valid.
        10. The SQL is valid PostgreSQL.
        11. The SQL answers the ORIGINAL USER QUESTION.
        12. The SQL does not repeat any previously failed SQL.
        13. The SQL does not repeat a previously failed approach
            when that approach caused the PostgreSQL error.

        Return ONLY the NEW corrected PostgreSQL SELECT statement.
        """;
    }

    private static string BuildAttemptHistory(
        IReadOnlyList<SqlAttemptViewModel> attempts)
    {
        var builder = new StringBuilder();

        foreach (var attempt in attempts)
        {
            builder.AppendLine(
                $"ATTEMPT {attempt.AttemptNumber}");

            builder.AppendLine(
                $"STATUS: {(attempt.Success ? "SUCCESS" : "FAILED")}");

            builder.AppendLine();

            builder.AppendLine("SQL:");
            builder.AppendLine(attempt.Sql);

            if (!string.IsNullOrWhiteSpace(attempt.Error))
            {
                builder.AppendLine();
                builder.AppendLine("POSTGRESQL ERROR:");
                builder.AppendLine(attempt.Error);
            }

            builder.AppendLine();
            builder.AppendLine("--------------------");
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string CleanSql(string response)
    {
        var sql = response.Trim();

        sql = Regex.Replace(
            sql,
            @"^```(?:sql)?\s*|\s*```$",
            "",
            RegexOptions.IgnoreCase);

        return sql.Trim();
    }
}
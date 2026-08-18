using BizPulse.AI.POC.Data;
using BizPulse.AI.POC.Models;
using BizPulse.AI.POC.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace BizPulse.AI.POC.Controllers;

public class AiTestController : Controller
{
    private const int MaxExecutionAttempts = 3;

    private readonly SqlGenerationService _sqlGenerationService;
    private readonly SqlExecutionService _sqlExecutionService;
    private readonly AppDbContext _dbContext;
    private readonly IConfiguration _configuration;

    public AiTestController(
        SqlGenerationService sqlGenerationService,
        SqlExecutionService sqlExecutionService,
        AppDbContext dbContext,
        IConfiguration configuration)
    {
        _sqlGenerationService = sqlGenerationService;
        _sqlExecutionService = sqlExecutionService;
        _dbContext = dbContext;
        _configuration = configuration;
    }

    private bool IsAiEnabled()
    {
        return _configuration.GetValue<bool>("AI:Enabled");
    }

    private static bool IsStrictSqlMode(string mode)
    {
        return string.Equals(
            mode,
            "StrictSql",
            StringComparison.OrdinalIgnoreCase);
    }

    [HttpGet]
    public IActionResult Index()
    {
        ViewBag.AiEnabled = IsAiEnabled();
        return View();
    }

    [HttpGet]
    public IActionResult Ask()
    {
        ViewBag.AiEnabled = IsAiEnabled();
        return View("Index");
    }

    private record SqlExecutionResult(
        string Sql,
        List<Dictionary<string, object?>> Results,
        List<SqlAttemptViewModel> Attempts,
        List<AiGenerationResult> Generations,
        string? Error);

    private async Task<SqlExecutionResult> ExecuteWithCorrectionAsync(
        string question,
        string initialSql,
        SqlGenerationResult initialGeneration,
        AiProvider provider,
        bool thinkEnabled,
        CancellationToken cancellationToken)
    {
        var attempts =
            new List<SqlAttemptViewModel>();

        var generations =
            new List<AiGenerationResult>
            {
        initialGeneration.AiGeneration
            };

        var currentSql =
            initialSql;

        var currentGenerationTimeSeconds =
            initialGeneration.AiGeneration.ResponseTimeMs / 1000.0;

        for (var attemptNumber = 1;
             attemptNumber <= MaxExecutionAttempts;
             attemptNumber++)
        {
            // ---------------------------------------------------------
            // Prevent Qwen from returning SQL that already failed.
            // ---------------------------------------------------------

            if (HasPreviouslyFailed(
                    currentSql,
                    attempts))
            {
                var duplicateError =
                    "The generated SQL is identical to a previously " +
                    "failed SQL statement. It was not executed.";

                attempts.Add(
                    new SqlAttemptViewModel
                    {
                        AttemptNumber = attemptNumber,
                        Sql = currentSql,
                        Success = false,
                        Error = duplicateError,
                        GenerationTimeSeconds =
                            currentGenerationTimeSeconds
                    });

                if (attemptNumber == MaxExecutionAttempts)
                {
                    return new SqlExecutionResult(
                        currentSql,
                        new List<Dictionary<string, object?>>(),
                        attempts,
                        generations,
                        "SQL generation repeatedly produced a " +
                        "previously failed SQL statement.");
                }

                var correctionResult =
                    await _sqlGenerationService.CorrectSqlAsync(
                        question,
                        attempts,
                        attemptNumber,
                        provider,
                        thinkEnabled,
                        cancellationToken);

                generations.Add(
                    correctionResult.AiGeneration);

                currentSql =
                    correctionResult.Sql;

                currentGenerationTimeSeconds =
                    correctionResult.AiGeneration.ResponseTimeMs / 1000.0;

                continue;
            }

            try
            {
                var results =
                    await _sqlExecutionService.ExecuteAsync(
                        currentSql,
                        cancellationToken);

                attempts.Add(
                    new SqlAttemptViewModel
                    {
                        AttemptNumber = attemptNumber,
                        Sql = currentSql,
                        Success = true,
                        Error = null,
                        GenerationTimeSeconds =
                            currentGenerationTimeSeconds
                    });

                return new SqlExecutionResult(
                    currentSql,
                    results,
                    attempts,
                    generations,
                    null);
            }
            catch (SqlValidationException ex)
            {
                // Safety/validation failures must never be sent
                // back to Qwen for correction.

                attempts.Add(
                    new SqlAttemptViewModel
                    {
                        AttemptNumber = attemptNumber,
                        Sql = currentSql,
                        Success = false,
                        Error = ex.Message,
                        GenerationTimeSeconds =
                            currentGenerationTimeSeconds
                    });

                return new SqlExecutionResult(
                    currentSql,
                    new List<Dictionary<string, object?>>(),
                    attempts,
                    generations,
                    ex.Message);
            }
            catch (Exception ex)
            {
                attempts.Add(
                    new SqlAttemptViewModel
                    {
                        AttemptNumber = attemptNumber,
                        Sql = currentSql,
                        Success = false,
                        Error = ex.Message,
                        GenerationTimeSeconds =
                            currentGenerationTimeSeconds
                    });

                // -----------------------------------------------------
                // No more attempts.
                // -----------------------------------------------------

                if (attemptNumber == MaxExecutionAttempts)
                {
                    return new SqlExecutionResult(
                        currentSql,
                        new List<Dictionary<string, object?>>(),
                        attempts,
                        generations,
                        "SQL execution failed after the maximum " +
                        "number of correction attempts.");
                }

                // -----------------------------------------------------
                // Generate corrected SQL using complete history.
                // -----------------------------------------------------

                var correctionResult =
                    await _sqlGenerationService.CorrectSqlAsync(
                        question,
                        attempts,
                        attemptNumber,
                        provider,
                        thinkEnabled,
                        cancellationToken);

                generations.Add(
                    correctionResult.AiGeneration);

                currentSql =
                    correctionResult.Sql;

                currentGenerationTimeSeconds =
                    correctionResult.AiGeneration.ResponseTimeMs / 1000.0;
            }
        }

        return new SqlExecutionResult(
            currentSql,
            new List<Dictionary<string, object?>>(),
            attempts,
            generations,
            "SQL execution failed.");
    }

    private static bool HasPreviouslyFailed(
        string sql,
        IReadOnlyList<SqlAttemptViewModel> attempts)
    {
        var normalizedSql =
            NormalizeSql(sql);

        return attempts.Any(x =>
            !x.Success &&
            NormalizeSql(x.Sql) == normalizedSql);
    }

    private static string NormalizeSql(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return string.Empty;
        }

        return string.Join(
                " ",
                sql
                    .Trim()
                    .Split(
                        (char[]?)null,
                        StringSplitOptions.RemoveEmptyEntries))
            .TrimEnd(';')
            .Trim()
            .ToLowerInvariant();
    }

    [HttpPost]
    public async Task<IActionResult> Ask(
        string prompt,
        AiProvider provider,
        bool think,
        string mode = "StrictSql",
        CancellationToken cancellationToken = default)
    {
        if (!IsAiEnabled())
        {
            ViewBag.AiEnabled = false;
            ViewBag.Prompt = prompt;
            ViewBag.Error =
                "AI querying is currently unavailable.";

            return View("Index");
        }

        ViewBag.AiEnabled = true;
        ViewBag.Mode = mode;
        ViewBag.ThinkEnabled = think;

        if (string.IsNullOrWhiteSpace(prompt))
        {
            return BadRequest("Please enter a question.");
        }

        var totalStopwatch =
            System.Diagnostics.Stopwatch.StartNew();

        // ---------------------------------------------------------
        // General Conversation mode
        // ---------------------------------------------------------
        // This branch intentionally does NOT generate SQL and does NOT
        // execute anything against PostgreSQL.
        if (!IsStrictSqlMode(mode))
        {
            try
            {
                var conversationResult =
                    await _sqlGenerationService.GenerateConversationAsync(
                        prompt,
                        provider,
                        think,
                        cancellationToken);

                totalStopwatch.Stop();

                ViewBag.Prompt = prompt;
                ViewBag.Mode = "Conversation";
                ViewBag.ConversationResponse = conversationResult.Content;

                ViewBag.InputTokens = conversationResult.InputTokens;
                ViewBag.OutputTokens = conversationResult.OutputTokens;
                ViewBag.TotalTokens = conversationResult.TotalTokens;
                ViewBag.ReasoningTokens = conversationResult.ReasoningTokens;
                ViewBag.Reasoning = conversationResult.Reasoning;
                ViewBag.Provider = conversationResult.Provider;
                ViewBag.Model = conversationResult.Model;
                ViewBag.TotalProcessingTimeSeconds =
                    totalStopwatch.Elapsed.TotalSeconds;

                var historyError = await TrySaveConversationExecutionHistoryAsync(
                    prompt,
                    think,
                    conversationResult,
                    totalStopwatch.ElapsedMilliseconds,
                    cancellationToken);

                if (!string.IsNullOrWhiteSpace(historyError))
                {
                    ViewBag.PersistenceError = historyError;
                }

                // Conversation responses are intentionally not sent through
                // the SQL execution/correction pipeline.
                return View("Index");
            }
            catch (Exception ex)
            {
                totalStopwatch.Stop();

                ViewBag.Prompt = prompt;
                ViewBag.Mode = "Conversation";
                ViewBag.Error = GetExceptionMessage(ex);
                ViewBag.TotalProcessingTimeSeconds =
                    totalStopwatch.Elapsed.TotalSeconds;

                var historyError = await TrySaveFailedExecutionHistoryAsync(
                    prompt,
                    provider,
                    think,
                    "Conversation",
                    GetExceptionMessage(ex),
                    totalStopwatch.ElapsedMilliseconds,
                    cancellationToken);

                if (!string.IsNullOrWhiteSpace(historyError))
                {
                    ViewBag.PersistenceError = historyError;
                }

                return View("Index");
            }
        }

        SqlExecutionResult? executionResult = null;

        try
        {
            // ---------------------------------------------------------
            // Generate initial SQL
            // ---------------------------------------------------------

            var generationResult =
                await _sqlGenerationService.GenerateSqlAsync(
                    prompt,
                    provider,
                    think,
                    cancellationToken);

            // ---------------------------------------------------------
            // Execute SQL + correction loop
            // ---------------------------------------------------------

            executionResult =
                await ExecuteWithCorrectionAsync(
                    prompt,
                    generationResult.Sql,
                    generationResult,
                    provider,
                    think,
                    cancellationToken);

            totalStopwatch.Stop();

            // ---------------------------------------------------------
            // Persist AI execution history
            // ---------------------------------------------------------

            var historyError = await TrySaveExecutionHistoryAsync(
                prompt,
                provider,
                think,
                executionResult,
                totalStopwatch.ElapsedMilliseconds,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(historyError))
            {
                ViewBag.PersistenceError = historyError;
            }

            // ---------------------------------------------------------
            // Populate UI
            // ---------------------------------------------------------

            ViewBag.Prompt =
                prompt;

            ViewBag.Sql =
                executionResult.Sql;

            ViewBag.Attempts =
                executionResult.Attempts;

            ViewBag.TotalGenerationTimeSeconds =
                executionResult.Attempts.Sum(
                    x => x.GenerationTimeSeconds);

            ViewBag.TotalProcessingTimeSeconds =
                totalStopwatch.Elapsed.TotalSeconds;

            // Token information for the current UI request.

            ViewBag.InputTokens =
                executionResult.Generations.Sum(
                    x => x.InputTokens);

            ViewBag.OutputTokens =
                executionResult.Generations.Sum(
                    x => x.OutputTokens);

            ViewBag.TotalTokens =
                executionResult.Generations.Sum(
                    x => x.TotalTokens);

            ViewBag.ReasoningTokens =
                executionResult.Generations.Sum(
                    x => x.ReasoningTokens);

            ViewBag.ThinkEnabled = think;

            ViewBag.Reasoning =
                BuildCombinedReasoning(executionResult.Generations);

            ViewBag.Provider =
                provider.ToString();

            ViewBag.Model =
                executionResult.Generations
                    .LastOrDefault()
                    ?.Model;

            if (string.IsNullOrWhiteSpace(
                executionResult.Error))
            {
                ViewBag.Results =
                    executionResult.Results;
            }
            else
            {
                ViewBag.Results =
                    null;

                ViewBag.Error =
                    executionResult.Error;
            }

            return View("Index");
        }
        catch (Exception ex)
        {
            totalStopwatch.Stop();

            // ---------------------------------------------------------
            // Even if AI generation itself fails, record the question.
            // ---------------------------------------------------------

            ViewBag.Prompt =
                prompt;

            ViewBag.Mode = mode;
            ViewBag.ThinkEnabled = think;
            ViewBag.Error =
                GetExceptionMessage(ex);

            var historyError = await TrySaveFailedExecutionHistoryAsync(
                prompt,
                provider,
                think,
                "Strict SQL",
                GetExceptionMessage(ex),
                totalStopwatch.ElapsedMilliseconds,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(historyError))
            {
                ViewBag.PersistenceError = historyError;
            }

            ViewBag.TotalProcessingTimeSeconds =
                totalStopwatch.Elapsed.TotalSeconds;

            return View("Index");
        }
    }

    private async Task<string?> TrySaveExecutionHistoryAsync(
        string question,
        AiProvider provider,
        bool thinkEnabled,
        SqlExecutionResult executionResult,
        long totalProcessingTimeMs,
        CancellationToken cancellationToken)
    {
        try
        {
            var generations = executionResult.Generations;
            var lastGeneration = generations.LastOrDefault();

            var execution = new AiAgentExecution
            {
                Question = question,
                Mode = "Strict SQL",
                Response = BuildSqlResponse(executionResult.Results),
                ThinkEnabled = thinkEnabled,
                Reasoning = BuildCombinedReasoning(generations),
                Provider = provider.ToString(),
                Model = lastGeneration?.Model ?? string.Empty,
                InputTokens = generations.Sum(x => x.InputTokens),
                OutputTokens = generations.Sum(x => x.OutputTokens),
                TotalTokens = generations.Sum(x => x.TotalTokens),
                ReasoningTokens = generations.Sum(x => x.ReasoningTokens),
                ResponseTimeMs = generations.Sum(x => x.ResponseTimeMs),
                TotalProcessingTimeMs = totalProcessingTimeMs,
                Success = string.IsNullOrWhiteSpace(executionResult.Error),
                FinalSql = executionResult.Sql,
                Error = executionResult.Error,
                CreatedAt = DateTime.UtcNow
            };

            foreach (var attempt in executionResult.Attempts)
            {
                execution.Attempts.Add(new AiAgentAttempt
                {
                    AttemptNumber = attempt.AttemptNumber,
                    Sql = attempt.Sql,
                    Success = attempt.Success,
                    Error = attempt.Error,
                    GenerationTimeMs = (long)(attempt.GenerationTimeSeconds * 1000),
                    CreatedAt = DateTime.UtcNow
                });
            }

            _dbContext.AiAgentExecutions.Add(execution);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return null;
        }
        catch (Exception ex)
        {
            _dbContext.ChangeTracker.Clear();
            return $"Execution completed, but history could not be saved: {GetExceptionMessage(ex)}";
        }
    }

    private async Task<string?> TrySaveConversationExecutionHistoryAsync(
        string question,
        bool thinkEnabled,
        AiGenerationResult result,
        long totalProcessingTimeMs,
        CancellationToken cancellationToken)
    {
        try
        {
            var execution = new AiAgentExecution
            {
                Question = question,
                Mode = "Conversation",
                Response = result.Content,
                ThinkEnabled = thinkEnabled,
                Reasoning = thinkEnabled ? result.Reasoning : null,
                Provider = result.Provider,
                Model = result.Model,
                InputTokens = result.InputTokens,
                OutputTokens = result.OutputTokens,
                TotalTokens = result.TotalTokens,
                ReasoningTokens = thinkEnabled ? result.ReasoningTokens : 0,
                ResponseTimeMs = result.ResponseTimeMs,
                TotalProcessingTimeMs = totalProcessingTimeMs,
                Success = true,
                FinalSql = null,
                Error = null,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.AiAgentExecutions.Add(execution);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return null;
        }
        catch (Exception ex)
        {
            _dbContext.ChangeTracker.Clear();
            return GetExceptionMessage(ex);
        }
    }

    private async Task<string?> TrySaveFailedExecutionHistoryAsync(
        string question,
        AiProvider provider,
        bool thinkEnabled,
        string mode,
        string error,
        long totalProcessingTimeMs,
        CancellationToken cancellationToken)
    {
        try
        {
            var execution = new AiAgentExecution
            {
                Question = question,
                Mode = mode,
                Response = null,
                ThinkEnabled = thinkEnabled,
                Reasoning = null,
                Provider = provider.ToString(),
                Model = string.Empty,
                InputTokens = 0,
                OutputTokens = 0,
                TotalTokens = 0,
                ReasoningTokens = 0,
                ResponseTimeMs = 0,
                TotalProcessingTimeMs = totalProcessingTimeMs,
                Success = false,
                FinalSql = null,
                Error = error,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.AiAgentExecutions.Add(execution);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return null;
        }
        catch (Exception ex)
        {
            _dbContext.ChangeTracker.Clear();
            return $"Failed execution was not recorded in history: {GetExceptionMessage(ex)}";
        }
    }

    private static string GetExceptionMessage(Exception ex)
    {
        var messages = new List<string>();
        var current = ex;

        while (current != null)
        {
            if (!string.IsNullOrWhiteSpace(current.Message) &&
                !messages.Contains(current.Message, StringComparer.Ordinal))
            {
                messages.Add(current.Message);
            }

            current = current.InnerException!;
        }

        return string.Join(" | ", messages);
    }

    private static string? BuildSqlResponse(
        IReadOnlyList<Dictionary<string, object?>> results)
    {
        if (results == null || results.Count == 0)
        {
            return "The query returned no results.";
        }

        return JsonSerializer.Serialize(
            results,
            new JsonSerializerOptions { WriteIndented = true });
    }

    private static string? BuildCombinedReasoning(
        IReadOnlyList<AiGenerationResult> generations)
    {
        var reasoningBlocks =
            generations
                .Where(x => !string.IsNullOrWhiteSpace(x.Reasoning))
                .Select((x, index) =>
                    $"REASONING - GENERATION {index + 1}\n\n{x.Reasoning!.Trim()}")
                .ToList();

        return reasoningBlocks.Count == 0
            ? null
            : string.Join("\n\n==============================\n\n", reasoningBlocks);
    }


}
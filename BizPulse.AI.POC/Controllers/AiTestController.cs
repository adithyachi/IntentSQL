using BizPulse.AI.POC.Data;
using BizPulse.AI.POC.Models;
using BizPulse.AI.POC.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
        CancellationToken cancellationToken)
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
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return BadRequest("Please enter a question.");
        }

        var totalStopwatch =
            System.Diagnostics.Stopwatch.StartNew();

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
                    cancellationToken);

            totalStopwatch.Stop();

            // ---------------------------------------------------------
            // Persist AI execution history
            // ---------------------------------------------------------

            await SaveExecutionHistoryAsync(
                prompt,
                provider,
                executionResult,
                totalStopwatch.ElapsedMilliseconds,
                cancellationToken);

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

            await SaveFailedExecutionHistoryAsync(
                prompt,
                provider,
                ex.Message,
                totalStopwatch.ElapsedMilliseconds,
                cancellationToken);

            ViewBag.Prompt =
                prompt;

            ViewBag.Error =
                ex.Message;

            ViewBag.TotalProcessingTimeSeconds =
                totalStopwatch.Elapsed.TotalSeconds;

            return View("Index");
        }
    }

    private async Task SaveExecutionHistoryAsync(
        string question,
        AiProvider provider,
        SqlExecutionResult executionResult,
        long totalProcessingTimeMs,
        CancellationToken cancellationToken)
    {
        var generations =
            executionResult.Generations;

        var lastGeneration =
            generations.LastOrDefault();

        var execution =
            new AiAgentExecution
            {
                Question = question,

                Provider =
                    provider.ToString(),

                Model =
                    lastGeneration?.Model ?? string.Empty,

                InputTokens =
                    generations.Sum(
                        x => x.InputTokens),

                OutputTokens =
                    generations.Sum(
                        x => x.OutputTokens),

                TotalTokens =
                    generations.Sum(
                        x => x.TotalTokens),

                ReasoningTokens =
                    generations.Sum(
                        x => x.ReasoningTokens),

                ResponseTimeMs =
                    generations.Sum(
                        x => x.ResponseTimeMs),

                TotalProcessingTimeMs =
                    totalProcessingTimeMs,

                Success =
                    string.IsNullOrWhiteSpace(
                        executionResult.Error),

                FinalSql =
                    executionResult.Sql,

                Error =
                    executionResult.Error,

                CreatedAt =
                    DateTime.UtcNow
            };

        foreach (var attempt in executionResult.Attempts)
        {
            execution.Attempts.Add(
                new AiAgentAttempt
                {
                    AttemptNumber =
                        attempt.AttemptNumber,

                    Sql =
                        attempt.Sql,

                    Success =
                        attempt.Success,

                    Error =
                        attempt.Error,

                    GenerationTimeMs =
                        (long)(
                            attempt.GenerationTimeSeconds *
                            1000),

                    CreatedAt =
                        DateTime.UtcNow
                });
        }

        _dbContext.AiAgentExecutions.Add(
            execution);

        await _dbContext.SaveChangesAsync(
            cancellationToken);
    }

    private async Task SaveFailedExecutionHistoryAsync(
        string question,
        AiProvider provider,
        string error,
        long totalProcessingTimeMs,
        CancellationToken cancellationToken)
    {
        var execution =
            new AiAgentExecution
            {
                Question =
                    question,

                Provider =
                    provider.ToString(),

                Model =
                    string.Empty,

                InputTokens =
                    0,

                OutputTokens =
                    0,

                TotalTokens =
                    0,

                ReasoningTokens =
                    0,

                ResponseTimeMs =
                    0,

                TotalProcessingTimeMs =
                    totalProcessingTimeMs,

                Success =
                    false,

                FinalSql =
                    null,

                Error =
                    error,

                CreatedAt =
                    DateTime.UtcNow
            };

        _dbContext.AiAgentExecutions.Add(
            execution);

        await _dbContext.SaveChangesAsync(
            cancellationToken);
    }
}
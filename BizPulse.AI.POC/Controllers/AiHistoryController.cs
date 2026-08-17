using BizPulse.AI.POC.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BizPulse.AI.POC.Controllers;

public class AiHistoryController : Controller
{
    private readonly AppDbContext _dbContext;

    public AiHistoryController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        CancellationToken cancellationToken)
    {
        var executions =
            await _dbContext.AiAgentExecutions
                .AsNoTracking()
                .Include(x => x.Attempts)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync(cancellationToken);

        return View(executions);
    }

    [HttpGet]
    public async Task<IActionResult> Details(
        long id,
        CancellationToken cancellationToken)
    {
        var execution =
            await _dbContext.AiAgentExecutions
                .AsNoTracking()
                .Include(x => x.Attempts)
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    cancellationToken);

        if (execution == null)
        {
            return NotFound();
        }

        return View(execution);
    }
}
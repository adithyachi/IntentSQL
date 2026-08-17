using BizPulse.AI.POC.Models;
using BizPulse.AI.POC.Services;
using Microsoft.AspNetCore.Mvc;

namespace BizPulse.AI.POC.Controllers;

public class DataExplorerController : Controller
{
    private readonly DataExplorerService _dataExplorerService;

    public DataExplorerController(
        DataExplorerService dataExplorerService)
    {
        _dataExplorerService =
            dataExplorerService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? table = null,
        int page = 1,
        int pageSize = 25,
        string? sortColumn = null,
        string? sortDirection = null,
        Dictionary<string, string>? filters = null,
        CancellationToken cancellationToken = default)
    {
        var selectedTable =
            string.IsNullOrWhiteSpace(table)
                ? "customers"
                : table;

        var result =
            await _dataExplorerService.GetDataAsync(
                selectedTable,
                page,
                pageSize,
                sortColumn,
                sortDirection,
                filters,
                cancellationToken);

        var model =
            new DataExplorerViewModel
            {
                SelectedTable = result.Table,

                AvailableTables =
                    _dataExplorerService
                        .GetTables()
                        .ToList(),

                Columns = result.Columns,

                Rows = result.Rows,

                TotalRows = result.TotalRows,

                Page = result.Page,

                PageSize = result.PageSize,

                SortColumn = result.SortColumn,

                SortDirection = result.SortDirection,

                Filters = filters ??
                          new Dictionary<string, string>()
            };

        return View(model);
    }
}
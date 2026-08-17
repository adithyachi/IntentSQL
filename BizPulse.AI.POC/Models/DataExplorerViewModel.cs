namespace BizPulse.AI.POC.Models;

public class DataExplorerViewModel
{
    public string SelectedTable { get; set; } = "customers";

    public List<string> AvailableTables { get; set; } = [];

    public List<string> Columns { get; set; } = [];

    public List<Dictionary<string, object?>> Rows { get; set; } = [];

    public int TotalRows { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 25;

    public string? SortColumn { get; set; }

    public string SortDirection { get; set; } = "asc";

    public Dictionary<string, string> Filters { get; set; } = [];

    public int TotalPages =>
        PageSize <= 0
            ? 0
            : (int)Math.Ceiling(
                TotalRows / (double)PageSize);

    public int StartRow =>
        TotalRows == 0
            ? 0
            : ((Page - 1) * PageSize) + 1;

    public int EndRow =>
        Math.Min(
            Page * PageSize,
            TotalRows);
}
using EbayClone.Application.Admin.Disputes.Queries.Common;
using EbayClone.Application.Admin.Disputes.Queries.GetDisputeDocket;
using EbayClone.Application.Admin.Disputes.Queries.GetDisputeStatistics;
using EbayClone.Application.Common.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EbayClone.Web.Pages.Admin.Disputes;

public class IndexModel : PageModel
{
    private readonly ISender _mediator;

    public IndexModel(ISender mediator)
    {
        _mediator = mediator;
    }

    public PaginatedList<DisputeDto> Disputes { get; set; } = null!;
    public DisputeStatisticsDto? Statistics { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Priority { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Type { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool OnlyMyDisputes { get; set; }

    public async Task OnGetAsync()
    {
        // Get statistics
        Statistics = await _mediator.Send(new GetDisputeStatisticsQuery());

        // Get disputes list
        var query = new GetDisputeDocketQuery
        {
            PageNumber = PageNumber,
            PageSize = 10,
            Status = Status,
            Priority = Priority,
            Type = Type,
            SearchTerm = SearchTerm,
            OnlyMyDisputes = OnlyMyDisputes,
            SortBy = "Deadline",
            Descending = false
        };

        Disputes = await _mediator.Send(query);
    }
}

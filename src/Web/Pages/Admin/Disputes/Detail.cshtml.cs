using EbayClone.Application.Admin.Disputes.Commands.AssignDispute;
using EbayClone.Application.Admin.Disputes.Queries.Common;
using EbayClone.Application.Admin.Disputes.Queries.GetDisputeDetail;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EbayClone.Web.Pages.Admin.Disputes;

public class DetailModel : PageModel
{
    private readonly ISender _mediator;

    public DetailModel(ISender mediator)
    {
        _mediator = mediator;
    }

    public DisputeDetailDto? Dispute { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        try
        {
            Dispute = await _mediator.Send(new GetDisputeDetailQuery(id));
            return Page();
        }
        catch
        {
            return NotFound();
        }
    }

    public async Task<IActionResult> OnPostAssignAsync(int disputeId)
    {
        try
        {
            await _mediator.Send(new AssignDisputeCommand(disputeId));
            TempData["SuccessMessage"] = "Dispute assigned to you successfully!";
            return RedirectToPage("./Detail", new { id = disputeId });
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Failed to assign dispute: {ex.Message}";
            return RedirectToPage("./Detail", new { id = disputeId });
        }
    }
}

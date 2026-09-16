using EbayClone.Application.Admin.Disputes.Commands.ResolveDispute;
using EbayClone.Application.Admin.Disputes.Queries.Common;
using EbayClone.Application.Admin.Disputes.Queries.GetDisputeDetail;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace EbayClone.Web.Pages.Admin.Disputes;

public class ResolveModel : PageModel
{
    private readonly ISender _mediator;

    public ResolveModel(ISender mediator)
    {
        _mediator = mediator;
    }

    public DisputeDetailDto? Dispute { get; set; }

    [BindProperty]
    public ResolveInputModel Input { get; set; } = new();

    public class ResolveInputModel
    {
        public int DisputeId { get; set; }
        
        [Required]
        public string Winner { get; set; } = string.Empty;
        
        public decimal? RefundAmount { get; set; }
        
        [Required]
        [MinLength(50)]
        [MaxLength(2000)]
        public string AdminNotes { get; set; } = string.Empty;
        
        public bool RequireReturn { get; set; }
        
        public bool AddSellerViolation { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        try
        {
            Dispute = await _mediator.Send(new GetDisputeDetailQuery(id));
            Input.DisputeId = id;
            return Page();
        }
        catch
        {
            return NotFound();
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            Dispute = await _mediator.Send(new GetDisputeDetailQuery(Input.DisputeId));
            return Page();
        }

        try
        {
            var command = new ResolveDisputeCommand
            {
                DisputeId = Input.DisputeId,
                Winner = Input.Winner,
                RefundAmount = Input.Winner == "Split" ? Input.RefundAmount : (Input.Winner == "Buyer" ? Dispute?.Amount : 0),
                AdminNotes = Input.AdminNotes,
                RequireReturn = Input.RequireReturn,
                AddSellerViolation = Input.AddSellerViolation,
                SendNotifications = true
            };

            await _mediator.Send(command);

            TempData["SuccessMessage"] = $"Dispute {Dispute?.CaseId} resolved successfully!";
            return RedirectToPage("./Detail", new { id = Input.DisputeId });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, $"Failed to resolve dispute: {ex.Message}");
            Dispute = await _mediator.Send(new GetDisputeDetailQuery(Input.DisputeId));
            return Page();
        }
    }
}

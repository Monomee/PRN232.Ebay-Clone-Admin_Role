using System;
using System.Collections.Generic;
using System.Text;

namespace EbayClone.Application.ReturRequests.Commands.RejectReturnRequest;
public class RejectReturnRequestCommandValidator
    : AbstractValidator<RejectReturnRequestCommand>
{
    public RejectReturnRequestCommandValidator()
    {
        RuleFor(x => x.ReturnRequestId)
            .GreaterThan(0).WithMessage("ReturnRequestId phải lớn hơn 0.");

        RuleFor(x => x.AdminNote)
            .NotEmpty().WithMessage("Admin phải nhập lý do từ chối.")
            .MaximumLength(500);
    }
}

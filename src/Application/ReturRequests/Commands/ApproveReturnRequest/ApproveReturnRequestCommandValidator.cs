using System;
using System.Collections.Generic;
using System.Text;

namespace EbayClone.Application.ReturRequests.Commands.ApproveReturnRequest;
public class ApproveReturnRequestCommandValidator
    : AbstractValidator<ApproveReturnRequestCommand>
{
    public ApproveReturnRequestCommandValidator()
    {
        RuleFor(x => x.ReturnRequestId)
            .GreaterThan(0).WithMessage("ReturnRequestId phải lớn hơn 0.");
    }
}

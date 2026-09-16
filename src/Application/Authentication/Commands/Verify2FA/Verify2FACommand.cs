using System;
using System.Collections.Generic;
using System.Text;

namespace EbayClone.Application.Authentication.Commands.Verify2FA;
public record Verify2FACommand(int UserId, string Code)
    : IRequest<Verify2FAResponse>;

using System;
using System.Collections.Generic;
using System.Text;

namespace EbayClone.Application.Authentication.Commands.Verify2FASetup;
public record Verify2FASetupCommand(int UserId, string Code) : IRequest<bool>;

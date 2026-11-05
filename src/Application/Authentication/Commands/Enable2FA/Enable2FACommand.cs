using System;
using System.Collections.Generic;
using System.Text;

namespace EbayClone.Application.Authentication.Commands.Enable2FA;
/// Returns the QR code, or null when 2FA is already active (its secret must not be replaced silently).
public record Enable2FACommand(string Email) : IRequest<string?>;

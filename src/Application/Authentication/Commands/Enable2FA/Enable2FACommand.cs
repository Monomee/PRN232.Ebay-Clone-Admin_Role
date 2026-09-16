using System;
using System.Collections.Generic;
using System.Text;

namespace EbayClone.Application.Authentication.Commands.Enable2FA;
public record Enable2FACommand(string Email) : IRequest<string>;

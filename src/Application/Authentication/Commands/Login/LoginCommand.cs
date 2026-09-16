using System;
using System.Collections.Generic;
using System.Text;

namespace EbayClone.Application.Authentication.Commands.Login;
public record LoginCommand(string Email, string Password)
    : IRequest<LoginResponse>;

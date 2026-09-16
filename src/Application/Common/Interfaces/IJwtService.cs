using System;
using System.Collections.Generic;
using System.Text;
using EbayClone.Domain.Entities;

namespace EbayClone.Application.Common.Interfaces;
public interface IJwtService
{
    string GenerateToken(User user);
}

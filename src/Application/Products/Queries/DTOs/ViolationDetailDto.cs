using System;
using System.Collections.Generic;
using System.Text;

namespace EbayClone.Application.Products.Queries.DTOs;
public class ViolationDetailDto
{
    public int ProductId { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? ShopEmail { get; set; } 
    public List<ReportItemDto> Reports { get; set; } = new();

}


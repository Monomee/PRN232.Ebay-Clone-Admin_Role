using System;
using System.Collections.Generic;
using System.Text;

namespace EbayClone.Application.Products.Queries.DTOs;
public class ProductDto
{
    public int Id { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public decimal? Price { get; set; }
    public string? Images { get; set; }
    public string? Status { get; set; }
    public int? SellerId { get; set; }
    public string? SellerName { get; set; }
}

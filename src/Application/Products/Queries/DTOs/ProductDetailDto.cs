using System;
using System.Collections.Generic;

namespace EbayClone.Application.Products.Queries.DTOs;

public class ProductDetailDto
{
    public int Id { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public decimal? Price { get; set; }
    public string? Images { get; set; }
    public string? Status { get; set; }
    public int? SellerId { get; set; }
    public string? SellerName { get; set; }
    
    // Detailed properties needed for the UI
    public bool? IsAuction { get; set; }
    public DateTime? AuctionEndTime { get; set; }
    
    // Reviews
    public List<ReviewDto> Reviews { get; set; } = new List<ReviewDto>();
}

public class ReviewDto
{
    public int Id { get; set; }
    public int? ReviewerId { get; set; }
    public string? ReviewerName { get; set; }
    public int? Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime? CreatedAt { get; set; }
    
    // Seller Reply
    public string? SellerReply { get; set; }
    public DateTime? SellerReplyCreatedAt { get; set; }
}

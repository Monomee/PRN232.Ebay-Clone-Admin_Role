using System;
using System.Collections.Generic;
using System.Text;

namespace EbayClone.Application.Products.Queries.DTOs;
public class ReportItemDto
{
    public int ReportId { get; set; }
    public string? ReporterType { get; set; }
    public string? Reason { get; set; }
    public string? ProofDocumentUrl { get; set; } 
    public string? Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

}

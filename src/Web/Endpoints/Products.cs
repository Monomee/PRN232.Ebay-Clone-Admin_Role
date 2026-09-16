
using EbayClone.Application.Common.Models;
using EbayClone.Application.Products.Commands.CreateProduct;
using EbayClone.Application.Products.Commands.CreateProductReport;
using EbayClone.Application.Products.Commands.DeleteProduct;
using EbayClone.Application.Products.Commands.ResolveProductViolation;
using EbayClone.Application.Products.Commands.UpdateProduct;
using EbayClone.Application.Products.Queries.DTOs;
using EbayClone.Application.Products.Queries.GetProducts;
using EbayClone.Application.Products.Queries.GetProductById;
using EbayClone.Application.Products.Queries.GetSellerProducts;
using EbayClone.Application.Products.Queries.GetViolationDetails;
using EbayClone.Web.Infrastructure;

namespace EbayClone.Web.Endpoints;

public class Products : EndpointGroupBase
{
    public override void Map(RouteGroupBuilder group)
    {
        // 'group' ở đây chính là nhóm route "/api/products" đã được hệ thống tạo sẵn
        // Áp dụng Standard Policy (60 req/min) cho toàn bộ group này
        group.RequireRateLimiting(RateLimitingExtensions.StandardPolicy);

        // Chỉ cần khai báo các hành động cụ thể vào group này
        group.MapGet(GetProducts);              // Tự động thành: GET /api/products
        group.MapGet("{id}", GetProductById);   // GET /api/products/{id}
        group.MapPost(CreateProduct);           // Tự động thành: POST /api/products
        group.MapPut("{id}", UpdateProduct);
        group.MapDelete("{id}", DeleteProduct);
        group.MapGet("managed/{id}/violation-details", GetViolationDetails);
        group.MapPost("managed/{id}/resolve-violation", ResolveViolation);


        // [MỚI] Màn hình 5: Lấy danh sách quản lý (có Lọc/Tab)
        group.MapGet("managed", GetManagedProducts); // GET /api/products/managed
        group.MapPost("reports", CreateProductReport);

        // [MỚI] Lấy danh sách sản phẩm của Seller
        group.MapGet("seller/{sellerId}", GetSellerProducts);
    }
    public async Task<List<ProductDto>> GetProducts(ISender sender)
    {
        return await sender.Send(new GetProductsQuery());
    }

    public async Task<IResult> GetProductById(ISender sender, int id)
    {
        var product = await sender.Send(new GetProductByIdQuery(id));
        return product != null ? Results.Ok(product) : Results.NotFound();
    }

    public async Task<int> CreateProduct(ISender sender, CreateProductCommand command)
    {
        return await sender.Send(command);
    }

    public async Task<IResult> UpdateProduct(ISender sender, int id, UpdateProductCommand command)
    {
        if (id != command.Id) return Results.BadRequest();

        await sender.Send(command);

        return Results.NoContent();
    }

    public async Task<IResult> DeleteProduct(ISender sender, int id)
    {
        await sender.Send(new DeleteProductCommand(id));

        return Results.NoContent();
    }

    public async Task<IResult> CreateProductReport(ISender sender, CreateProductReportCommand command)
    {
        await sender.Send(command);
        return Results.NoContent();
    }

    // 1. API cho Màn hình 5 (List)
    // Sử dụng [AsParameters] để map query string (?page=1&tab=Reported) vào Object Query
    public async Task<PaginatedList<ManagedProductDto>> GetManagedProducts(ISender sender, [AsParameters] GetManagedProductsQuery query)
    {
        return await sender.Send(query);
    }

    public async Task<ViolationDetailDto> GetViolationDetails(ISender sender, int id)
    {
        return await sender.Send(new GetViolationDetailsQuery(id));
    }

    public async Task<IResult> ResolveViolation(ISender sender, int id, ResolveProductViolationCommand command)
    {
        if (id != command.ProductId) return Results.BadRequest();
        await sender.Send(command);
        return Results.NoContent();
    }

    public async Task<PaginatedList<ProductDto>> GetSellerProducts(ISender sender, int sellerId, [AsParameters] GetSellerProductsQuery query)
    {
        query.SellerId = sellerId;
        return await sender.Send(query);
    }
}

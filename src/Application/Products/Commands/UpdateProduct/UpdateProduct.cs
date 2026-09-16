using System;
using System.Collections.Generic;
using System.Text;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;

namespace EbayClone.Application.Products.Commands.UpdateProduct;
public record UpdateProductCommand : IRequest
{
    public int Id { get; set; } // Cần ID để biết sửa cái nào
    public string? Title { get; set; }
    public string? Description { get; set; }
    public decimal? Price { get; set; }
}

public class UpdateProductCommandHandler : IRequestHandler<UpdateProductCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public UpdateProductCommandHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        // 1. Tìm entity trong DB
        var entity = await _context.Products
            .FindAsync(new object[] { request.Id }, cancellationToken);

        // 2. Kiểm tra nếu không tồn tại
        if (entity == null)
        {
            throw new NotFoundException(nameof(Product), $"{request.Id}");
        }

        // 3. Kiểm tra tính sở hữu (IDOR)
        if (_user.Id == null || !int.TryParse(_user.Id, out int userId) || entity.SellerId != userId)
        {
            throw new EbayClone.Application.Common.Exceptions.ForbiddenAccessException();
        }

        // 3. Cập nhật dữ liệu
        // CỰC KỲ QUAN TRỌNG: Chỉ lấy đúng 3 trường để cập nhật. Không lấy Status!
        entity.Title = request.Title;
        entity.Description = request.Description;
        entity.Price = request.Price;

        // 4. Lưu thay đổi
        await _context.SaveChangesAsync(cancellationToken);
    }
}

using System;
using System.Collections.Generic;
using System.Text;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;

namespace EbayClone.Application.Products.Commands.DeleteProduct;
public record DeleteProductCommand(int Id) : IRequest;

public class DeleteProductCommandHandler : IRequestHandler<DeleteProductCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public DeleteProductCommandHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task Handle(DeleteProductCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.Products
            .FindAsync(new object[] { request.Id }, cancellationToken);

        if (entity == null)
        {
            throw new NotFoundException(nameof(Product), $"{request.Id}");
        }

        if (_user.Id == null || !int.TryParse(_user.Id, out int userId) || entity.SellerId != userId)
        {
            throw new EbayClone.Application.Common.Exceptions.ForbiddenAccessException();
        }

        _context.Products.Remove(entity);

        await _context.SaveChangesAsync(cancellationToken);
    }
}

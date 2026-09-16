using EbayClone.Application.AdminRoles;
using EbayClone.Application.AdminRoles.Commands.AssignRole;
using EbayClone.Application.AdminRoles.Commands.CreateAdminRole;
using EbayClone.Application.AdminRoles.Commands.CreateAdminUser;
using EbayClone.Application.AdminRoles.Queries.GetAdminRoles;
using EbayClone.Application.AdminRoles.Queries.GetAdminUsers;
using EbayClone.Application.Common.Models;
using EbayClone.Domain.Constants;
using EbayClone.Web.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace EbayClone.Web.Endpoints;

public class AdminRoles : EndpointGroupBase
{
    public override void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.RequireAuthorization(Policies.ManageAdminRoles);

        // Roles management
        groupBuilder.MapGet("roles", GetRoles);
        groupBuilder.MapPost("roles", CreateRole);

        // Admin users management
        groupBuilder.MapGet("users", GetAdminUsers);
        groupBuilder.MapPost("users", CreateAdminUser);
        groupBuilder.MapPost("users/assign", AssignRole);
    }

    public async Task<Ok<List<AdminRoleDto>>> GetRoles(ISender sender)
    {
        var result = await sender.Send(new GetAdminRolesQuery());
        return TypedResults.Ok(result);
    }

    public async Task<Created<int>> CreateRole(
        ISender sender,
        [FromBody] CreateRoleRequest request)
    {
        var roleId = await sender.Send(new CreateAdminRoleCommand
        {
            RoleName = request.RoleName,
            Description = request.Description,
            Permissions = request.Permissions,
            CreatedBy = request.CreatedBy
        });

        return TypedResults.Created($"/api/AdminRoles/roles/{roleId}", roleId);
    }

    public async Task<Ok<PaginatedList<AdminUserRoleDto>>> GetAdminUsers(
        ISender sender,
        [AsParameters] GetAdminUsersQuery query)
    {
        var result = await sender.Send(query);
        return TypedResults.Ok(result);
    }

    public async Task<Results<Ok, NotFound>> AssignRole(
        ISender sender,
        [FromBody] AssignRoleRequest request)
    {
        var result = await sender.Send(new AssignRoleCommand
        {
            UserId = request.UserId,
            RoleId = request.RoleId,
            AssignedBy = request.AssignedBy
        });

        if (!result)
            return TypedResults.NotFound();

        return TypedResults.Ok();
    }

    public async Task<Results<Created<int>, BadRequest<string>>> CreateAdminUser(
        ISender sender,
        [FromBody] CreateAdminUserRequest request)
    {
        try
        {
            var userId = await sender.Send(new CreateAdminUserCommand
            {
                Username = request.Username,
                Email = request.Email,
                Password = request.Password,
                RoleId = request.RoleId,
                CreatedBy = request.CreatedBy
            });

            return TypedResults.Created($"/api/AdminRoles/users/{userId}", userId);
        }
        catch (ArgumentException ex)
        {
            return TypedResults.BadRequest(ex.Message);
        }
    }
}

// Request DTOs
public record CreateRoleRequest(string RoleName, string? Description, List<string> Permissions, int CreatedBy);
public record CreateAdminUserRequest(string Username, string Email, string Password, int RoleId, int CreatedBy);
public record AssignRoleRequest(int UserId, int RoleId, int AssignedBy);

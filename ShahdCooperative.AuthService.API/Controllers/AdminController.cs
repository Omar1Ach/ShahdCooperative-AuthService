using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShahdCooperative.AuthService.Application.Commands.DeleteUser;
using ShahdCooperative.AuthService.Application.Commands.LockUser;
using ShahdCooperative.AuthService.Application.Commands.UnlockUser;
using ShahdCooperative.AuthService.Application.Commands.UpdateUserRole;
using ShahdCooperative.AuthService.Application.DTOs;
using ShahdCooperative.AuthService.Application.Queries.GetAllUsers;
using ShahdCooperative.AuthService.Application.Queries.GetAuditLogs;
using ShahdCooperative.AuthService.Application.Queries.GetSecurityDashboard;

namespace ShahdCooperative.AuthService.API.Controllers;

[ApiController]
[Route("api/[controller]")]
// [Authorize(Roles = "Admin")] // TODO: Uncomment when authentication is fully implemented
public class AdminController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Get all users with pagination and filtering
    /// </summary>
    [HttpGet("users")]
    [ProducesResponseType(typeof(PaginatedResponse<AdminUserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllUsers(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDescending = false)
    {
        var query = new GetAllUsersQuery(pageNumber, pageSize, searchTerm, sortBy, sortDescending);
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Lock a user account
    /// </summary>
    [HttpPost("users/{userId}/lock")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> LockUser(Guid userId, [FromBody] int lockoutMinutes = 1440)
    {
        var command = new LockUserCommand(userId, lockoutMinutes);
        await _mediator.Send(command);
        return Ok(new { message = "User locked successfully" });
    }

    /// <summary>
    /// Unlock a user account
    /// </summary>
    [HttpPost("users/{userId}/unlock")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnlockUser(Guid userId)
    {
        var command = new UnlockUserCommand(userId);
        await _mediator.Send(command);
        return Ok(new { message = "User unlocked successfully" });
    }

    /// <summary>
    /// Delete a user (soft delete)
    /// </summary>
    [HttpDelete("users/{userId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteUser(Guid userId)
    {
        var command = new DeleteUserCommand(userId);
        await _mediator.Send(command);
        return Ok(new { message = "User deleted successfully" });
    }

    /// <summary>
    /// Update user role
    /// </summary>
    [HttpPut("users/{userId}/role")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateUserRole(Guid userId, [FromBody] UpdateRoleRequest request)
    {
        var command = new UpdateUserRoleCommand(userId, request.Role);
        await _mediator.Send(command);
        return Ok(new { message = "User role updated successfully" });
    }

    /// <summary>
    /// Get audit logs with filtering
    /// </summary>
    [HttpGet("audit-logs")]
    [ProducesResponseType(typeof(PaginatedResponse<AuditLogDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? userId = null,
        [FromQuery] string? action = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        var query = new GetAuditLogsQuery(pageNumber, pageSize, userId, action, startDate, endDate);
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Get security dashboard statistics
    /// </summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(SecurityDashboardDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSecurityDashboard()
    {
        var query = new GetSecurityDashboardQuery();
        var result = await _mediator.Send(query);
        return Ok(result);
    }
}

using MediatR;
using ShahdCooperative.AuthService.Application.DTOs;

namespace ShahdCooperative.AuthService.Application.Queries.GetAuditLogs;

public record GetAuditLogsQuery(
    int PageNumber = 1,
    int PageSize = 20,
    Guid? UserId = null,
    string? Action = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null) : IRequest<PaginatedResponse<AuditLogDto>>;

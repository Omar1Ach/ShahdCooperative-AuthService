using AutoMapper;
using MediatR;
using ShahdCooperative.AuthService.Application.DTOs;
using ShahdCooperative.AuthService.Domain.Interfaces;

namespace ShahdCooperative.AuthService.Application.Queries.GetAuditLogs;

public class GetAuditLogsQueryHandler : IRequestHandler<GetAuditLogsQuery, PaginatedResponse<AuditLogDto>>
{
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IMapper _mapper;

    public GetAuditLogsQueryHandler(IAuditLogRepository auditLogRepository, IMapper mapper)
    {
        _auditLogRepository = auditLogRepository;
        _mapper = mapper;
    }

    public async Task<PaginatedResponse<AuditLogDto>> Handle(GetAuditLogsQuery request, CancellationToken cancellationToken)
    {
        var (logs, totalCount) = await _auditLogRepository.GetAllAsync(
            request.PageNumber,
            request.PageSize,
            request.UserId,
            request.Action,
            request.StartDate,
            request.EndDate);

        var logDtos = _mapper.Map<List<AuditLogDto>>(logs);

        return new PaginatedResponse<AuditLogDto>
        {
            Items = logDtos,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }
}

using MediatR;
using ShahdCooperative.AuthService.Application.DTOs;

namespace ShahdCooperative.AuthService.Application.Queries.GetSecurityDashboard;

public record GetSecurityDashboardQuery() : IRequest<SecurityDashboardDto>;

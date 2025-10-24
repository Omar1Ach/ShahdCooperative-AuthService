using MediatR;
using ShahdCooperative.AuthService.Application.DTOs;

namespace ShahdCooperative.AuthService.Application.Queries.GetAllUsers;

public record GetAllUsersQuery(
    int PageNumber = 1,
    int PageSize = 10,
    string? SearchTerm = null,
    string? SortBy = null,
    bool SortDescending = false) : IRequest<PaginatedResponse<AdminUserDto>>;

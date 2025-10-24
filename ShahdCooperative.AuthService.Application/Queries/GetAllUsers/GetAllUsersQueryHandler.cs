using AutoMapper;
using MediatR;
using ShahdCooperative.AuthService.Application.DTOs;
using ShahdCooperative.AuthService.Domain.Interfaces;

namespace ShahdCooperative.AuthService.Application.Queries.GetAllUsers;

public class GetAllUsersQueryHandler : IRequestHandler<GetAllUsersQuery, PaginatedResponse<AdminUserDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly IMapper _mapper;

    public GetAllUsersQueryHandler(IUserRepository userRepository, IMapper mapper)
    {
        _userRepository = userRepository;
        _mapper = mapper;
    }

    public async Task<PaginatedResponse<AdminUserDto>> Handle(GetAllUsersQuery request, CancellationToken cancellationToken)
    {
        var (users, totalCount) = await _userRepository.GetAllAsync(
            request.PageNumber,
            request.PageSize,
            request.SearchTerm,
            request.SortBy,
            request.SortDescending);

        var userDtos = _mapper.Map<List<AdminUserDto>>(users);

        return new PaginatedResponse<AdminUserDto>
        {
            Items = userDtos,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }
}

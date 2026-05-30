using MultiClinica.API.Common;
using MultiClinica.API.DTOs;
using MultiClinica.API.DTOs.User;

namespace MultiClinica.API.Services.Interfaces;

public interface IUserService
{
    Task<Result<PagedResult<UserResponseDto>>> GetPagedAsync(int page, int pageSize);

    Task<Result<UserResponseDto>> GetByIdAsync(int id);

    Task<Result<UserResponseDto>> CreateAsync(CreateUserDto dto);

    Task<Result<UserResponseDto>> UpdateAsync(int id, UpdateUserDto dto);

    Task<Result<bool>> DeleteAsync(int id);
}

using MultiClinica.API.Common;
using MultiClinica.API.DTOs.SessionTypes;
using MultiClinica.API.Models;
using MultiClinica.API.Repositories;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Services;

public class SessionTypeService(SessionTypeRepository repository, IUsuarioLogadoService usuario)
{
    private static SessionTypeResponseDto Map(SessionType item) => new()
    {
        Id = item.Id,
        Name = item.Name
    };

    public async Task<List<SessionTypeResponseDto>> GetAllAsync(string? search) =>
        (await repository.GetAllAsync(search)).Select(Map).ToList();

    public async Task<Result<SessionTypeResponseDto>> CreateAsync(CreateSessionTypeDto dto)
    {
        var name = dto.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return Result<SessionTypeResponseDto>.Fail(ErrorCodes.EmptyField, "O nome é obrigatório.");
        if (name.Length > 100)
            return Result<SessionTypeResponseDto>.Fail(ErrorCodes.InvalidValue, "O nome deve ter no máximo 100 caracteres.");
        if (await repository.ExistsByNameAsync(name))
            return Result<SessionTypeResponseDto>.Fail(ErrorCodes.DuplicateName, "Já existe um tipo de sessão com este nome.");

        var entity = new SessionType
        {
            Name = name,
            ClinicaId = usuario.ClinicaId
        };

        await repository.AddAsync(entity);
        return Result<SessionTypeResponseDto>.Ok(Map(entity));
    }
}

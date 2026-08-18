using MultiClinica.API.Common;

namespace MultiClinica.API.Services.Interfaces;

public interface IClinicLikeService
{
    Task<Result<ClinicLikeResult>> LikeAsync(int clinicId);
    Task<Result<ClinicLikeResult>> UnlikeAsync(int clinicId);
}

public sealed record ClinicLikeResult(int ClinicId, int LikeCount, bool LikedByMe);

namespace MultiClinica.API.Services.Interfaces;

public interface IAttachmentStorage
{
    Task<string> SaveAsync(Stream stream, string objectKey, string contentType, CancellationToken cancellationToken = default);
    Task<string> CreateReadUrlAsync(string objectKey, TimeSpan expiresIn, CancellationToken cancellationToken = default);
}

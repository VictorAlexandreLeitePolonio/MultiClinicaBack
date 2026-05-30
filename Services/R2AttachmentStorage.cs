using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Services;

public class R2AttachmentStorage(IConfiguration configuration) : IAttachmentStorage
{
    public Task<string> SaveAsync(Stream stream, string objectKey, string contentType, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(configuration["R2_BUCKET_NAME"]))
            throw new InvalidOperationException("R2_BUCKET_NAME não configurado.");

        return Task.FromResult(objectKey);
    }
}

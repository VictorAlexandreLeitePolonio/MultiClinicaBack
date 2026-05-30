using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Services;

public class R2AttachmentStorage(IConfiguration configuration) : IAttachmentStorage
{
    public async Task<string> SaveAsync(Stream stream, string objectKey, string contentType, CancellationToken cancellationToken = default)
    {
        var (client, bucketName) = CreateClient();
        using (client)
        {
            await client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = bucketName,
                Key = objectKey,
                InputStream = stream,
                ContentType = contentType,
                AutoCloseStream = false
            }, cancellationToken);
        }

        return objectKey;
    }

    public Task<string> CreateReadUrlAsync(string objectKey, TimeSpan expiresIn, CancellationToken cancellationToken = default)
    {
        var (client, bucketName) = CreateClient();
        using (client)
        {
            var url = client.GetPreSignedURL(new GetPreSignedUrlRequest
            {
                BucketName = bucketName,
                Key = objectKey,
                Expires = DateTime.UtcNow.Add(expiresIn),
                Verb = HttpVerb.GET
            });

            return Task.FromResult(url);
        }
    }

    private (AmazonS3Client Client, string BucketName) CreateClient()
    {
        var accountId = configuration["R2_ACCOUNT_ID"];
        var accessKeyId = configuration["R2_ACCESS_KEY_ID"];
        var secretAccessKey = configuration["R2_SECRET_ACCESS_KEY"];
        var bucketName = configuration["R2_BUCKET_NAME"];

        if (string.IsNullOrWhiteSpace(accountId)
            || string.IsNullOrWhiteSpace(accessKeyId)
            || string.IsNullOrWhiteSpace(secretAccessKey)
            || string.IsNullOrWhiteSpace(bucketName))
        {
            throw new InvalidOperationException("Configuração R2 incompleta. Defina R2_ACCOUNT_ID, R2_ACCESS_KEY_ID, R2_SECRET_ACCESS_KEY e R2_BUCKET_NAME.");
        }

        var credentials = new BasicAWSCredentials(accessKeyId, secretAccessKey);
        var client = new AmazonS3Client(credentials, new AmazonS3Config
        {
            ServiceURL = $"https://{accountId}.r2.cloudflarestorage.com",
            AuthenticationRegion = "auto",
            ForcePathStyle = true,
            RegionEndpoint = RegionEndpoint.USEast1
        });

        return (client, bucketName);
    }
}

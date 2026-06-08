using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Services;

public class S3AttachmentStorage(IConfiguration configuration) : IAttachmentStorage
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
        var accessKeyId = configuration["AWS_ACCESS_KEY_ID"];
        var secretAccessKey = configuration["AWS_SECRET_ACCESS_KEY"];
        var sessionToken = configuration["AWS_SESSION_TOKEN"];
        var region = configuration["AWS_REGION"];
        var bucketName = configuration["S3_BUCKET_NAME"];

        if (string.IsNullOrWhiteSpace(accessKeyId)
            || string.IsNullOrWhiteSpace(secretAccessKey)
            || string.IsNullOrWhiteSpace(region)
            || string.IsNullOrWhiteSpace(bucketName))
        {
            throw new InvalidOperationException("Configuração S3 incompleta. Defina AWS_ACCESS_KEY_ID, AWS_SECRET_ACCESS_KEY, AWS_REGION e S3_BUCKET_NAME.");
        }

        AWSCredentials credentials = string.IsNullOrWhiteSpace(sessionToken)
            ? new BasicAWSCredentials(accessKeyId, secretAccessKey)
            : new SessionAWSCredentials(accessKeyId, secretAccessKey, sessionToken);
        var client = new AmazonS3Client(credentials, new AmazonS3Config
        {
            RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region)
        });

        return (client, bucketName);
    }
}

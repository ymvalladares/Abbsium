using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Server.Services
{
    public class S3Service : IS3Service
    {
        private readonly AmazonS3Client _s3Client;
        private readonly string _bucketName;
        private readonly string _region;
        private readonly ILogger<S3Service> _logger;

        public S3Service(IConfiguration config, ILogger<S3Service> logger)
        {
            _bucketName = config["AWS:BucketName"] ?? throw new ArgumentNullException("AWS:BucketName");
            _region = config["AWS:Region"] ?? "us-east-1";
            
            var awsOptions = new AmazonS3Config
            {
                RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(_region)
            };

            _s3Client = new AmazonS3Client(
                config["AWS:AccessKey"],
                config["AWS:SecretKey"],
                awsOptions
            );
            
            _logger = logger;
        }

        public async Task<object> GeneratePresignedUploadUrlAsync(string fileName, string contentType, int expirationMinutes = 15)
        {
            var key = $"uploads/{Guid.NewGuid()}_{fileName}";
            
            var request = new GetPreSignedUrlRequest
            {
                BucketName = _bucketName,
                Key = key,
                Expires = DateTime.UtcNow.AddMinutes(expirationMinutes),
                ContentType = contentType,
                Verb = HttpVerb.PUT,
                Protocol = Protocol.HTTPS
            };

            var url = _s3Client.GetPreSignedURL(request);
            var publicUrl = GetPublicUrl(key);
            
            _logger.LogInformation("Generated presigned URL for key: {Key}", key);
            
            return new { Url = url, Key = key, PublicUrl = publicUrl };
        }

        public string GeneratePresignedDownloadUrl(string key, int expirationMinutes = 60)
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = _bucketName,
                Key = key,
                Expires = DateTime.UtcNow.AddMinutes(expirationMinutes),
                Verb = HttpVerb.GET,
                Protocol = Protocol.HTTPS
            };

            return _s3Client.GetPreSignedURL(request);
        }

        public async Task<Stream> DownloadStreamAsync(string key)
        {
            var request = new GetObjectRequest
            {
                BucketName = _bucketName,
                Key = key
            };

            var response = await _s3Client.GetObjectAsync(request);
            
            var memoryStream = new MemoryStream();
            await response.ResponseStream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;
            
            _logger.LogInformation("Downloaded stream for key: {Key}, size: {Size} bytes", key, memoryStream.Length);
            
            return memoryStream;
        }

        public string GetPublicUrl(string key)
        {
            var regionPart = _region == "us-east-1" ? "" : $".{_region}";
            return $"https://{_bucketName}.s3{regionPart}.amazonaws.com/{key}";
        }
    }
}

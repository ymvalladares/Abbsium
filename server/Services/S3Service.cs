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
            
            try
            {
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
                
                _logger.LogInformation("Generated presigned URL for key: {Key}, bucket: {Bucket}", key, _bucketName);
                
                return new { Url = url, Key = key, PublicUrl = publicUrl };
            }
            catch (AmazonS3Exception ex)
            {
                _logger.LogError(ex, "S3 error generating presigned URL: {ErrorCode} - {Message}", ex.ErrorCode, ex.Message);
                throw new InvalidOperationException($"Failed to generate upload URL: {ex.Message}", ex);
            }
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

        public async Task<bool> DeleteObjectAsync(string key)
        {
            try
            {
                var request = new DeleteObjectRequest
                {
                    BucketName = _bucketName,
                    Key = key
                };

                await _s3Client.DeleteObjectAsync(request);
                _logger.LogInformation("Deleted S3 object: {Key}", key);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete S3 object: {Key}", key);
                return false;
            }
        }

        public async Task<int> DeleteObjectsByPrefixAsync(string prefix)
        {
            var deletedCount = 0;
            try
            {
                var listRequest = new ListObjectsV2Request
                {
                    BucketName = _bucketName,
                    Prefix = prefix
                };

                ListObjectsV2Response? listResponse;
                do
                {
                    listResponse = await _s3Client.ListObjectsV2Async(listRequest);

                    if (listResponse.S3Objects.Count > 0)
                    {
                        var deleteRequest = new DeleteObjectsRequest
                        {
                            BucketName = _bucketName
                        };

                        foreach (var s3Object in listResponse.S3Objects)
                        {
                            deleteRequest.AddKey(s3Object.Key);
                        }

                        await _s3Client.DeleteObjectsAsync(deleteRequest);
                        deletedCount += listResponse.S3Objects.Count;
                        _logger.LogInformation("Deleted {Count} objects with prefix: {Prefix}", listResponse.S3Objects.Count, prefix);
                    }

                    listRequest.ContinuationToken = listResponse.NextContinuationToken;
                }
                while (listResponse.IsTruncated);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete objects with prefix: {Prefix}", prefix);
            }

            return deletedCount;
        }

        public async Task<bool> VerifyBucketExistsAsync()
        {
            try
            {
                var request = new GetBucketLocationRequest
                {
                    BucketName = _bucketName
                };

                var response = await _s3Client.GetBucketLocationAsync(request);
                _logger.LogInformation("Bucket {Bucket} exists, location: {Location}", _bucketName, response.Location);
                return true;
            }
            catch (AmazonS3Exception ex) when (ex.ErrorCode == "NoSuchBucket")
            {
                _logger.LogError("Bucket {Bucket} does not exist", _bucketName);
                return false;
            }
            catch (AmazonS3Exception ex) when (ex.ErrorCode == "AccessDenied")
            {
                _logger.LogError("Access denied to bucket {Bucket}. Check IAM permissions.", _bucketName);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error verifying bucket {Bucket}", _bucketName);
                return false;
            }
        }

        public async Task<object> GenerateCarPhotoPresignedUrlAsync(Guid dealerId, string dealerName, int carId, string fileName, string contentType, int expirationMinutes = 15)
        {
            var safeName = SanitizeFolderName(dealerName);
            var folderName = $"{dealerId}_{safeName}";
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var uniqueId = Guid.NewGuid().ToString("N")[..8];
            var extension = Path.GetExtension(fileName);
            var cleanName = Path.GetFileNameWithoutExtension(fileName)
                .Replace(" ", "_")
                .Replace("__", "_")
                .Trim('_');
            var key = $"dealers/{folderName}/cars/{carId}/{timestamp}_{uniqueId}_{cleanName}{extension}";

            try
            {
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

                _logger.LogInformation(
                    "Generated presigned URL for dealer={DealerId} ({SafeName}), car={CarId}, key={Key}, bucket={Bucket}",
                    dealerId, safeName, carId, key, _bucketName);

                return new { Url = url, Key = key, PublicUrl = publicUrl };
            }
            catch (AmazonS3Exception ex)
            {
                _logger.LogError(ex, "S3 error generating presigned URL for dealer={DealerId}, car={CarId}: {ErrorCode} - {Message}", dealerId, carId, ex.ErrorCode, ex.Message);
                throw new InvalidOperationException($"Failed to generate upload URL: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error generating presigned URL for dealer={DealerId}, car={CarId}", dealerId, carId);
                throw;
            }
        }

        private string SanitizeFolderName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "unknown";

            return new string(name
                .ToLowerInvariant()
                .Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_')
                .ToArray())
                .Replace(" ", "-")
                .Trim('-')
                .Substring(0, Math.Min(name.Length, 50));
        }

        public string GetCarPhotoPublicUrl(string s3Key)
        {
            if (string.IsNullOrEmpty(s3Key)) return string.Empty;
            return GetPublicUrl(s3Key);
        }

        public async Task<List<S3ObjectSummary>> ListObjectsByPrefixAsync(string prefix)
        {
            var objects = new List<S3ObjectSummary>();
            try
            {
                var listRequest = new ListObjectsV2Request
                {
                    BucketName = _bucketName,
                    Prefix = prefix
                };

                ListObjectsV2Response? listResponse;
                do
                {
                    listResponse = await _s3Client.ListObjectsV2Async(listRequest);

                    foreach (var s3Object in listResponse.S3Objects)
                    {
                        objects.Add(new S3ObjectSummary
                        {
                            Key = s3Object.Key,
                            Size = s3Object.Size,
                            LastModified = s3Object.LastModified
                        });
                    }

                    listRequest.ContinuationToken = listResponse.NextContinuationToken;
                }
                while (listResponse.IsTruncated);

                _logger.LogInformation("Listed {Count} objects with prefix: {Prefix}", objects.Count, prefix);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to list objects with prefix: {Prefix}", prefix);
            }

            return objects;
        }

        public async Task<int> DeleteDealerPhotosAsync(Guid dealerId)
        {
            var prefix = $"dealers/{dealerId}_";
            var deleted = await DeleteObjectsByPrefixAsync(prefix);
            _logger.LogInformation("Deleted {Count} photos for dealer {DealerId}", deleted, dealerId);
            return deleted;
        }

        public async Task<int> DeleteCarPhotosAsync(Guid dealerId, int carId)
        {
            var prefix = $"dealers/{dealerId}_";
            var subPrefix = $"/cars/{carId}/";
            var objects = await ListObjectsByPrefixAsync(prefix);
            var carObjects = objects.Where(o => o.Key.Contains(subPrefix)).ToList();

            int deleted = 0;
            if (carObjects.Count > 0)
            {
                var deleteRequest = new DeleteObjectsRequest { BucketName = _bucketName };
                foreach (var obj in carObjects)
                {
                    deleteRequest.AddKey(obj.Key);
                }

                await _s3Client.DeleteObjectsAsync(deleteRequest);
                deleted = carObjects.Count;
            }

            _logger.LogInformation("Deleted {Count} photos for car {CarId} (dealer {DealerId})", deleted, carId, dealerId);
            return deleted;
        }
    }
}

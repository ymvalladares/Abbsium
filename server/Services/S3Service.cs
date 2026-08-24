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

        private static readonly HashSet<string> AllowedImageContentTypes = new()
        {
            "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp", "image/svg+xml", "image/bmp"
        };

        private static readonly HashSet<string> AllowedVideoContentTypes = new()
        {
            "video/mp4", "video/webm", "video/ogg", "video/quicktime", "video/x-msvideo", "video/x-matroska"
        };

        private static readonly HashSet<string> AllowedImageExtensions = new()
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg", ".bmp"
        };

        private static readonly HashSet<string> AllowedVideoExtensions = new()
        {
            ".mp4", ".webm", ".ogg", ".mov", ".avi", ".mkv"
        };

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
            if (!IsValidContentType(contentType, fileName, out string validationError))
            {
                throw new InvalidOperationException(validationError);
            }

            var sanitizedFileName = SanitizeFileName(fileName);
            var key = $"uploads/{Guid.NewGuid()}_{sanitizedFileName}";
            
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
            if (!IsValidContentType(contentType, fileName, out string validationError))
            {
                throw new InvalidOperationException(validationError);
            }

            var safeName = SanitizeFolderName(dealerName);
            var folderName = $"{dealerId}_{safeName}";
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var uniqueId = Guid.NewGuid().ToString("N")[..8];
            var extension = Path.GetExtension(fileName);
            var cleanName = SanitizeFileName(Path.GetFileNameWithoutExtension(fileName));
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

        private string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "unnamed";

            var sanitized = new string(name
                .Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '.')
                .ToArray())
                .Replace("..", "")
                .Trim();

            if (sanitized.Length > 200)
                sanitized = sanitized.Substring(0, 200);

            return string.IsNullOrEmpty(sanitized) ? "unnamed" : sanitized;
        }

        private bool IsValidContentType(string contentType, string fileName, out string errorMessage)
        {
            errorMessage = null;
            var extension = Path.GetExtension(fileName)?.ToLowerInvariant();

            var isImage = AllowedImageContentTypes.Contains(contentType) || AllowedImageExtensions.Contains(extension);
            var isVideo = AllowedVideoContentTypes.Contains(contentType) || AllowedVideoExtensions.Contains(extension);

            if (!isImage && !isVideo)
            {
                errorMessage = "File type not allowed. Only images (jpg, png, gif, webp, svg) and videos (mp4, webm, mov) are permitted.";
                return false;
            }

            if (isImage && !AllowedImageContentTypes.Contains(contentType) && AllowedImageExtensions.Contains(extension))
            {
                errorMessage = "Invalid image file type.";
                return false;
            }

            if (isVideo && !AllowedVideoContentTypes.Contains(contentType) && AllowedVideoExtensions.Contains(extension))
            {
                errorMessage = "Invalid video file type.";
                return false;
            }

            return true;
        }

        public string GetCarPhotoPublicUrl(string s3Key)
        {
            if (string.IsNullOrEmpty(s3Key)) return string.Empty;
            return GetPublicUrl(s3Key);
        }

        public async Task<bool> UploadFileAsync(string key, string filePath, string contentType, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("File not found for S3 upload: {FilePath}", filePath);
                    return false;
                }

                using var fileStream = File.OpenRead(filePath);
                var request = new PutObjectRequest
                {
                    BucketName = _bucketName,
                    Key = key.TrimStart('/'),
                    InputStream = fileStream,
                    ContentType = contentType,
                    CannedACL = S3CannedACL.PublicRead
                };

                await _s3Client.PutObjectAsync(request, cancellationToken);
                _logger.LogInformation("Uploaded file to S3: {Key} ({Size} bytes, {ContentType})", key, new FileInfo(filePath).Length, contentType);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload file to S3: {Key}", key);
                return false;
            }
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

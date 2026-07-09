namespace Server.Services
{
    public interface IS3Service
    {
        Task<object> GeneratePresignedUploadUrlAsync(string fileName, string contentType, int expirationMinutes = 15);
        string GeneratePresignedDownloadUrl(string key, int expirationMinutes = 60);
        Task<Stream> DownloadStreamAsync(string key);
        string GetPublicUrl(string key);
        Task<bool> DeleteObjectAsync(string key);
        Task<int> DeleteObjectsByPrefixAsync(string prefix);
        Task<object> GenerateCarPhotoPresignedUrlAsync(Guid dealerId, string dealerName, int carId, string fileName, string contentType, int expirationMinutes = 15);
        Task<bool> VerifyBucketExistsAsync();
        Task<List<S3ObjectSummary>> ListObjectsByPrefixAsync(string prefix);
        Task<int> DeleteDealerPhotosAsync(Guid dealerId);
        Task<int> DeleteCarPhotosAsync(Guid dealerId, int carId);
        string GetCarPhotoPublicUrl(string s3Key);
    }

    public class S3ObjectSummary
    {
        public string Key { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
    }
}

namespace Server.Services
{
    public interface IS3Service
    {
        Task<object> GeneratePresignedUploadUrlAsync(string fileName, string contentType, int expirationMinutes = 15);
        string GeneratePresignedDownloadUrl(string key, int expirationMinutes = 60);
        Task<Stream> DownloadStreamAsync(string key);
        string GetPublicUrl(string key);
    }
}

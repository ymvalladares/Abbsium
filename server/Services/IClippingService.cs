using Server.Entitys;
using Server.ModelDTO;

namespace Server.Services
{
    public interface IClippingService
    {
        Task<ClipJob> CreateJobAsync(string userId, string videoUrl, int maxClips, int minDuration, int maxDuration, string? aspectRatio = null);
        Task<ClipJob?> GetJobAsync(string userId, Guid jobId);
        Task<List<ClipJob>> GetUserJobsAsync(string userId);
        Task ProcessJobAsync(Guid jobId, CancellationToken cancellationToken = default);
        Task DeleteJobAsync(string userId, Guid jobId);
        Task CleanupPublishedClipsAsync(string userId, Guid jobId);
    }
}

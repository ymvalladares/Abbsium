using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Server.ModelDTO;
using Server.Services;

namespace Server.Controllers
{
    [Authorize]
    public class ClippingController : Base_Control_Api
    {
        private readonly IClippingService _clippingService;
        private readonly IConfiguration _config;

        public ClippingController(IClippingService clippingService, IConfiguration config)
        {
            _clippingService = clippingService;
            _config = config;
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateJob([FromBody] CreateClipJobDto request)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });

            if (!IsValidVideoUrl(request.VideoUrl))
                return BadRequest("Invalid video URL. Supported: YouTube, Vimeo, direct video links.");

            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ??
                         User.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var job = await _clippingService.CreateJobAsync(
                userId,
                request.VideoUrl,
                request.MaxClips ?? 5,
                request.MinClipDuration ?? 30,
                request.MaxClipDuration ?? 60
            );

            return Ok(new
            {
                jobId = job.Id,
                status = job.Status,
                message = "Clip job created. Processing started."
            });
        }

        [HttpGet("jobs")]
        public async Task<IActionResult> GetJobs()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ??
                         User.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var jobs = await _clippingService.GetUserJobsAsync(userId);

            var backendUrl = _config["BackendUrl"]?.TrimEnd('/');

            var result = new List<object>();
            foreach (var j in jobs)
            {
                var clipsResult = new List<object>();
                foreach (var c in j.Clips)
                {
                    clipsResult.Add(new
                    {
                        c.Id,
                        c.Title,
                        c.Description,
                        c.StartTimeSeconds,
                        c.EndTimeSeconds,
                        c.DurationSeconds,
                        c.EngagementScore,
                        c.CreatedAt,
                        VideoUrl = !string.IsNullOrEmpty(c.S3Key) ? $"{backendUrl}{c.S3Key}" : null,
                        ThumbnailUrl = !string.IsNullOrEmpty(c.ThumbnailS3Key) ? $"{backendUrl}{c.ThumbnailS3Key}" : null
                    });
                }

                result.Add(new
                {
                    j.Id,
                    j.VideoUrl,
                    j.VideoTitle,
                    j.Status,
                    j.ErrorMessage,
                    j.MaxClips,
                    j.CreatedAt,
                    j.CompletedAt,
                    ClipCount = j.Clips.Count,
                    Clips = clipsResult
                });
            }

            return Ok(result);
        }

        [HttpGet("job/{jobId}")]
        public async Task<IActionResult> GetJob(Guid jobId)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ??
                         User.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var job = await _clippingService.GetJobAsync(userId, jobId);
            if (job == null)
                return NotFound("Job not found");

            var backendUrl = _config["BackendUrl"]?.TrimEnd('/');

            var clipsResult = new List<object>();
            foreach (var c in job.Clips)
            {
                clipsResult.Add(new
                {
                    c.Id,
                    c.Title,
                    c.Description,
                    c.StartTimeSeconds,
                    c.EndTimeSeconds,
                    c.DurationSeconds,
                    c.EngagementScore,
                    c.CreatedAt,
                    VideoUrl = !string.IsNullOrEmpty(c.S3Key) ? $"{backendUrl}{c.S3Key}" : null,
                    ThumbnailUrl = !string.IsNullOrEmpty(c.ThumbnailS3Key) ? $"{backendUrl}{c.ThumbnailS3Key}" : null
                });
            }

            return Ok(new
            {
                job.Id,
                job.VideoUrl,
                job.VideoTitle,
                job.Status,
                job.ErrorMessage,
                job.MaxClips,
                job.CreatedAt,
                job.CompletedAt,
                Clips = clipsResult
            });
        }

        [HttpDelete("job/{jobId}")]
        public async Task<IActionResult> DeleteJob(Guid jobId)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ??
                         User.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            await _clippingService.DeleteJobAsync(userId, jobId);
            return Ok(new { message = "Job deleted successfully" });
        }

        [HttpPost("job/{jobId}/cleanup")]
        public async Task<IActionResult> CleanupPublishedClips(Guid jobId)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ??
                         User.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            await _clippingService.CleanupPublishedClipsAsync(userId, jobId);
            return Ok(new { message = "Published clips cleaned up from S3" });
        }

        private bool IsValidVideoUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;

            var validPatterns = new[]
            {
                "youtube.com", "youtu.be", "vimeo.com", "dailymotion.com",
                "tiktok.com", "instagram.com", "facebook.com", "twitter.com",
                "x.com", ".mp4", ".mov", ".avi", ".mkv", ".webm"
            };

            var lowerUrl = url.ToLowerInvariant();
            return validPatterns.Any(p => lowerUrl.Contains(p)) && Uri.IsWellFormedUriString(url, UriKind.Absolute);
        }
    }
}

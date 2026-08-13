using System.ComponentModel.DataAnnotations;

namespace Server.ModelDTO
{
    public class CreateClipJobDto
    {
        [Required]
        public string VideoUrl { get; set; } = string.Empty;

        public int? MaxClips { get; set; }

        public int? MinClipDuration { get; set; }

        public int? MaxClipDuration { get; set; }
    }
}

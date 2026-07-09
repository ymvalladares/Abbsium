using System.Text.Json;

namespace Server.Services
{
    public class SocialErrorMapper
    {
        public static string MapFacebookError(string rawResponse, out string userAction)
        {
            userAction = null;

            try
            {
                using var doc = JsonDocument.Parse(rawResponse);
                var root = doc.RootElement;

                if (!root.TryGetProperty("error", out var error))
                    return rawResponse;

                var code = error.TryGetProperty("code", out var codeElem) ? codeElem.GetInt32() : 0;
                var subcode = error.TryGetProperty("error_subcode", out var subElem) ? subElem.GetInt32() : 0;
                var message = error.TryGetProperty("message", out var msgElem) ? msgElem.GetString() : "";

                if (code == 368 && subcode == 4854002)
                {
                    userAction = "Facebook requires identity confirmation. Open the Facebook app on your phone and follow the verification instructions.";
                    return "Identity verification required";
                }

                if (code == 190)
                {
                    userAction = "Your Facebook session has expired. Please reconnect your Facebook account.";
                    return "Session expired";
                }

                if (code == 200)
                {
                    if (message.Contains("publish_actions"))
                    {
                        userAction = "You do not have permission to publish. Make sure you are an admin of the Facebook Page.";
                        return "Insufficient permissions";
                    }
                    userAction = "You do not have permission to publish to this Page. Please verify that you are an admin.";
                    return "Insufficient permissions";
                }

                if (code == 100)
                {
                    userAction = "Invalid post data. Please check the content and try again.";
                    return "Invalid data";
                }

                if (code == 4)
                {
                    userAction = "Too many posts. Please wait a few minutes and try again.";
                    return "Rate limit reached";
                }

                if (code == 341)
                {
                    userAction = "The video is too long or has an unsupported format. Please use MP4 with a duration under 240 minutes.";
                    return "Video not supported";
                }

                if (!string.IsNullOrEmpty(message))
                {
                    userAction = $"Facebook reported: {message}";
                    return "Facebook error";
                }
            }
            catch { }

            userAction = "An unexpected error occurred. Please try again later.";
            return "Unknown error";
        }

        public static string MapInstagramError(string rawResponse, out string userAction)
        {
            userAction = null;

            try
            {
                using var doc = JsonDocument.Parse(rawResponse);
                var root = doc.RootElement;

                if (!root.TryGetProperty("error", out var error))
                    return rawResponse;

                var code = error.TryGetProperty("code", out var codeElem) ? codeElem.GetInt32() : 0;
                var message = error.TryGetProperty("message", out var msgElem) ? msgElem.GetString() : "";

                if (code == 190)
                {
                    userAction = "Your Instagram session has expired. Please reconnect your account.";
                    return "Session expired";
                }

                if (code == 10)
                {
                    userAction = "The image URL is not accessible. Make sure it is public and valid.";
                    return "Image not accessible";
                }

                if (message.Contains("Business account"))
                {
                    userAction = "You need an Instagram Business account. Convert your personal account to Business from the Instagram app.";
                    return "Business account required";
                }

                if (!string.IsNullOrEmpty(message))
                {
                    userAction = $"Instagram reported: {message}";
                    return "Instagram error";
                }
            }
            catch { }

            userAction = "An unexpected error occurred. Please try again later.";
            return "Unknown error";
        }

        public static string MapYouTubeError(string rawResponse, out string userAction)
        {
            userAction = null;

            try
            {
                using var doc = JsonDocument.Parse(rawResponse);
                var root = doc.RootElement;

                if (root.TryGetProperty("error", out var error) &&
                    error.TryGetProperty("message", out var msgElem))
                {
                    var message = msgElem.GetString() ?? "";

                    if (message.Contains("quota") || message.Contains("quotaExceeded"))
                    {
                        userAction = "Daily upload limit reached for YouTube. Please try again tomorrow.";
                        return "Daily limit reached";
                    }

                    if (message.Contains("upload"))
                    {
                        userAction = "Error uploading the video. Please ensure it is MP4 and under 256GB.";
                        return "Upload error";
                    }

                    userAction = $"YouTube reported: {message}";
                    return "YouTube error";
                }
            }
            catch { }

            userAction = "An unexpected error occurred. Please try again later.";
            return "Unknown error";
        }

        public static string MapTikTokError(string rawResponse, out string userAction)
        {
            userAction = null;

            try
            {
                using var doc = JsonDocument.Parse(rawResponse);
                var root = doc.RootElement;

                if (root.TryGetProperty("error", out var error) &&
                    error.TryGetProperty("message", out var msgElem))
                {
                    var message = msgElem.GetString() ?? "";

                    if (message.Contains("quota") || message.Contains("rate"))
                    {
                        userAction = "Too many uploads to TikTok. Please wait a few minutes and try again.";
                        return "Upload limit reached";
                    }

                    if (message.Contains("video"))
                    {
                        userAction = "The video does not meet TikTok requirements. Please use MP4 with a duration between 3 and 10 minutes.";
                        return "Video not supported";
                    }

                    userAction = $"TikTok reported: {message}";
                    return "TikTok error";
                }
            }
            catch { }

            userAction = "An unexpected error occurred. Please try again later.";
            return "Unknown error";
        }
    }
}

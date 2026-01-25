using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Entitys;
using Server.ModelDTO;
using System.Security.Claims;
using System.Text.Json;

namespace Server.Controllers
{
    public class SocialAuthController : Base_Control_Api
    {
        private readonly DbContext_app _db;
        private readonly IConfiguration _config;

        public SocialAuthController(DbContext_app db, IConfiguration config)
        {
            _db = db;
            _config = config;
        }

        // ================= FACEBOOK =================
        [Authorize]
        [HttpPost("facebook/connect")]
        public IActionResult ConnectFacebook()
        {
            
            var state = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var url =
                "https://www.facebook.com/v19.0/dialog/oauth" +
                $"?client_id={_config["Facebook:ClientId"]}" +
                $"&redirect_uri={_config["Facebook:RedirectUri"]}" +
                $"&state={state}" +
                $"&scope=public_profile";

            return Ok(new { url });
        }

        [HttpGet("facebook/callback")]
        public async Task<IActionResult> FacebookCallback(string code, string state)
        {
            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
                return Content("<script>window.opener.postMessage({ type: 'AUTH_ERROR' }, '*'); window.close();</script>", "text/html");

            var tokenResponse = await ExchangeFacebookCode(code);

            _db.SocialAccounts.Add(new SocialAccount
            {
                UserId = state,
                Provider = "facebook",
                AccessToken = tokenResponse.AccessToken,
                RefreshToken = string.Empty,
                ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn),
                Scope = "public_profile,email",
                IsActive = true
            });

            await _db.SaveChangesAsync();

            // Devuelve un HTML que envía el mensaje y cierra el popup
            return Content(@"
            <script>
                window.opener.postMessage({ type: 'AUTH_SUCCESS', provider: 'facebook' }, '*');
                window.close();
            </script>
        ", "text/html");
        }

        private async Task<(string AccessToken, int ExpiresIn)> ExchangeFacebookCode(string code)
        {
            using var client = new HttpClient();

            var url =
                $"https://graph.facebook.com/v19.0/oauth/access_token" +
                $"?client_id={_config["Facebook:ClientId"]}" +
                $"&client_secret={_config["Facebook:ClientSecret"]}" +
                $"&redirect_uri={_config["Facebook:RedirectUri"]}" +
                $"&code={code}";

            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var jsonString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            var accessToken = root.GetProperty("access_token").GetString();
            var expiresIn = root.GetProperty("expires_in").GetInt32();

            return (accessToken, expiresIn);
        }

        // ================= STATUS =================
        [Authorize]
        [HttpGet("status")]
        public IActionResult Status()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier); ;

            var connected = _db.SocialAccounts
                .Where(x => x.UserId == userId && x.IsActive)
                .Select(x => new SocialAccountDTO
                {
                    Provider = x.Provider,
                    Connected = true,
                    ExpiresAt = x.ExpiresAt
                })
                .ToList();

            return Ok(connected);
        }

        // ================= DISCONNECT =================
        [Authorize]
        [HttpPost("disconnect/{provider}")] // Cambiado a HttpDelete por convención
        public async Task<IActionResult> Disconnect(string provider)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var acc = await _db.SocialAccounts
                .FirstOrDefaultAsync(x => x.UserId == userId && x.Provider.ToLower() == provider.ToLower());

            if (acc == null) return NotFound();

            _db.SocialAccounts.Remove(acc);
            await _db.SaveChangesAsync();

            return Ok();
        }

        private async Task<bool> IsFacebookTokenValid(string accessToken)
        {
            using var client = new HttpClient();

            var res = await client.GetAsync(
                $"https://graph.facebook.com/me?access_token={accessToken}");

            return res.IsSuccessStatusCode;
        }

        [Authorize]
        [HttpGet("connections/check")]
        public async Task<IActionResult> CheckConnections()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier); ;

            var accounts = _db.SocialAccounts
                .Where(x => x.UserId == userId && x.IsActive)
                .ToList();

            var result = new List<object>();

            foreach (var acc in accounts)
            {
                bool isValid = false;

                if (acc.Provider == "facebook")
                {
                    isValid = await IsFacebookTokenValid(acc.AccessToken);
                }

                // Si el token murió → desactivar
                if (!isValid)
                {
                    acc.IsActive = false;
                    await _db.SaveChangesAsync();
                }

                result.Add(new
                {
                    provider = acc.Provider,
                    connected = isValid,
                    expiresAt = acc.ExpiresAt
                });
            }

            return Ok(result);
        }

        private async Task<IActionResult> TestFacebookProfile(string accessToken)
        {
            using var client = new HttpClient();

            var url =
                $"https://graph.facebook.com/me" +
                $"?fields=id,name,email,picture.width(200)" +
                $"&access_token={accessToken}";

            var res = await client.GetAsync(url);

            if (!res.IsSuccessStatusCode)
            {
                var err = await res.Content.ReadAsStringAsync();
                return BadRequest(new { error = "Facebook API error", details = err });
            }

            var json = await res.Content.ReadAsStringAsync();
            return Ok(JsonDocument.Parse(json).RootElement);
        }

        public class FacebookPostRequest
        {
            public string Message { get; set; }    // Texto del post
            public string PhotoUrl { get; set; }   // URL o Base64 de la foto
            public string Caption { get; set; }    // Caption de la foto
        }

        [Authorize]
        [HttpGet("facebook/test-profile")]
        public async Task<IActionResult> TestProfile()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var acc = await _db.SocialAccounts
                .FirstOrDefaultAsync(x => x.UserId == userId && x.Provider == "facebook" && x.IsActive);

            if (acc == null) return NotFound("Facebook not connected");

            return await TestFacebookProfile(acc.AccessToken);
        }

        [Authorize]
        [HttpPost("facebook/post")]
        public async Task<IActionResult> PostToFacebook([FromBody] FacebookPostRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Buscar cuenta de Facebook activa del usuario
            var acc = await _db.SocialAccounts
                .FirstOrDefaultAsync(x => x.UserId == userId && x.Provider == "facebook" && x.IsActive);

            if (acc == null)
                return BadRequest("Facebook not connected");

            try
            {
                using var client = new HttpClient();

                // Construir request a Graph API
                var fbUrl = "https://graph.facebook.com/me/photos"; // publicar foto
                var content = new MultipartFormDataContent();

                if (!string.IsNullOrEmpty(request.PhotoUrl))
                {
                    // Si es base64, convertir a byte[]
                    byte[] bytes;
                    if (request.PhotoUrl.StartsWith("data:"))
                    {
                        var base64Data = request.PhotoUrl.Substring(request.PhotoUrl.IndexOf(",") + 1);
                        bytes = Convert.FromBase64String(base64Data);
                    }
                    else
                    {
                        bytes = System.IO.File.ReadAllBytes(request.PhotoUrl);
                    }

                    content.Add(new ByteArrayContent(bytes), "source", "photo.jpg");
                }

                content.Add(new StringContent(request.Message ?? ""), "message");

                // token de acceso
                content.Add(new StringContent(acc.AccessToken), "access_token");

                var fbResponse = await client.PostAsync(fbUrl, content);
                var responseString = await fbResponse.Content.ReadAsStringAsync();

                if (!fbResponse.IsSuccessStatusCode)
                {
                    return BadRequest(new { error = "Facebook API error", details = responseString });
                }

                return Ok(new { status = "Published", response = responseString });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Server error", details = ex.Message });
            }
        }






        // ================= OTROS PROVEEDORES (ejemplo) =================
        // Puedes agregar Instagram, YouTube, TikTok, etc. de manera similar
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Server.Controllers;

namespace Server.SocialNetwork.Controllers
{
    [Authorize]
    public class FacebookConnectController : Base_Control_Api
    {
        [HttpGet("connect")]
        public IActionResult Connect()
        {
            var url =
              $"https://www.facebook.com/v19.0/dialog/oauth" +
              $"?client_id=APP_ID" +
              $"&redirect_uri=CALLBACK_URL" +
              $"&scope=pages_show_list,pages_manage_posts";

            return Redirect(url);
        }


        //[HttpPost("publish")]
        //public async Task<IActionResult> Publish(PostRequest req)
        //{
        //    var token = GetTokenFromDB(UserId, "Facebook", req.PageId);

        //    // Llamada Graph API
        //}
    }
}

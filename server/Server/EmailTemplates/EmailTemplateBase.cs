namespace Server.EmailTemplates
{
    public static class EmailTemplateBase
    {
        public static string Wrap(string accentColor, string headerTitle, string headerSubtitle, string bodyContent)
        {
            return $@"
                    <!DOCTYPE html>
                    <html>
                    <head>
                      <meta charset='UTF-8'>
                      <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                    </head>
                    <body style='margin:0;padding:0;background-color:#f0f4ff;font-family:DM Sans,Segoe UI,sans-serif;'>
                      <table width='100%' cellpadding='0' cellspacing='0' style='background-color:#f0f4ff;padding:40px 16px;'>
                        <tr>
                          <td align='center'>
                            <table width='100%' cellpadding='0' cellspacing='0' style='max-width:560px;background:#ffffff;border-radius:20px;overflow:hidden;border:1px solid #e8eaed;'>

                              <!-- Header -->
                              <tr>
                                <td style='background:linear-gradient(135deg,{accentColor} 0%,{accentColor}cc 100%);padding:36px 40px;text-align:center;'>
                                  <p style='margin:0;font-size:26px;font-weight:800;color:#ffffff;letter-spacing:-0.5px;'>Abbsium</p>
                                  <p style='margin:6px 0 0;font-size:13px;color:rgba(255,255,255,0.75);'>{headerSubtitle}</p>
                                </td>
                              </tr>

                              <!-- Dynamic Body -->
                              <tr>
                                <td style='padding:40px 40px 32px;'>
                                  {bodyContent}
                                </td>
                              </tr>

                              <!-- Footer -->
                              <tr>
                                <td style='background:#f8f9ff;padding:20px 40px;border-top:1px solid #f1f3f4;'>
                                  <p style='margin:0;font-size:12.5px;color:#9aa0a6;line-height:1.6;'>
                                    🔒 If you didn't request this email, you can safely ignore it.
                                  </p>
                                </td>
                              </tr>
                              <tr>
                                <td style='padding:24px 40px;text-align:center;'>
                                  <p style='margin:0;font-size:12px;color:#b0bec5;'>© {DateTime.UtcNow.Year} Abbsium. All rights reserved.</p>
                                </td>
                              </tr>

                            </table>
                          </td>
                        </tr>
                      </table>
                    </body>
                    </html>";
        }

        // Reusable button block
        public static string Button(string link, string label, string color) => $@"
        <table cellpadding='0' cellspacing='0' width='100%'>
          <tr>
            <td align='center' style='padding:8px 0 32px;'>
              <a href='{link}'
                 style='display:inline-block;background:linear-gradient(135deg,{color} 0%,{color}cc 100%);color:#ffffff;text-decoration:none;font-size:15px;font-weight:700;padding:14px 36px;border-radius:100px;'>
                {label}
              </a>
            </td>
          </tr>
        </table>";

        // Reusable divider
        public static string Divider() => @"
        <table cellpadding='0' cellspacing='0' width='100%' style='margin-bottom:24px;'>
          <tr><td style='border-top:1px solid #f1f3f4;'></td></tr>
        </table>";

        // Reusable fallback link
        public static string FallbackLink(string link) => $@"
        <p style='margin:0 0 8px;font-size:13px;color:#9aa0a6;'>If the button doesn't work, copy and paste this link:</p>
        <p style='margin:0;font-size:12px;color:#6366f1;word-break:break-all;'>{link}</p>";
    }
}

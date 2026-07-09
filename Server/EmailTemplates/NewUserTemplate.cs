namespace Server.EmailTemplates
{
    public static class NewUserTemplate
    {
        public static string Build(string username, string password)
        {
            var body = $@"
            <p style='margin:0 0 8px;font-size:22px;font-weight:700;color:#0d1117;letter-spacing:-0.3px;'>Welcome to Abbsium</p>
            <p style='margin:0 0 28px;font-size:15px;color:#5f6368;line-height:1.7;'>
              An administrator has created an account for you. Use the credentials below to log in and change your password as soon as possible.
            </p>
            <table cellpadding='0' cellspacing='0' width='100%'>
              <tr>
                <td style='padding:16px;background:#f8f9ff;border-radius:12px;border:1px solid #e8eaed;'>
                  <table width='100%' cellpadding='0' cellspacing='0'>
                    <tr>
                      <td style='padding-bottom:12px;'>
                        <p style='margin:0;font-size:12px;color:#9aa0a6;'>Username</p>
                        <p style='margin:4px 0 0;font-size:16px;font-weight:700;color:#0d1117;'>{username}</p>
                      </td>
                    </tr>
                    <tr>
                      <td style='border-top:1px solid #e8eaed;padding-top:12px;'>
                        <p style='margin:0;font-size:12px;color:#9aa0a6;'>Temporary Password</p>
                        <p style='margin:4px 0 0;font-size:16px;font-weight:700;color:#6366f1;letter-spacing:1px;'>{password}</p>
                      </td>
                    </tr>
                  </table>
                </td>
              </tr>
            </table>
            {EmailTemplateBase.Divider()}
            <p style='margin:0 0 8px;font-size:13px;color:#9aa0a6;'>For security reasons, please change your password after your first login.</p>";

            return EmailTemplateBase.Wrap(
                accentColor: "#10b981",
                headerTitle: "Abbsium",
                headerSubtitle: "Welcome aboard",
                bodyContent: body
            );
        }
    }
}

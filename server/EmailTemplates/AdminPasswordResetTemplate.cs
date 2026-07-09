namespace Server.EmailTemplates
{
    public static class AdminPasswordResetTemplate
    {
        public static string Build(string newPassword)
        {
            var body = $@"
            <p style='margin:0 0 8px;font-size:22px;font-weight:700;color:#0d1117;letter-spacing:-0.3px;'>Your password has been reset</p>
            <p style='margin:0 0 28px;font-size:15px;color:#5f6368;line-height:1.7;'>
              An administrator has reset your password. Use the temporary password below to log in and change it immediately.
            </p>
            <table cellpadding='0' cellspacing='0' width='100%'>
              <tr>
                <td align='center' style='padding:16px;background:#f8f9ff;border-radius:12px;border:1px solid #e8eaed;'>
                  <p style='margin:0;font-size:12px;color:#9aa0a6;'>Temporary Password</p>
                  <p style='margin:8px 0 0;font-size:24px;font-weight:800;color:#6366f1;letter-spacing:1px;'>{newPassword}</p>
                </td>
              </tr>
            </table>
            {EmailTemplateBase.Divider()}
            <p style='margin:0 0 8px;font-size:13px;color:#9aa0a6;'>For security reasons, please change your password after logging in.</p>";

            return EmailTemplateBase.Wrap(
                accentColor: "#6366f1",
                headerTitle: "Abbsium",
                headerSubtitle: "Password reset by administrator",
                bodyContent: body
            );
        }
    }
}

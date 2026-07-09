namespace Server.EmailTemplates
{
    public static class PasswordChangedTemplate
    {
        public static string Build(string userName)
        {
            var body = $@"
            <p style='margin:0 0 8px;font-size:22px;font-weight:700;color:#0d1117;letter-spacing:-0.3px;'>Your password was changed</p>
            <p style='margin:0 0 28px;font-size:15px;color:#5f6368;line-height:1.7;'>
              Hello {userName}, your password has been changed successfully. If you did not make this change, please contact our support team or an administrator immediately.
            </p>
            <table cellpadding='0' cellspacing='0' width='100%'>
              <tr>
                <td style='padding:16px;background:#fff3f3;border-radius:12px;border:1px solid #fde8e8;'>
                  <p style='margin:0;font-size:13px;color:#b91c1c;line-height:1.6;'>
                    If you don't recognize this activity, please reach out to your administrator or our support team right away to secure your account.
                  </p>
                </td>
              </tr>
            </table>
            {EmailTemplateBase.Divider()}
            <p style='margin:0 0 8px;font-size:13px;color:#9aa0a6;'>For your security, never share your password with anyone.</p>";

            return EmailTemplateBase.Wrap(
                accentColor: "#10b981",
                headerTitle: "Abbsium",
                headerSubtitle: "Security notification",
                bodyContent: body
            );
        }
    }
}

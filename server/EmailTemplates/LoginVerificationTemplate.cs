namespace Server.EmailTemplates
{
    public static class LoginVerificationTemplate
    {
        public static string Build(string code)
        {
            var body = $@"
            <p style='margin:0 0 8px;font-size:22px;font-weight:700;color:#0d1117;letter-spacing:-0.3px;'>Verify your login</p>
            <p style='margin:0 0 28px;font-size:15px;color:#5f6368;line-height:1.7;'>
              Use the following 6-digit code to complete your sign-in. This code expires in <strong style='color:#0d1117;'>10 minutes</strong>.
            </p>

            <!-- Code Display -->
            <table cellpadding='0' cellspacing='0' width='100%' style='margin:0 0 28px;'>
              <tr>
                <td align='center'>
                  <table cellpadding='0' cellspacing='0'>
                    <tr>
                      <td style='background:linear-gradient(135deg,#7c3aed 0%,#6d28d9 100%);padding:24px 40px;border-radius:16px;text-align:center;'>
                        <p style='margin:0;font-size:36px;font-weight:800;color:#ffffff;letter-spacing:8px;font-family:monospace;'>{code}</p>
                      </td>
                    </tr>
                  </table>
                </td>
              </tr>
            </table>

            {EmailTemplateBase.Divider()}

            <p style='margin:0 0 12px;font-size:13px;color:#9aa0a6;line-height:1.6;'>
              If you didn't attempt to sign in, please secure your account immediately:
            </p>
            <p style='margin:0;font-size:13px;color:#ef4444;font-weight:600;line-height:1.6;'>
              • Change your password if you didn't request this code.<br>
              • Enable two-factor authentication for extra security.
            </p>";

            return EmailTemplateBase.Wrap(
                accentColor: "#7c3aed",
                headerTitle: "Abbsium",
                headerSubtitle: "Login Verification",
                bodyContent: body
            );
        }
    }
}

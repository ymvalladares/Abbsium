namespace Server.EmailTemplates
{
    public static class PasswordResetTemplate
    {
        public static string Build(string resetLink)
        {
            var body = $@"
            <p style='margin:0 0 8px;font-size:22px;font-weight:700;color:#0d1117;letter-spacing:-0.3px;'>Reset your password</p>
            <p style='margin:0 0 28px;font-size:15px;color:#5f6368;line-height:1.7;'>
              We received a request to reset your password. Click below to choose a new one. If you didn't request this, no action is needed.
            </p>
            {EmailTemplateBase.Button(resetLink, "Reset my password", "#ef4444")}
            {EmailTemplateBase.Divider()}
            {EmailTemplateBase.FallbackLink(resetLink)}
            <p style='margin:16px 0 0;font-size:12.5px;color:#9aa0a6;'>⏱ This link expires in <strong style='color:#5f6368;'>1 hour</strong>.</p>";

            return EmailTemplateBase.Wrap(
                accentColor: "#ef4444",
                headerTitle: "Abbsium",
                headerSubtitle: "Password reset request",
                bodyContent: body
            );
        }
    }
}

namespace Server.EmailTemplates
{

    public static class EmailConfirmationTemplate
    {
        public static string Build(string confirmLink)
        {
            var body = $@"
            <p style='margin:0 0 8px;font-size:22px;font-weight:700;color:#0d1117;letter-spacing:-0.3px;'>Verify your email address</p>
            <p style='margin:0 0 28px;font-size:15px;color:#5f6368;line-height:1.7;'>
              Thanks for signing up! Confirm your email to activate your Abbsium account and keep it secure.
            </p>
            {EmailTemplateBase.Button(confirmLink, "Confirm my email", "#6366f1")}
            {EmailTemplateBase.Divider()}
            {EmailTemplateBase.FallbackLink(confirmLink)}
            <p style='margin:16px 0 0;font-size:12.5px;color:#9aa0a6;'>⏱ This link expires in <strong style='color:#5f6368;'>24 hours</strong>.</p>";

            return EmailTemplateBase.Wrap(
                accentColor: "#6366f1",
                headerTitle: "Abbsium",
                headerSubtitle: "The platform built for your business",
                bodyContent: body
            );
        }
    }
}

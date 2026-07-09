namespace Server.EmailTemplates
{
    public static class WelcomeTemplate
    {
        public static string Build(string userName, string dashboardLink)
        {
            var body = $@"
            <p style='margin:0 0 8px;font-size:22px;font-weight:700;color:#0d1117;letter-spacing:-0.3px;'>Welcome to Abbsium, {userName}! 🎉</p>
            <p style='margin:0 0 28px;font-size:15px;color:#5f6368;line-height:1.7;'>
              Your account is all set. We're excited to have you on board. Head to your dashboard to get started.
            </p>
            {EmailTemplateBase.Button(dashboardLink, "Go to my dashboard", "#10b981")}";

            return EmailTemplateBase.Wrap(
                accentColor: "#10b981",
                headerTitle: "Abbsium",
                headerSubtitle: "Welcome aboard 🚀",
                bodyContent: body
            );
        }
    }
}

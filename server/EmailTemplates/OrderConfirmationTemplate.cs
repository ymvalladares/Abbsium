namespace Server.EmailTemplates;

public static class OrderConfirmationTemplate
{
    public static string Build(
        string userName,
        string planName,
        string planMode,
        decimal amount,
        string currency,
        string orderId)
    {
        var isSubscription = planMode == "subscription";

        var body = $@"
            <p style='margin:0 0 8px;font-size:22px;font-weight:700;color:#0d1117;letter-spacing:-0.3px;'>
                Your order is confirmed ✅
            </p>
            <p style='margin:0 0 28px;font-size:15px;color:#5f6368;line-height:1.7;'>
                Hi <strong style='color:#0d1117;'>{userName}</strong>, thank you for your purchase! 
                Your <strong style='color:#0d1117;'>{planName}</strong> is now active and ready to use.
            </p>

            <table cellpadding='0' cellspacing='0' width='100%' style='margin-bottom:28px;background:#f8f9ff;border-radius:12px;border:1px solid #e8eaed;'>
                <tr>
                    <td style='padding:20px 24px;'>
                        <table cellpadding='0' cellspacing='0' width='100%'>
                            {Row("Plan", planName)}
                            {Row("Type", isSubscription ? "Monthly Subscription" : "One-time Payment")}
                            {Row("Amount", $"{currency} {amount:F2}")}
                            {Row("Order ID", orderId)}
                            {Row("Date", DateTime.UtcNow.ToString("MMMM dd, yyyy"))}
                            {Row("Status", "<span style='color:#10b981;font-weight:700;'>Completed</span>")}
                        </table>
                    </td>
                </tr>
            </table>

            {(isSubscription ? $@"
            <p style='margin:0 0 20px;font-size:13.5px;color:#5f6368;line-height:1.7;'>
                🔄 Your subscription renews automatically every month. 
                You can cancel anytime from your dashboard.
            </p>" : "")}

            {EmailTemplateBase.Button("https://abbsium.com/platform", "Go to my dashboard", "#6366f1")}
            {EmailTemplateBase.Divider()}

            <p style='margin:0;font-size:12.5px;color:#9aa0a6;line-height:1.6;'>
                If you have any questions about your order, reply to this email or contact us at 
                <a href='mailto:yordan.j.martinez@gmail.com' style='color:#6366f1;'>yordan.j.martinez@gmail.com</a>
            </p>";

        return EmailTemplateBase.Wrap(
            accentColor: "#6366f1",
            headerTitle: "Abbsium",
            headerSubtitle: "Order confirmation",
            bodyContent: body
        );
    }

    private static string Row(string label, string value) => $@"
        <tr>
            <td style='padding:6px 0;font-size:13px;color:#9aa0a6;font-family:Arial,sans-serif;width:120px;'>{label}</td>
            <td style='padding:6px 0;font-size:13px;color:#0d1117;font-weight:600;font-family:Arial,sans-serif;'>{value}</td>
        </tr>";
}
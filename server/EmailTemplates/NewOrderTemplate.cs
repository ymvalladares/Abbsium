namespace Server.EmailTemplates;

public static class NewOrderTemplate
{
    public static string Build(
        string userName,
        string userEmail,
        string planName,
        string planMode,
        decimal amount,
        string currency,
        string orderId)
    {
        var body = $@"
            <p style='margin:0 0 8px;font-size:22px;font-weight:700;color:#0d1117;letter-spacing:-0.3px;'>
                New order received 🎉
            </p>
            <p style='margin:0 0 28px;font-size:15px;color:#5f6368;line-height:1.7;'>
                A user just completed a payment on Abbsium.
            </p>

            <table cellpadding='0' cellspacing='0' width='100%' style='margin-bottom:24px;background:#f8f9ff;border-radius:12px;border:1px solid #e8eaed;'>
                <tr>
                    <td style='padding:20px 24px;'>
                        <table cellpadding='0' cellspacing='0' width='100%'>
                            {Row("User", userEmail)}
                            {Row("Username", userName)}
                            {Row("Plan", planName)}
                            {Row("Type", planMode == "subscription" ? "Monthly Subscription" : "One-time Payment")}
                            {Row("Amount", $"{currency} {amount:F2}")}
                            {Row("Order ID", orderId)}
                            {Row("Date", DateTime.UtcNow.ToString("MMMM dd, yyyy — HH:mm UTC"))}
                        </table>
                    </td>
                </tr>
            </table>

            <p style='margin:0;font-size:13px;color:#9aa0a6;'>
                Log in to your dashboard to see the full order details.
            </p>";

        return EmailTemplateBase.Wrap(
            accentColor: "#10b981",
            headerTitle: "Abbsium",
            headerSubtitle: "New order notification",
            bodyContent: body
        );
    }

    private static string Row(string label, string value) => $@"
        <tr>
            <td style='padding:6px 0;font-size:13px;color:#9aa0a6;font-family:Arial,sans-serif;width:120px;'>{label}</td>
            <td style='padding:6px 0;font-size:13px;color:#0d1117;font-weight:600;font-family:Arial,sans-serif;'>{value}</td>
        </tr>";
}
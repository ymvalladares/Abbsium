using Microsoft.AspNetCore.Mvc;
using Server.Entitys;
using Server.Repositories.IRepositories;
using Stripe;
using System.Text.Json;

namespace Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StripeWebhookController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<StripeWebhookController> _logger;
        private readonly IConfiguration _config;

        public StripeWebhookController(IUnitOfWork unitOfWork, ILogger<StripeWebhookController> logger, IConfiguration config)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _config = config;
        }

        [HttpPost("webhook")]
        public async Task<IActionResult> HandleWebhook()
        {
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();

            try
            {
                var stripeEvent = EventUtility.ConstructEvent(
                    json,
                    Request.Headers["Stripe-Signature"],
                    _config["Stripe:WebhookSecret"]
                );

                _logger.LogInformation("Stripe event received: {EventType}", stripeEvent.Type);

                switch (stripeEvent.Type)
                {
                    case "invoice.payment_succeeded":
                        await HandleInvoicePaymentSucceeded(stripeEvent, json);
                        break;

                    case "invoice.payment_failed":
                        await HandleInvoicePaymentFailed(stripeEvent, json);
                        break;

                    case "customer.subscription.updated":
                        await HandleSubscriptionUpdated(stripeEvent, json);
                        break;

                    case "customer.subscription.deleted":
                        await HandleSubscriptionDeleted(stripeEvent, json);
                        break;

                    case "customer.subscription.created":
                        await HandleSubscriptionCreated(stripeEvent, json);
                        break;

                    case "charge.refunded":
                        await HandleChargeRefunded(stripeEvent);
                        break;

                    default:
                        _logger.LogWarning("Unhandled Stripe event: {EventType}", stripeEvent.Type);
                        break;
                }

                return Ok();
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Stripe webhook error");
                return BadRequest();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Webhook processing error");
                return StatusCode(500);
            }
        }

        private string GetSubIdFromJson(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var obj = doc.RootElement.GetProperty("data").GetProperty("object");
                if (obj.TryGetProperty("subscription", out var subProp) && subProp.ValueKind == JsonValueKind.String)
                    return subProp.GetString();
                if (obj.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String)
                    return idProp.GetString();
            }
            catch { }
            return null;
        }

        private long? GetLongFromJson(JsonElement el, string prop)
        {
            if (el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number)
                return v.GetInt64();
            return null;
        }

        private async Task HandleInvoicePaymentSucceeded(Event stripeEvent, string rawJson)
        {
            var invoice = stripeEvent.Data.Object as Invoice;
            if (invoice == null) return;

            var subId = GetSubIdFromJson(rawJson);
            if (string.IsNullOrEmpty(subId)) return;

            var order = await _unitOfWork.OrderRepository.GetFirstOrDefaultAsync(o => o.SubscriptionId == subId);
            if (order == null) return;

            order.Amount = (decimal)(invoice.AmountPaid / 100M);
            order.Status = "Completed";
            order.LastStripeEvent = "invoice.payment_succeeded";
            order.FailedPaymentAttempts = 0;
            order.StripeInvoiceUrl = invoice.HostedInvoiceUrl;
            order.NextBillingDate = DateTime.UtcNow.AddMonths(1);

            _unitOfWork.OrderRepository.Update(order);

            var paymentHistory = new PaymentHistory
            {
                OrderId = order.Id,
                UserId = order.UserId,
                DealerId = order.DealerId,
                Amount = (decimal)(invoice.AmountPaid / 100M),
                Currency = order.Currency,
                Status = "paid",
                StripeInvoiceId = invoice.Id,
                StripeInvoiceUrl = invoice.HostedInvoiceUrl,
                StripePaymentIntentId = string.Empty,
                CreatedAt = DateTime.UtcNow,
                PaidAt = DateTime.UtcNow,
                FailedAttempts = 0,
                Description = $"Monthly subscription - {order.ServiceType}",
                Notes = $"Period: {invoice.PeriodStart:yyyy-MM-dd} to {invoice.PeriodEnd:yyyy-MM-dd}"
            };

            _unitOfWork.PaymentHistoryRepository.Add(paymentHistory);
            await _unitOfWork.SaveAsync();

            var dealer = order.DealerId.HasValue
                ? await _unitOfWork.DealerRepository.GetFirstOrDefaultAsync(d => d.Id == order.DealerId)
                : await _unitOfWork.DealerRepository.GetFirstOrDefaultAsync(d => d.OwnerId == order.UserId);

            if (dealer != null && !dealer.IsActive)
            {
                dealer.IsActive = true;
                _unitOfWork.DealerRepository.Update(dealer);
            }

            await _unitOfWork.SaveAsync();
        }

        private async Task HandleInvoicePaymentFailed(Event stripeEvent, string rawJson)
        {
            var invoice = stripeEvent.Data.Object as Invoice;
            if (invoice == null) return;

            var subId = GetSubIdFromJson(rawJson);
            if (string.IsNullOrEmpty(subId)) return;

            var order = await _unitOfWork.OrderRepository.GetFirstOrDefaultAsync(o => o.SubscriptionId == subId);
            if (order == null) return;

            order.FailedPaymentAttempts += 1;
            order.Status = "Payment Failed";
            order.LastStripeEvent = "invoice.payment_failed";

            var paymentHistory = new PaymentHistory
            {
                OrderId = order.Id,
                UserId = order.UserId,
                DealerId = order.DealerId,
                Amount = (decimal)(invoice.AmountDue / 100M),
                Currency = order.Currency,
                Status = "failed",
                StripeInvoiceId = invoice.Id,
                StripeInvoiceUrl = invoice.HostedInvoiceUrl,
                StripePaymentIntentId = string.Empty,
                CreatedAt = DateTime.UtcNow,
                FailedAttempts = order.FailedPaymentAttempts,
                Description = $"Monthly subscription - {order.ServiceType} (Payment Failed)",
                Notes = $"Failed attempt {order.FailedPaymentAttempts}/3"
            };

            if (order.FailedPaymentAttempts >= 3)
            {
                order.Status = "Suspended";
                paymentHistory.Status = "suspended";
                paymentHistory.Notes = "Subscription suspended after 3 failed attempts";

                var dealer = order.DealerId.HasValue
                    ? await _unitOfWork.DealerRepository.GetFirstOrDefaultAsync(d => d.Id == order.DealerId)
                    : await _unitOfWork.DealerRepository.GetFirstOrDefaultAsync(d => d.OwnerId == order.UserId);

                if (dealer != null)
                {
                    dealer.IsActive = false;
                    _unitOfWork.DealerRepository.Update(dealer);
                }
            }

            _unitOfWork.OrderRepository.Update(order);
            _unitOfWork.PaymentHistoryRepository.Add(paymentHistory);
            await _unitOfWork.SaveAsync();
        }

        private async Task HandleSubscriptionUpdated(Event stripeEvent, string rawJson)
        {
            var sub = stripeEvent.Data.Object as Subscription;
            if (sub == null) return;

            var order = await _unitOfWork.OrderRepository.GetFirstOrDefaultAsync(o => o.SubscriptionId == sub.Id);
            if (order == null) return;

            order.Status = sub.Status switch
            {
                "active" => "Completed",
                "past_due" => "Past Due",
                "unpaid" => "Unpaid",
                "canceled" => "Cancelled",
                "trialing" => "Trialing",
                _ => sub.Status
            };

            order.CancelAtPeriodEnd = sub.CancelAtPeriodEnd;
            order.LastStripeEvent = "customer.subscription.updated";

            using var doc = JsonDocument.Parse(rawJson);
            var obj = doc.RootElement.GetProperty("data").GetProperty("object");

            var cps = GetLongFromJson(obj, "current_period_start");
            var cpe = GetLongFromJson(obj, "current_period_end");
            var endDate = GetLongFromJson(obj, "ended_at");
            var trialEnd = GetLongFromJson(obj, "trial_end");

            if (cps.HasValue) order.CurrentPeriodStart = DateTimeOffset.FromUnixTimeSeconds(cps.Value).UtcDateTime;
            if (cpe.HasValue) { order.CurrentPeriodEnd = DateTimeOffset.FromUnixTimeSeconds(cpe.Value).UtcDateTime; order.NextBillingDate = order.CurrentPeriodEnd; }
            if (endDate.HasValue) order.SubscriptionEndDate = DateTimeOffset.FromUnixTimeSeconds(endDate.Value).UtcDateTime;
            if (trialEnd.HasValue && trialEnd.Value > 0) order.TrialEnd = DateTimeOffset.FromUnixTimeSeconds(trialEnd.Value).UtcDateTime;

            var items = obj.GetProperty("items").GetProperty("data");
            if (items.GetArrayLength() > 0)
            {
                var item = items[0];
                var plan = item.GetProperty("plan");
                if (plan.TryGetProperty("interval", out var intervalProp) && intervalProp.ValueKind == JsonValueKind.String)
                    order.Interval = intervalProp.GetString();
                if (plan.TryGetProperty("interval_count", out var icProp) && icProp.ValueKind == JsonValueKind.Number)
                    order.IntervalCount = (int)icProp.GetInt64();
                if (item.TryGetProperty("quantity", out var qProp) && qProp.ValueKind == JsonValueKind.Number)
                    order.Quantity = (int)qProp.GetInt64();
            }

            _unitOfWork.OrderRepository.Update(order);
            await _unitOfWork.SaveAsync();
        }

        private async Task HandleSubscriptionCreated(Event stripeEvent, string rawJson)
        {
            var sub = stripeEvent.Data.Object as Subscription;
            if (sub == null) return;

            var order = await _unitOfWork.OrderRepository.GetFirstOrDefaultAsync(o => o.SubscriptionId == sub.Id);
            if (order == null) return;

            using var doc = JsonDocument.Parse(rawJson);
            var obj = doc.RootElement.GetProperty("data").GetProperty("object");

            var cpe = GetLongFromJson(obj, "current_period_end");
            var trialEnd = GetLongFromJson(obj, "trial_end");
            if (cpe.HasValue) order.NextBillingDate = DateTimeOffset.FromUnixTimeSeconds(cpe.Value).UtcDateTime;
            if (trialEnd.HasValue && trialEnd.Value > 0) order.TrialEnd = DateTimeOffset.FromUnixTimeSeconds(trialEnd.Value).UtcDateTime;

            var items = obj.GetProperty("items").GetProperty("data");
            if (items.GetArrayLength() > 0)
            {
                var item = items[0];
                var plan = item.GetProperty("plan");
                if (plan.TryGetProperty("interval", out var intervalProp) && intervalProp.ValueKind == JsonValueKind.String)
                    order.Interval = intervalProp.GetString();
                if (plan.TryGetProperty("interval_count", out var icProp) && icProp.ValueKind == JsonValueKind.Number)
                    order.IntervalCount = (int)icProp.GetInt64();
                if (item.TryGetProperty("quantity", out var qProp) && qProp.ValueKind == JsonValueKind.Number)
                    order.Quantity = (int)qProp.GetInt64();
            }

            _unitOfWork.OrderRepository.Update(order);
            await _unitOfWork.SaveAsync();
        }

        private async Task HandleSubscriptionDeleted(Event stripeEvent, string rawJson)
        {
            var sub = stripeEvent.Data.Object as Subscription;
            if (sub == null) return;

            var order = await _unitOfWork.OrderRepository.GetFirstOrDefaultAsync(o => o.SubscriptionId == sub.Id);
            if (order == null) return;

            order.Status = "Cancelled";
            order.CancelledAt = DateTime.UtcNow;
            order.LastStripeEvent = "customer.subscription.deleted";

            if (sub.CancellationDetails?.Reason != null)
                order.CancellationReason = sub.CancellationDetails.Reason;

            using var doc = JsonDocument.Parse(rawJson);
            var obj = doc.RootElement.GetProperty("data").GetProperty("object");
            var endDate = GetLongFromJson(obj, "ended_at");
            if (endDate.HasValue) order.SubscriptionEndDate = DateTimeOffset.FromUnixTimeSeconds(endDate.Value).UtcDateTime;

            _unitOfWork.OrderRepository.Update(order);

            var dealer = order.DealerId.HasValue
                ? await _unitOfWork.DealerRepository.GetFirstOrDefaultAsync(d => d.Id == order.DealerId)
                : await _unitOfWork.DealerRepository.GetFirstOrDefaultAsync(d => d.OwnerId == order.UserId);

            if (dealer != null)
            {
                dealer.IsActive = false;
                _unitOfWork.DealerRepository.Update(dealer);
            }

            await _unitOfWork.SaveAsync();
        }

        private async Task HandleChargeRefunded(Event stripeEvent)
        {
            var charge = stripeEvent.Data.Object as Charge;
            if (charge == null) return;

            var order = await _unitOfWork.OrderRepository.GetFirstOrDefaultAsync(o => o.PaymentIntentId == charge.PaymentIntentId);
            if (order == null) return;

            order.Status = "Refunded";
            order.LastStripeEvent = "charge.refunded";

            _unitOfWork.OrderRepository.Update(order);
            await _unitOfWork.SaveAsync();
        }
    }
}

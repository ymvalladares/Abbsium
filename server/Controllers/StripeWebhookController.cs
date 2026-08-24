using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Server.Entitys;
using Server.Repositories.IRepositories;
using Stripe;
using System.Text.Json;

namespace Server.Controllers
{
    [Authorize]
    public class StripeWebhookController : Base_Control_Api
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

                _logger.LogInformation("[Stripe] Event received: {EventType} | ID: {EventId}", stripeEvent.Type, stripeEvent.Id);

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
                        _logger.LogWarning("[Stripe] Unhandled event: {EventType}", stripeEvent.Type);
                        break;
                }

                return Ok();
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "[Stripe] Webhook signature/parse error");
                return BadRequest();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Stripe] Webhook processing error");
                return StatusCode(500);
            }
        }

        private string GetSubIdFromJson(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var obj = doc.RootElement.GetProperty("data").GetProperty("object");

                if (obj.TryGetProperty("subscription", out var subProp))
                {
                    if (subProp.ValueKind == JsonValueKind.String)
                        return subProp.GetString();

                    if (subProp.ValueKind == JsonValueKind.Object && subProp.TryGetProperty("id", out var subId))
                        return subId.GetString();
                }

                if (obj.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String)
                    return idProp.GetString();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Stripe] Failed to extract subscription ID from JSON");
            }
            return null;
        }

        private DateTime? GetDateFromJson(JsonElement el, string prop)
        {
            if (el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number)
            {
                var ts = v.GetInt64();
                if (ts > 0)
                    return DateTimeOffset.FromUnixTimeSeconds(ts).UtcDateTime;
            }
            return null;
        }

        private string ExtractPaymentIntentId(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var obj = doc.RootElement.GetProperty("data").GetProperty("object");
                if (obj.TryGetProperty("payment_intent", out var piProp))
                {
                    if (piProp.ValueKind == JsonValueKind.String)
                        return piProp.GetString();
                    if (piProp.ValueKind == JsonValueKind.Object && piProp.TryGetProperty("id", out var piId))
                        return piId.GetString();
                }
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
            if (invoice == null)
            {
                _logger.LogWarning("[Stripe] invoice.payment_succeeded: Invoice object is null");
                return;
            }

            var subId = GetSubIdFromJson(rawJson);
            if (string.IsNullOrEmpty(subId))
            {
                _logger.LogWarning("[Stripe] invoice.payment_succeeded: No subscription ID found. EventId: {EventId}", stripeEvent.Id);
                return;
            }

            var order = await _unitOfWork.OrderRepository.GetFirstOrDefaultAsync(o => o.SubscriptionId == subId);
            if (order == null)
            {
                _logger.LogWarning("[Stripe] invoice.payment_succeeded: No order found for subscription {SubId}. EventId: {EventId}", subId, stripeEvent.Id);
                return;
            }

            var amountPaid = invoice.AmountPaid / 100M;

            order.Amount = (decimal)amountPaid;
            order.Status = "Completed";
            order.LastStripeEvent = "invoice.payment_succeeded";
            order.FailedPaymentAttempts = 0;
            order.StripeInvoiceUrl = invoice.HostedInvoiceUrl;
            order.NextBillingDate = DateTime.UtcNow.AddMonths(1);

            using var doc = JsonDocument.Parse(rawJson);
            var obj = doc.RootElement.GetProperty("data").GetProperty("object");

            var periodStartTs = GetLongFromJson(obj, "period_start");
            var periodEndTs = GetLongFromJson(obj, "period_end");
            var periodStart = periodStartTs.HasValue ? DateTimeOffset.FromUnixTimeSeconds(periodStartTs.Value).UtcDateTime : DateTime.UtcNow;
            var periodEnd = periodEndTs.HasValue ? DateTimeOffset.FromUnixTimeSeconds(periodEndTs.Value).UtcDateTime : DateTime.UtcNow;

            var paymentIntentId = ExtractPaymentIntentId(rawJson);

            var paymentHistory = new PaymentHistory
            {
                OrderId = order.Id,
                UserId = order.UserId,
                DealerId = order.DealerId,
                Amount = (decimal)amountPaid,
                Currency = order.Currency,
                Status = "paid",
                StripeInvoiceId = invoice.Id,
                StripeInvoiceUrl = invoice.HostedInvoiceUrl,
                StripePaymentIntentId = paymentIntentId ?? string.Empty,
                CreatedAt = DateTime.UtcNow,
                PaidAt = DateTime.UtcNow,
                FailedAttempts = 0,
                Description = $"Monthly subscription - {order.ServiceType}",
                Notes = $"Period: {periodStart:yyyy-MM-dd} to {periodEnd:yyyy-MM-dd}"
            };

            _unitOfWork.OrderRepository.Update(order);
            _unitOfWork.PaymentHistoryRepository.Add(paymentHistory);
            await _unitOfWork.SaveAsync();

            var dealer = order.DealerId.HasValue
                ? await _unitOfWork.DealerRepository.GetFirstOrDefaultAsync(d => d.Id == order.DealerId)
                : await _unitOfWork.DealerRepository.GetFirstOrDefaultAsync(d => d.OwnerId == order.UserId);

            if (dealer != null && !dealer.IsActive)
            {
                dealer.IsActive = true;
                _unitOfWork.DealerRepository.Update(dealer);
                await _unitOfWork.SaveAsync();
            }

            _logger.LogInformation("[Stripe] invoice.payment_succeeded: Payment ${Amount} recorded for user {UserId}, subscription {SubId}. PaymentIntent: {PI}",
                amountPaid, order.UserId, subId, paymentIntentId);
        }

        private async Task HandleInvoicePaymentFailed(Event stripeEvent, string rawJson)
        {
            var invoice = stripeEvent.Data.Object as Invoice;
            if (invoice == null)
            {
                _logger.LogWarning("[Stripe] invoice.payment_failed: Invoice object is null");
                return;
            }

            var subId = GetSubIdFromJson(rawJson);
            if (string.IsNullOrEmpty(subId))
            {
                _logger.LogWarning("[Stripe] invoice.payment_failed: No subscription ID found. EventId: {EventId}", stripeEvent.Id);
                return;
            }

            var order = await _unitOfWork.OrderRepository.GetFirstOrDefaultAsync(o => o.SubscriptionId == subId);
            if (order == null)
            {
                _logger.LogWarning("[Stripe] invoice.payment_failed: No order found for subscription {SubId}", subId);
                return;
            }

            order.FailedPaymentAttempts += 1;
            order.Status = "Payment Failed";
            order.LastStripeEvent = "invoice.payment_failed";

            var paymentIntentId = ExtractPaymentIntentId(rawJson);

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
                StripePaymentIntentId = paymentIntentId ?? string.Empty,
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

            _logger.LogWarning("[Stripe] invoice.payment_failed: Order {OrderId}, attempt {Attempt}/3, status: {Status}",
                order.Id, order.FailedPaymentAttempts, order.Status);
        }

        private async Task HandleSubscriptionUpdated(Event stripeEvent, string rawJson)
        {
            var sub = stripeEvent.Data.Object as Subscription;
            if (sub == null)
            {
                _logger.LogWarning("[Stripe] customer.subscription.updated: Subscription object is null");
                return;
            }

            var order = await _unitOfWork.OrderRepository.GetFirstOrDefaultAsync(o => o.SubscriptionId == sub.Id);
            if (order == null)
            {
                _logger.LogWarning("[Stripe] customer.subscription.updated: No order found for subscription {SubId}", sub.Id);
                return;
            }

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

            _logger.LogInformation("[Stripe] customer.subscription.updated: Order {OrderId}, status: {Status}, cancelAtPeriodEnd: {Cancel}",
                order.Id, order.Status, order.CancelAtPeriodEnd);
        }

        private async Task HandleSubscriptionCreated(Event stripeEvent, string rawJson)
        {
            var sub = stripeEvent.Data.Object as Subscription;
            if (sub == null)
            {
                _logger.LogWarning("[Stripe] customer.subscription.created: Subscription object is null");
                return;
            }

            var order = await _unitOfWork.OrderRepository.GetFirstOrDefaultAsync(o => o.SubscriptionId == sub.Id);
            if (order == null)
            {
                _logger.LogWarning("[Stripe] customer.subscription.created: No order found for subscription {SubId}", sub.Id);
                return;
            }

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
            if (sub == null)
            {
                _logger.LogWarning("[Stripe] customer.subscription.deleted: Subscription object is null");
                return;
            }

            var order = await _unitOfWork.OrderRepository.GetFirstOrDefaultAsync(o => o.SubscriptionId == sub.Id);
            if (order == null)
            {
                _logger.LogWarning("[Stripe] customer.subscription.deleted: No order found for subscription {SubId}", sub.Id);
                return;
            }

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

            _logger.LogInformation("[Stripe] customer.subscription.deleted: Order {OrderId} cancelled. Reason: {Reason}",
                order.Id, order.CancellationReason ?? "Unknown");
        }

        private async Task HandleChargeRefunded(Event stripeEvent)
        {
            var charge = stripeEvent.Data.Object as Charge;
            if (charge == null)
            {
                _logger.LogWarning("[Stripe] charge.refunded: Charge object is null");
                return;
            }

            var order = await _unitOfWork.OrderRepository.GetFirstOrDefaultAsync(o => o.PaymentIntentId == charge.PaymentIntentId);
            if (order == null)
            {
                _logger.LogWarning("[Stripe] charge.refunded: No order found for PaymentIntent {PI}", charge.PaymentIntentId);
                return;
            }

            order.Status = "Refunded";
            order.LastStripeEvent = "charge.refunded";

            _unitOfWork.OrderRepository.Update(order);
            await _unitOfWork.SaveAsync();
        }
    }
}

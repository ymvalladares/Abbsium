using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Server.EmailTemplates;
using Server.Entitys;
using Server.Repositories.IRepositories;
using Stripe;
using Stripe.Checkout;
using System.Security.Claims;

namespace Server.Controllers
{
    [Authorize]
    public class OrderController : Base_Control_Api
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmailSender _emailSender;
        private readonly IConfiguration _config;
        private readonly UserManager<User_data> _userManager;

        public OrderController(IUnitOfWork unitOfWork, IEmailSender emailSender, IConfiguration config, UserManager<User_data> userManager)
        {
            _unitOfWork = unitOfWork;
            _emailSender = emailSender;
            _config = config;
            _userManager = userManager;
        }

        [HttpPost("create-checkout-session")]
        public async Task<IActionResult> CreateCheckoutSession([FromBody] CreatePaymentRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (request.Amount <= 0)
                return BadRequest(new { message = "Amount must be greater than 0." });

            if (request.Amount > 999999)
                return BadRequest(new { message = "Amount exceeds maximum allowed." });

            if (request.Mode != "payment" && request.Mode != "subscription")
                return BadRequest(new { message = "Invalid mode. Use 'payment' or 'subscription'." });

            var baseUrl = _config["ClientUrl"] ?? "http://localhost:3000";

            SessionCreateOptions options;

            if (request.Mode == "payment")
            {
                options = new SessionCreateOptions
                {
                    PaymentMethodTypes = new List<string> { "card" },
                    Mode = "payment",
                    LineItems = new List<SessionLineItemOptions>
            {
                new()
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency   = "usd",
                        UnitAmount = (long)(request.Amount * 100),
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name        = GetPlanName(request.ServiceType),
                            Description = GetPlanDescription(request.ServiceType, request.Amount, request.Mode),
                            Images      = new List<string> { GetPlanImage(request.ServiceType) }
                        }
                    },
                    Quantity = 1
                }
            },
                    SuccessUrl = $"{baseUrl}/platform/success-payment?session_id={{CHECKOUT_SESSION_ID}}",
                    CancelUrl = $"{baseUrl}/platform/payment-denied",
                    Metadata = new Dictionary<string, string>
            {
                { "userId",      userId              },
                { "serviceType", request.ServiceType },
                { "planMode",    "payment"           },
                { "plan_type",   "one-time"          }
            }
                };
            }
            else
            {
                options = new SessionCreateOptions
                {
                    PaymentMethodTypes = new List<string> { "card" },
                    Mode = "subscription",
                    LineItems = new List<SessionLineItemOptions>
            {
                new()
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency   = "usd",
                        UnitAmount = (long)(request.Amount * 100),
                        Recurring  = new SessionLineItemPriceDataRecurringOptions
                        {
                            Interval = "month"
                        },
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name        = GetPlanName(request.ServiceType),
                            Description = GetPlanDescription(request.ServiceType, request.Amount, request.Mode),
                            Images      = new List<string> { GetPlanImage(request.ServiceType) }
                        }
                    },
                    Quantity = 1
                }
            },
                    SuccessUrl = $"{baseUrl}/platform/success-payment?session_id={{CHECKOUT_SESSION_ID}}",
                    CancelUrl = $"{baseUrl}/platform/payment-denied",
                    Metadata = new Dictionary<string, string>
            {
                { "userId",      userId              },
                { "serviceType", request.ServiceType },
                { "planMode",    "subscription"      },
                { "plan_type",   "subscription"      }
            }
                };
            }

            var service = new SessionService();
            var session = await service.CreateAsync(options);

            return Ok(new { sessionId = session.Id, sessionUrl = session.Url });
        }

        private static string GetPlanName(string serviceType) =>
            serviceType.ToLower() switch
            {
                "starter" => "Starter Plan",
                "professional" => "Professional Plan",
                "enterprise" => "Enterprise Plan",
                "dealer" => "Dealer Plan",
                _ => serviceType
            };

        private static string GetPlanDescription(string serviceType, decimal amount, string mode)
        {
            var name = GetPlanName(serviceType);
            var formatted = amount.ToString("F2");
            var prefix = mode == "subscription" ? $"${formatted}/mo · Monthly subscription" : $"${formatted} · One-time payment";
            return $"{prefix} · {name}";
        }

        private static string GetPlanImage(string serviceType) =>
            serviceType.ToLower() switch
            {
                "starter" => "https://images.unsplash.com/photo-1499750310107-5fef28a66643?w=800&auto=format&fit=crop",
                "professional" => "https://images.unsplash.com/photo-1460925895917-afdab827c52f?w=800&auto=format&fit=crop",
                "enterprise" => "https://images.unsplash.com/photo-1504868584819-f8e8b4b6d7e3?w=800&auto=format&fit=crop",
                "dealer" => "https://images.unsplash.com/photo-1562519776-b232588f8000?w=800&auto=format&fit=crop",
                _ => "https://images.unsplash.com/photo-1460925895917-afdab827c52f?w=800&auto=format&fit=crop"
            };

        [HttpPost("verify")]
        public async Task<IActionResult> VerifyPayment([FromBody] PaymentVerificationRequest request)
        {
            var sessionService = new SessionService();
            var session = await sessionService.GetAsync(request.SessionId, new SessionGetOptions
            {
                Expand = new List<string> { "subscription", "customer", "payment_intent", "subscription.latest_invoice", "subscription.default_payment_method" }
            });

            var isPaid = session.PaymentStatus == "paid" ||
                         session.PaymentStatus == "no_payment_required";

            if (!isPaid)
                return BadRequest(new { message = "Payment failed or incomplete." });

            var userId = session.Metadata?.GetValueOrDefault("userId");
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest(new { message = "Missing user information in session." });
            }

            var serviceType = session.Metadata?.GetValueOrDefault("serviceType");
            var planMode = session.Metadata.GetValueOrDefault("planMode", "payment");
            var amount = (decimal)(session.AmountTotal ?? 0) / 100M;

            string? paymentIntentId = session.PaymentIntentId ?? string.Empty;

            var dedupeId = planMode == "subscription"
                ? session.SubscriptionId
                : paymentIntentId;

            var existing = await _unitOfWork.OrderRepository
                .GetFirstOrDefaultAsync(o => o.PaymentIntentId == dedupeId || o.SubscriptionId == dedupeId);

            if (existing == null)
            {
                var dealer = await _unitOfWork.DealerRepository.GetFirstOrDefaultAsync(d => d.OwnerId == userId);
                var user = await _unitOfWork.UserRepository.GetFirstOrDefaultAsync(u => u.Id == userId);

                string stripeBrand = string.Empty;
                string stripeLast4 = string.Empty;
                string stripeExpMonth = string.Empty;
                string stripeExpYear = string.Empty;
                string stripeInvoiceUrl = string.Empty;

                if (planMode == "subscription" && session.Subscription != null)
                {
                    var card = session.Subscription.DefaultPaymentMethod?.Card;
                    if (card != null)
                    {
                        stripeBrand = card.Brand;
                        stripeLast4 = card.Last4;
                        stripeExpMonth = card.ExpMonth.ToString();
                        stripeExpYear = card.ExpYear.ToString();
                    }

                    var latestInvoice = session.Subscription.LatestInvoice;
                    if (latestInvoice != null)
                    {
                        stripeInvoiceUrl = latestInvoice.HostedInvoiceUrl ?? string.Empty;
                    }
                }

                var order = new Order
                {
                    UserId = userId,
                    DealerId = dealer?.Id,
                    Amount = amount,
                    Currency = session.Currency ?? "usd",
                    ServiceType = serviceType ?? string.Empty,
                    PaymentIntentId = paymentIntentId ?? string.Empty,
                    SubscriptionId = session.SubscriptionId ?? string.Empty,
                    StripeCustomerId = session.CustomerId ?? string.Empty,
                    StripeCustomerEmail = session.Metadata.TryGetValue("userEmail", out var email) ? email : (user?.Email ?? string.Empty),
                    StripePaymentMethod = "card",
                    StripeBrand = stripeBrand,
                    StripeLast4 = stripeLast4,
                    StripeExpMonth = stripeExpMonth,
                    StripeExpYear = stripeExpYear,
                    StripeInvoiceUrl = stripeInvoiceUrl,
                    Status = "Completed",
                    PlanMode = planMode
                };

                if (planMode == "subscription" && session.Subscription != null)
                {
                    order.Interval = "month";
                    order.IntervalCount = 1;
                    order.Quantity = (int?)(session.Subscription.Items?.Data?.FirstOrDefault()?.Quantity) ?? 1;
                    order.NextBillingDate = DateTime.UtcNow.AddMonths(1);
                }

                _unitOfWork.OrderRepository.Add(order);
                await _unitOfWork.SaveAsync();

                var paymentHistory = new PaymentHistory
                {
                    OrderId = order.Id,
                    UserId = userId,
                    DealerId = dealer?.Id,
                    Amount = amount,
                    Currency = order.Currency,
                    Status = "paid",
                    StripeInvoiceId = string.Empty,
                    StripeInvoiceUrl = stripeInvoiceUrl,
                    StripePaymentIntentId = paymentIntentId ?? string.Empty,
                    CreatedAt = DateTime.UtcNow,
                    PaidAt = DateTime.UtcNow,
                    FailedAttempts = 0,
                    Description = $"{GetPlanName(serviceType ?? string.Empty)} - {planMode}",
                    Notes = "Initial payment"
                };

                _unitOfWork.PaymentHistoryRepository.Add(paymentHistory);
                await _unitOfWork.SaveAsync();

                await _emailSender.SendEmail(new Email
                {
                    To = _config["AdminUser:adminEmail"],
                    Subject = $"New Order — {GetPlanName(serviceType ?? string.Empty)}",
                    Body = NewOrderTemplate.Build(
                        userName: user?.UserName ?? userId,
                        userEmail: user?.Email ?? "unknown",
                        planName: GetPlanName(serviceType ?? string.Empty),
                        planMode: planMode,
                        amount: amount,
                        currency: (session.Currency ?? "usd").ToUpper(),
                        orderId: dedupeId ?? "N/A"
                    )
                });

                await _emailSender.SendEmail(new Email
                {
                    To = user?.Email ?? "unknown",
                    Subject = $"Your {GetPlanName(serviceType ?? string.Empty)} order is confirmed!",
                    Body = OrderConfirmationTemplate.Build(
                        userName: user?.UserName ?? "there",
                        planName: GetPlanName(serviceType ?? string.Empty),
                        planMode: planMode,
                        amount: amount,
                        currency: (session.Currency ?? "usd").ToUpper(),
                        orderId: dedupeId ?? "N/A"
                    )
                });

                if (dealer != null && planMode == "subscription")
                {
                    dealer.IsActive = true;
                    _unitOfWork.DealerRepository.Update(dealer);
                    await _unitOfWork.SaveAsync();
                }
            }

            return Ok(new
            {
                message = "Payment verified.",
                planMode,
                serviceType
            });
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<IEnumerable<Order>>> GetAllOrders(
            [FromQuery] string? status,
            [FromQuery] string? planMode,
            [FromQuery] Guid? dealerId,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var orders = await _unitOfWork.OrderRepository.GetAllAsync(
                o => (string.IsNullOrEmpty(status) || o.Status == status)
                  && (string.IsNullOrEmpty(planMode) || o.PlanMode == planMode)
                  && (!dealerId.HasValue || o.DealerId == dealerId)
                  && (!startDate.HasValue || o.CreatedAt >= startDate)
                  && (!endDate.HasValue || o.CreatedAt <= endDate),
                o => o.User_data,
                o => o.Dealer
            );

            var ordered = orders.OrderByDescending(o => o.CreatedAt);

            var total = ordered.Count();
            var paged = ordered.Skip((page - 1) * pageSize).Take(pageSize);

            return Ok(new
            {
                orders = paged,
                total,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling((double)total / pageSize)
            });
        }

        [HttpGet("ById/{id}")]
        public async Task<ActionResult<Order>> GetOrderById(Guid id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
            var isAdmin = roles.Contains("Admin");

            var order = await _unitOfWork.OrderRepository
                .GetFirstOrDefaultAsync(x => x.Id == id, x => x.User_data, x => x.Dealer);
            if (order == null) return NotFound("Order not found");

            if (!isAdmin && order.UserId != userId)
                return Unauthorized("You do not have permission to view this order");

            return Ok(order);
        }

        [HttpGet("ByDealer/{dealerId}")]
        public async Task<ActionResult<IEnumerable<Order>>> GetOrdersByDealer(Guid dealerId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
            var isAdmin = roles.Contains("Admin");

            var dealer = await _unitOfWork.DealerRepository.GetFirstOrDefaultAsync(x => x.Id == dealerId);
            if (dealer == null) return NotFound("Dealer not found");

            if (!isAdmin && dealer.OwnerId != userId)
                return Unauthorized("You do not have permission to view these orders");

            var orders = await _unitOfWork.OrderRepository.GetAllAsync(
                o => o.DealerId == dealerId,
                o => o.User_data
            );
            return Ok(orders.OrderByDescending(o => o.CreatedAt));
        }

        [HttpGet("stats")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetOrderStats()
        {
            var allOrders = await _unitOfWork.OrderRepository.GetAllAsync();
            var ordersList = allOrders.ToList();

            var totalOrders = ordersList.Count;
            var totalRevenue = ordersList.Where(o => o.Status == "Completed").Sum(o => o.Amount);
            var activeSubscriptions = ordersList.Count(o => o.Status == "Completed" && o.PlanMode == "subscription" && o.SubscriptionId != string.Empty);
            var pendingOrders = ordersList.Count(o => o.Status == "pending" || o.Status == "Past Due");
            var failedPayments = ordersList.Count(o => o.Status == "Payment Failed");
            var cancelledSubscriptions = ordersList.Count(o => o.Status == "Cancelled");

            var monthlyRevenue = ordersList
                .Where(o => o.Status == "Completed" && o.CreatedAt.Month == DateTime.UtcNow.Month && o.CreatedAt.Year == DateTime.UtcNow.Year)
                .Sum(o => o.Amount);

            return Ok(new
            {
                totalOrders,
                totalRevenue,
                activeSubscriptions,
                pendingOrders,
                failedPayments,
                cancelledSubscriptions,
                monthlyRevenue
            });
        }

        [HttpPost("cancel-subscription/{orderId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CancelSubscription(Guid orderId, [FromBody] CancelSubscriptionRequest request)
        {
            var order = await _unitOfWork.OrderRepository.GetFirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null) return NotFound("Order not found");

            if (string.IsNullOrEmpty(order.SubscriptionId))
                return BadRequest("This order is not a subscription");

            try
            {
                var subscriptionService = new SubscriptionService();
                await subscriptionService.CancelAsync(order.SubscriptionId, new SubscriptionCancelOptions
                {
                    InvoiceNow = request.InvoiceNow ?? false,
                    Prorate = request.Prorate ?? true
                });

                order.Status = "Cancelled";
                order.CancelledAt = DateTime.UtcNow;
                order.CancellationReason = request.Reason ?? "Cancelled by admin";
                order.SubscriptionEndDate = DateTime.UtcNow;
                order.LastStripeEvent = "admin.cancelled";

                _unitOfWork.OrderRepository.Update(order);

                var dealer = order.DealerId.HasValue
                    ? await _unitOfWork.DealerRepository.GetFirstOrDefaultAsync(d => d.Id == order.DealerId)
                    : await _unitOfWork.DealerRepository.GetFirstOrDefaultAsync(d => d.OwnerId == order.UserId);

                if (dealer != null)
                {
                    dealer.IsActive = false;
                    _unitOfWork.DealerRepository.Update(dealer);
                }

                var cancelPayment = new PaymentHistory
                {
                    OrderId = order.Id,
                    UserId = order.UserId,
                    DealerId = dealer?.Id,
                    Amount = order.Amount,
                    Currency = order.Currency,
                    Status = "cancelled",
                    StripeInvoiceId = string.Empty,
                    StripeInvoiceUrl = string.Empty,
                    StripePaymentIntentId = string.Empty,
                    CreatedAt = DateTime.UtcNow,
                    FailedAttempts = 0,
                    Description = $"Subscription cancelled by admin - {order.ServiceType}",
                    Notes = $"Reason: {request.Reason ?? "Cancelled by admin"}"
                };

                _unitOfWork.PaymentHistoryRepository.Add(cancelPayment);
                await _unitOfWork.SaveAsync();

                return Ok(new { message = "Subscription cancelled successfully" });
            }
            catch (StripeException ex)
            {
                return StatusCode(500, new { message = "Failed to cancel subscription", error = ex.Message });
            }
        }

        [HttpPost("reactivate-subscription/{orderId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ReactivateSubscription(Guid orderId)
        {
            var order = await _unitOfWork.OrderRepository.GetFirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null) return NotFound("Order not found");

            if (string.IsNullOrEmpty(order.SubscriptionId))
                return BadRequest("This order is not a subscription");

            try
            {
                var subscriptionService = new SubscriptionService();
                var subscription = await subscriptionService.UpdateAsync(order.SubscriptionId, new SubscriptionUpdateOptions
                {
                    CancelAtPeriodEnd = false
                });

                order.Status = "Completed";
                order.CancelledAt = null;
                order.CancellationReason = string.Empty;
                order.NextBillingDate = DateTime.UtcNow.AddMonths(1);
                order.LastStripeEvent = "admin.reactivated";

                _unitOfWork.OrderRepository.Update(order);
                await _unitOfWork.SaveAsync();

                if (order.DealerId.HasValue)
                {
                    var dealer = await _unitOfWork.DealerRepository.GetFirstOrDefaultAsync(d => d.Id == order.DealerId);
                    if (dealer != null)
                    {
                        dealer.IsActive = true;
                        _unitOfWork.DealerRepository.Update(dealer);
                        await _unitOfWork.SaveAsync();
                    }
                }

                return Ok(new { message = "Subscription reactivated successfully" });
            }
            catch (StripeException ex)
            {
                return StatusCode(500, new { message = "Failed to reactivate subscription", error = ex.Message });
            }
        }

        [HttpPost("add-note/{orderId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AddNote(Guid orderId, [FromBody] AddNoteRequest request)
        {
            var order = await _unitOfWork.OrderRepository.GetFirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null) return NotFound("Order not found");

            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm");
            var note = $"[{timestamp}] {request.Note}";

            order.Notes = string.IsNullOrEmpty(order.Notes) ? note : $"{order.Notes}\n{note}";

            _unitOfWork.OrderRepository.Update(order);
            await _unitOfWork.SaveAsync();

            return Ok(new { message = "Note added successfully" });
        }

        [HttpGet("subscription-details/{orderId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetSubscriptionDetails(Guid orderId)
        {
            var order = await _unitOfWork.OrderRepository.GetFirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null) return NotFound("Order not found");

            if (string.IsNullOrEmpty(order.SubscriptionId))
                return BadRequest("This order is not a subscription");

            try
            {
                var subscriptionService = new SubscriptionService();
                var subscription = await subscriptionService.GetAsync(order.SubscriptionId, new SubscriptionGetOptions
                {
                    Expand = new List<string> { "customer", "latest_invoice", "latest_invoice.payment_intent", "default_payment_method" }
                });

                var firstItem = subscription.Items?.Data?.FirstOrDefault();
                var plan = firstItem?.Plan;

                var result = new
                {
                    subscriptionId = subscription.Id,
                    status = subscription.Status,
                    customerId = subscription.CustomerId,
                    customerEmail = (subscription.Customer as Stripe.Customer)?.Email,
                    customerName = (subscription.Customer as Stripe.Customer)?.Name,
                    currentPeriodStart = order.CurrentPeriodStart,
                    currentPeriodEnd = order.CurrentPeriodEnd,
                    cancelAtPeriodEnd = subscription.CancelAtPeriodEnd,
                    canceledAt = subscription.CanceledAt,
                    endedAt = subscription.EndedAt,
                    trialStart = subscription.TrialStart,
                    trialEnd = subscription.TrialEnd,
                    quantity = firstItem?.Quantity,
                    interval = plan?.Interval,
                    intervalCount = plan?.IntervalCount,
                    amount = plan?.Amount != null ? (decimal)(plan.Amount.Value / 100M) : (decimal?)null,
                    currency = plan?.Currency,
                    invoiceUrl = subscription.LatestInvoice?.HostedInvoiceUrl,
                    paymentMethod = subscription.DefaultPaymentMethod != null
                        ? new
                        {
                            type = subscription.DefaultPaymentMethod.Type,
                            brand = subscription.DefaultPaymentMethod.Card?.Brand,
                            last4 = subscription.DefaultPaymentMethod.Card?.Last4,
                            expMonth = subscription.DefaultPaymentMethod.Card?.ExpMonth,
                            expYear = subscription.DefaultPaymentMethod.Card?.ExpYear
                        }
                        : null,
                    metadata = subscription.Metadata,
                    created = subscription.Created
                };

                return Ok(result);
            }
            catch (StripeException ex)
            {
                return StatusCode(500, new { message = "Failed to fetch subscription details", error = ex.Message });
            }
        }

        [HttpPost("sync-subscription/{orderId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SyncSubscription(Guid orderId)
        {
            var order = await _unitOfWork.OrderRepository.GetFirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null) return NotFound("Order not found");

            if (string.IsNullOrEmpty(order.SubscriptionId))
                return BadRequest("This order is not a subscription");

            try
            {
                var subscriptionService = new SubscriptionService();
                var subscription = await subscriptionService.GetAsync(order.SubscriptionId, new SubscriptionGetOptions
                {
                    Expand = new List<string> { "latest_invoice", "default_payment_method" }
                });

                order.Status = subscription.Status switch
                {
                    "active" => "Completed",
                    "past_due" => "Past Due",
                    "unpaid" => "Unpaid",
                    "canceled" => "Cancelled",
                    "trialing" => "Trialing",
                    _ => subscription.Status
                };

                order.CancelAtPeriodEnd = subscription.CancelAtPeriodEnd;
                var firstItem = subscription.Items?.Data?.FirstOrDefault();
                var plan = firstItem?.Plan;
                order.Interval = plan?.Interval;
                order.IntervalCount = plan?.IntervalCount != null ? (int?)plan.IntervalCount : null;
                order.Quantity = firstItem?.Quantity != null ? (int?)firstItem.Quantity : null;

                if (subscription.LatestInvoice?.HostedInvoiceUrl != null)
                    order.StripeInvoiceUrl = subscription.LatestInvoice.HostedInvoiceUrl;

                if (subscription.DefaultPaymentMethod?.Card != null)
                {
                    var card = subscription.DefaultPaymentMethod.Card;
                    order.StripePaymentMethod = "card";
                    order.StripeBrand = card.Brand;
                    order.StripeLast4 = card.Last4;
                    order.StripeExpMonth = card.ExpMonth.ToString();
                    order.StripeExpYear = card.ExpYear.ToString();
                }

                order.LastStripeEvent = "admin.synced";

                _unitOfWork.OrderRepository.Update(order);
                await _unitOfWork.SaveAsync();

                return Ok(new { message = "Subscription synced successfully", order });
            }
            catch (StripeException ex)
            {
                return StatusCode(500, new { message = "Failed to sync subscription", error = ex.Message });
            }
        }

        [HttpGet("payment-history")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetPaymentHistory(
            [FromQuery] string? userId,
            [FromQuery] Guid? orderId,
            [FromQuery] string? status,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            var payments = await _unitOfWork.PaymentHistoryRepository.GetAllAsync(
                ph => (string.IsNullOrEmpty(userId) || ph.UserId == userId)
                   && (!orderId.HasValue || ph.OrderId == orderId)
                   && (string.IsNullOrEmpty(status) || ph.Status == status)
                   && (!startDate.HasValue || ph.CreatedAt >= startDate)
                   && (!endDate.HasValue || ph.CreatedAt <= endDate),
                ph => ph.Order
            );

            var ordered = payments.OrderByDescending(ph => ph.CreatedAt);
            var total = ordered.Count();
            var paged = ordered.Skip((page - 1) * pageSize).Take(pageSize);

            var result = paged.Select(ph => new
            {
                id = ph.Id,
                orderId = ph.OrderId,
                userId = ph.UserId,
                dealerId = ph.DealerId,
                amount = ph.Amount,
                currency = ph.Currency,
                status = ph.Status,
                stripeInvoiceId = ph.StripeInvoiceId,
                stripeInvoiceUrl = ph.StripeInvoiceUrl,
                stripePaymentIntentId = ph.StripePaymentIntentId,
                createdAt = ph.CreatedAt,
                paidAt = ph.PaidAt,
                failedAttempts = ph.FailedAttempts,
                description = ph.Description,
                notes = ph.Notes,
                orderStatus = ph.Order?.Status,
                orderSubscriptionId = ph.Order?.SubscriptionId
            });

            return Ok(new
            {
                payments = result,
                total,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling((double)total / pageSize)
            });
        }

        [HttpGet("payment-history/user/{userId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetUserPaymentHistory(string userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            var payments = await _unitOfWork.PaymentHistoryRepository.GetAllAsync(
                ph => ph.UserId == userId,
                ph => ph.Order
            );

            var ordered = payments.OrderByDescending(ph => ph.CreatedAt);
            var total = ordered.Count();
            var paged = ordered.Skip((page - 1) * pageSize).Take(pageSize);

            var result = paged.Select(ph => new
            {
                id = ph.Id,
                orderId = ph.OrderId,
                amount = ph.Amount,
                currency = ph.Currency,
                status = ph.Status,
                stripeInvoiceUrl = ph.StripeInvoiceUrl,
                createdAt = ph.CreatedAt,
                paidAt = ph.PaidAt,
                failedAttempts = ph.FailedAttempts,
                description = ph.Description,
                notes = ph.Notes
            });

            return Ok(new
            {
                payments = result,
                total,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling((double)total / pageSize)
            });
        }

        [HttpPost("sync-payments/{orderId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SyncPayments(Guid orderId)
        {
            var order = await _unitOfWork.OrderRepository.GetFirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null)
                return NotFound("Order not found");
            if (string.IsNullOrEmpty(order.SubscriptionId))
                return BadRequest("Order has no subscription");

            try
            {
                var stripeInvoiceService = new Stripe.InvoiceService();
                var invoices = await stripeInvoiceService.ListAsync(new Stripe.InvoiceListOptions
                {
                    Subscription = order.SubscriptionId,
                    Limit = 100
                });

                var synced = 0;
                foreach (var invoice in invoices)
                {
                    var existing = await _unitOfWork.PaymentHistoryRepository.GetFirstOrDefaultAsync(ph => ph.StripeInvoiceId == invoice.Id);
                    if (existing != null) continue;

                    var paymentHistory = new PaymentHistory
                    {
                        OrderId = order.Id,
                        UserId = order.UserId,
                        DealerId = order.DealerId,
                        Amount = (decimal)(invoice.AmountPaid / 100M),
                        Currency = order.Currency,
                        Status = invoice.Status switch
                        {
                            "paid" => "paid",
                            "open" => "pending",
                            _ => "failed"
                        },
                        StripeInvoiceId = invoice.Id,
                        StripeInvoiceUrl = invoice.HostedInvoiceUrl,
                        StripePaymentIntentId = string.Empty,
                        CreatedAt = invoice.Created,
                        PaidAt = invoice.Status == "paid" ? invoice.Created : null,
                        FailedAttempts = 0,
                        Description = $"Synced - {order.ServiceType} subscription",
                        Notes = $"Invoice: {invoice.Number ?? "N/A"}"
                    };

                    _unitOfWork.PaymentHistoryRepository.Add(paymentHistory);
                    synced++;
                }

                if (synced > 0)
                {
                    order.LastStripeEvent = "manual_sync";
                    _unitOfWork.OrderRepository.Update(order);
                }

                await _unitOfWork.SaveAsync();

                return Ok(new { message = $"Synced {synced} missing payment(s)", syncedCount = synced });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to sync payments", error = ex.Message });
            }
        }

        [HttpGet("payment-overview")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetPaymentOverview(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] string? status)
        {
            var payments = await _unitOfWork.PaymentHistoryRepository.GetAllAsync(
                ph => (!startDate.HasValue || ph.CreatedAt >= startDate)
                   && (!endDate.HasValue || ph.CreatedAt <= endDate)
                   && (string.IsNullOrEmpty(status) || ph.Status == status),
                ph => ph.Order
            );

            var totalPayments = payments.Count();
            var totalRevenue = payments.Where(p => p.Status == "paid").Sum(p => (double)p.Amount);
            var totalFailed = payments.Where(p => p.Status == "failed" || p.Status == "suspended").Count();
            var totalFailedAmount = payments.Where(p => p.Status == "failed" || p.Status == "suspended").Sum(p => (double)p.Amount);

            var byStatus = payments.GroupBy(p => p.Status).Select(g => new
            {
                status = g.Key,
                count = g.Count(),
                amount = g.Sum(p => (double)p.Amount)
            }).ToList();

            var byServiceType = payments.Where(p => p.Status == "paid")
                .GroupBy(p => p.Order?.ServiceType ?? "Unknown")
                .Select(g => new { serviceType = g.Key, count = g.Count(), amount = g.Sum(p => (double)p.Amount) })
                .OrderByDescending(x => x.amount)
                .ToList();

            var byMonth = payments.Where(p => p.Status == "paid")
                .GroupBy(p => new { Year = p.CreatedAt.Year, Month = p.CreatedAt.Month })
                .Select(g => new
                {
                    month = $"{g.Key.Year}-{g.Key.Month:D2}",
                    count = g.Count(),
                    amount = g.Sum(p => (double)p.Amount)
                })
                .OrderBy(x => x.month)
                .ToList();

            return Ok(new
            {
                totalPayments,
                totalRevenue,
                totalFailed,
                totalFailedAmount,
                byStatus,
                byServiceType,
                byMonth
            });
        }

        [HttpGet("user-payments")]
        public async Task<IActionResult> GetUserPayments()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized("Invalid token");

            var payments = await _unitOfWork.PaymentHistoryRepository.GetAllAsync(
                ph => ph.UserId == userId,
                ph => ph.Order
            );

            var activeSubscriptions = (await _unitOfWork.OrderRepository.GetAllAsync(o => o.UserId == userId && o.SubscriptionId != null))
                .Where(o => o.Status == "Completed" || o.Status == "Trialing")
                .Select(o => new
                {
                    orderId = o.Id,
                    serviceType = o.ServiceType,
                    planMode = o.PlanMode,
                    status = o.Status,
                    nextBillingDate = o.NextBillingDate,
                    amount = o.Amount,
                    currency = o.Currency,
                    subscriptionId = o.SubscriptionId
                })
                .ToList();

            var paymentsResult = payments.OrderByDescending(ph => ph.CreatedAt).Select(ph => new
            {
                id = ph.Id,
                orderId = ph.OrderId,
                amount = ph.Amount,
                currency = ph.Currency,
                status = ph.Status,
                stripeInvoiceUrl = ph.StripeInvoiceUrl,
                stripePaymentIntentId = ph.StripePaymentIntentId,
                createdAt = ph.CreatedAt,
                paidAt = ph.PaidAt,
                description = ph.Description,
                notes = ph.Notes,
                serviceType = ph.Order?.ServiceType
            }).ToList();

            var totalSpent = payments.Where(p => p.Status == "paid").Sum(p => (double)p.Amount);

            return Ok(new
            {
                subscriptions = activeSubscriptions,
                payments = paymentsResult,
                totalSpent
            });
        }

        [HttpGet("stripe-invoices")]
        public async Task<IActionResult> GetStripeInvoices()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized("Invalid token");

            var orders = await _unitOfWork.OrderRepository.GetAllAsync(o => o.UserId == userId && !string.IsNullOrEmpty(o.SubscriptionId));
            var subscriptionIds = orders.Select(o => o.SubscriptionId).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();

            if (subscriptionIds.Count == 0)
                return Ok(new { invoices = new List<object>(), totalSpent = 0 });

            try
            {
                var allInvoices = new List<dynamic>();
                var stripeInvoiceService = new Stripe.InvoiceService();

                foreach (var subId in subscriptionIds)
                {
                    var invoices = await stripeInvoiceService.ListAsync(new Stripe.InvoiceListOptions
                    {
                        Subscription = subId,
                        Limit = 100
                    });

                    var order = orders.FirstOrDefault(o => o.SubscriptionId == subId);

                    foreach (var inv in invoices)
                    {
                        allInvoices.Add(new
                        {
                            id = inv.Id,
                            number = inv.Number ?? "N/A",
                            amount = (decimal)(inv.AmountPaid / 100M),
                            amountDue = (decimal)(inv.AmountDue / 100M),
                            currency = inv.Currency?.ToUpper() ?? "USD",
                            status = inv.Status,
                            created = inv.Created,
                            periodStart = inv.PeriodStart,
                            periodEnd = inv.PeriodEnd,
                            hostedInvoiceUrl = inv.HostedInvoiceUrl,
                            invoicePdf = inv.InvoicePdf,
                            paymentIntent = string.Empty,
                            description = $"Invoice #{inv.Number ?? inv.Id}",
                            serviceType = order?.ServiceType
                        });
                    }
                }

                var sorted = allInvoices.OrderByDescending(i => i.created).ToList();
                var totalSpent = sorted.Where(i => i.status == "paid").Sum(i => (double)i.amount);

                return Ok(new { invoices = sorted, totalSpent });
            }
            catch (StripeException ex)
            {
                return StatusCode(500, new { message = "Failed to fetch invoices from Stripe", error = ex.Message });
            }
        }

        [HttpGet("billing")]
        public async Task<IActionResult> GetBillingInfo()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized("Invalid token");

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound("User not found");

            var orders = (await _unitOfWork.OrderRepository.GetAllAsync(o => o.UserId == userId))
                .OrderByDescending(o => o.CreatedAt)
                .ToList();

            var latestOrder = orders.FirstOrDefault(o => !string.IsNullOrEmpty(o.StripeBrand));

            var cardInfo = latestOrder != null ? new
            {
                cardHolder = latestOrder.StripeBrand,
                expiryDate = $"{latestOrder.StripeExpMonth:D2}/{latestOrder.StripeExpYear}",
                cardType = latestOrder.StripeBrand,
                last4 = latestOrder.StripeLast4
            } : null;

            var paymentHistory = (await _unitOfWork.PaymentHistoryRepository.GetAllAsync(ph => ph.UserId == userId))
                .OrderByDescending(ph => ph.CreatedAt)
                .ToList();

            var payments = paymentHistory.Count > 0
                ? paymentHistory.Select(ph => new
                {
                    id = ph.Id,
                    date = ph.CreatedAt,
                    description = ph.Description,
                    amount = $"{(ph.Currency ?? "USD").ToUpper()} {ph.Amount:F2}",
                    status = ph.Status,
                    invoice = ph.StripeInvoiceUrl ?? "N/A"
                }).Take(20).ToList()
                : orders.Select(o => new
                {
                    id = o.Id,
                    date = o.CreatedAt,
                    description = $"{o.ServiceType} - {o.PlanMode}",
                    amount = $"{(o.Currency ?? "USD").ToUpper()} {o.Amount:F2}",
                    status = o.Status == "Completed" ? "paid" : o.Status == "Pending" ? "pending" : "failed",
                    invoice = o.StripeInvoiceUrl ?? "N/A"
                }).Take(10).ToList();

            return Ok(new
            {
                card = cardInfo,
                address = new
                {
                    name = user.UserName ?? "",
                    email = user.Email ?? "",
                    country = "",
                    city = "",
                    address = "",
                    zipCode = ""
                },
                payments = payments
            });
        }

        [HttpPut("billing/address")]
        public async Task<IActionResult> UpdateBillingAddress([FromBody] BillingAddressRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized("Invalid token");

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound("User not found");

            user.UserName = request.Name ?? user.UserName;
            user.Email = request.Email ?? user.Email;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
                return BadRequest(new { message = "Failed to update user info", errors = result.Errors });

            return Ok(new { message = "Billing address updated successfully" });
        }
    }

    public class BillingAddressRequest
    {
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Country { get; set; }
        public string? City { get; set; }
        public string? Address { get; set; }
        public string? ZipCode { get; set; }
    }

    public class CancelSubscriptionRequest
    {
        public string? Reason { get; set; }
        public bool? InvoiceNow { get; set; }
        public bool? Prorate { get; set; }
    }

    public class AddNoteRequest
    {
        public string Note { get; set; } = string.Empty;
    }
}

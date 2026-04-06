using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

        public OrderController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        [HttpPost("create-checkout-session")]
        public async Task<IActionResult> CreateCheckoutSession([FromBody] CreatePaymentRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (request.Amount <= 0)
                return BadRequest(new { message = "Amount must be greater than 0." });

            if (request.Mode != "payment" && request.Mode != "subscription")
                return BadRequest(new { message = "Invalid mode. Use 'payment' or 'subscription'." });

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
                            Description = GetPlanDescription(request.ServiceType),
                            Images      = new List<string> { GetPlanImage(request.ServiceType) }
                        }
                    },
                    Quantity = 1
                }
            },
                    SuccessUrl = "https://abbsium.com/platform/success-payment?session_id={CHECKOUT_SESSION_ID}",
                    CancelUrl = "https://abbsium.com/platform/payment-denied",
                    Metadata = new Dictionary<string, string>
            {
                { "userId",      userId              },
                { "serviceType", request.ServiceType },
                { "planMode",    "payment"           }
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
                            Description = GetPlanDescription(request.ServiceType),
                            Images      = new List<string> { GetPlanImage(request.ServiceType) }
                        }
                    },
                    Quantity = 1
                }
            },
                    SuccessUrl = "https://abbsium.com/platform/success-payment?session_id={CHECKOUT_SESSION_ID}",
                    CancelUrl = "https://abbsium.com/platform/payment-denied",
                    Metadata = new Dictionary<string, string>
            {
                { "userId",      userId              },
                { "serviceType", request.ServiceType },
                { "planMode",    "subscription"      }
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
                _ => serviceType
            };

        private static string GetPlanDescription(string serviceType) =>
            serviceType.ToLower() switch
            {
                "starter" => "One-time payment · Up to 5 projects · Basic dashboard · Email support · 10GB storage · Monthly reports",
                "professional" => "Monthly subscription · Unlimited projects · Advanced analytics · 24/7 priority support · 100GB storage · Up to 15 users · Custom reports",
                "enterprise" => "Monthly subscription · Everything in Professional · Full API access · Unlimited users · Unlimited storage · Dedicated support · Automatic backups · Custom integrations",
                _ => "Abbsium service plan"
            };

        private static string GetPlanImage(string serviceType) =>
            serviceType.ToLower() switch
            {
                "starter" => "https://images.unsplash.com/photo-1499750310107-5fef28a66643?w=800&auto=format&fit=crop",
                "professional" => "https://images.unsplash.com/photo-1460925895917-afdab827c52f?w=800&auto=format&fit=crop",
                "enterprise" => "https://images.unsplash.com/photo-1504868584819-f8e8b4b6d7e3?w=800&auto=format&fit=crop",
                _ => "https://images.unsplash.com/photo-1460925895917-afdab827c52f?w=800&auto=format&fit=crop"
            };

        // POST /api/order/verify
        [HttpPost("verify")]
        public async Task<IActionResult> VerifyPayment([FromBody] PaymentVerificationRequest request)
        {
            var sessionService = new SessionService();
            var session = await sessionService.GetAsync(request.SessionId, new SessionGetOptions
            {
                Expand = new List<string> { "subscription" }
            });

            // Suscripciones recién creadas tienen "no_payment_required" o "paid"
            var isPaid = session.PaymentStatus == "paid" ||
                         session.PaymentStatus == "no_payment_required";

            if (!isPaid)
                return BadRequest(new { message = "Payment failed or incomplete." });

            var userId = session.Metadata["userId"];
            var serviceType = session.Metadata["serviceType"];
            var planMode = session.Metadata.GetValueOrDefault("planMode", "payment");
            var amount = (decimal)(session.AmountTotal ?? 0) / 100M;

            // Idempotencia: subscription usa SubscriptionId, pago único usa PaymentIntentId
            var dedupeId = planMode == "subscription"
                ? session.SubscriptionId
                : session.PaymentIntentId;

            var existing = _unitOfWork.OrderRepository
                .GetFirstOrDefault(o => o.PaymentIntentId == dedupeId);

            if (existing == null)
            {
                var order = new Order
                {
                    UserId = userId,
                    Amount = amount,
                    Currency = session.Currency ?? "usd",
                    ServiceType = serviceType,
                    PaymentIntentId = dedupeId,
                    Status = "Completed"
                };

                _unitOfWork.OrderRepository.Add(order);
                _unitOfWork.Save();
            }

            return Ok(new
            {
                message = "Payment verified.",
                planMode,
                serviceType
            });
        }

        // GET /api/order
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Order>>> GetAllOrders()
        {
            var orders = _unitOfWork.OrderRepository.GetAll();
            if (orders == null) return BadRequest("Orders not found");
            return Ok(orders);
        }

        // GET /api/order/ById/{id}
        [HttpGet("ById/{id}")]
        public async Task<ActionResult<Order>> GetOrderById(Guid id)
        {
            var order = _unitOfWork.OrderRepository
                .GetFirstOrDefault(x => x.Id == id, x => x.User_data);
            return Ok(order);
        }
    }
}
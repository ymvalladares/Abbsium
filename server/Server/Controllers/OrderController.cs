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

            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                Mode = "payment",
                LineItems = new List<SessionLineItemOptions>
            {
            new SessionLineItemOptions
            {
                PriceData = new SessionLineItemPriceDataOptions
                {
                    Currency = "usd",
                    UnitAmount = (long)(request.Amount * 100), // en centavos
                    ProductData = new SessionLineItemPriceDataProductDataOptions
                    {
                        Name = request.ServiceType,
                        Images = new List<string> {
                    "https://www.bdtask.com/blog/uploads/website-development-life-cycle.jpg"
                }
                    }
                },
                Quantity = 1
            }
        },
                //SuccessUrl = "https://abbsium.com/#/success-payment?session_id={CHECKOUT_SESSION_ID}",
                //CancelUrl = "https://abbsium.com/#/payment-denied",
                SuccessUrl = "http://localhost:3000/#/success-payment?session_id={CHECKOUT_SESSION_ID}",
                CancelUrl = "http://localhost:3000/#/payment-denied",
                Metadata = new Dictionary<string, string>
        {
            { "userId", userId },
            { "serviceType", request.ServiceType }
        }
            };

            var service = new SessionService();
            var session = await service.CreateAsync(options);

            return Ok(new { sessionId = session.Id });
        }

        [HttpPost("verify")]
        public async Task<IActionResult> VerifyPayment([FromBody] PaymentVerificationRequest request)
        {
            var service = new SessionService();
            var session = await service.GetAsync(request.SessionId);

            if (session.PaymentStatus == "paid")
            {
                var userId = session.Metadata["userId"];
                var serviceType = session.Metadata["serviceType"];
                var amount = (decimal)session.AmountTotal / 100M;

                var existing = _unitOfWork.OrderRepository
                    .GetFirstOrDefault(o => o.PaymentIntentId == session.PaymentIntentId);

                if (existing == null)
                {
                    var order = new Order
                    {
                        UserId = userId,
                        Amount = amount,
                        Currency = session.Currency,
                        ServiceType = serviceType,
                        PaymentIntentId = session.PaymentIntentId,
                        Status = "Completed"
                    };

                    _unitOfWork.OrderRepository.Add(order);
                    _unitOfWork.Save();
                }

                return Ok();
            }

            return BadRequest("Payment Failled");
        }



        [HttpGet]
        public async Task<ActionResult<IEnumerable<Order>>> getAllOrders()
        {
            var query = _unitOfWork.OrderRepository.GetAll();
            if (query == null) return BadRequest("Orders not found");
            return Ok(query);
        }



        [HttpGet("ById/{id}")]
        public async Task<ActionResult<Order>> getOrderById(Guid id)
        {
            var query = _unitOfWork.OrderRepository.GetFirstOrDefault(x => x.Id == id, x => x.User_data);
            return Ok(query);
        }


    }
}

using CINEMA.Services;
using CINEMA.Models; // Bắt buộc để nhận diện Order của project bạn
using Stripe;
using Stripe.Checkout;
using Microsoft.Extensions.Logging;

namespace CINEMA.Services
{
    public class PaymentResult
    {
        public string PaymentCode { get; set; } = string.Empty;
        public string PaymentUrl { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class PaymentService : IPaymentService
    {
        private readonly ILogger<PaymentService> _logger;

        public PaymentService(ILogger<PaymentService> logger)
        {
            _logger = logger;
        }

        // Ép kiểu rõ ràng tham số là CINEMA.Models.Order
        public async Task<PaymentResult> CreatePaymentAsync(CINEMA.Models.Order order, string successUrl, string cancelUrl)
        {
            try
            {
                // TotalAmount của bạn là kiểu nullable (decimal?), nên cần dùng ?? 0 để tránh lỗi null
                var amount = (long)(order.TotalAmount ?? 0);

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
                                Currency = "vnd",
                                UnitAmount = amount,
                                ProductData = new SessionLineItemPriceDataProductDataOptions
                                {
                                    Name = $"Đơn hàng vé xem phim #{order.OrderId}",
                                    Description = $"Thanh toán đơn #{order.OrderId}"
                                }
                            },
                            Quantity = 1
                        }
                    },
                    // Đã bỏ trường CustomerEmail vì Model Order của bạn không có trường này trực tiếp.
                    // Stripe sẽ tự động yêu cầu khách hàng nhập email trên trang thanh toán của họ.
                    SuccessUrl = successUrl,
                    CancelUrl = cancelUrl
                };

                var service = new SessionService();
                var session = await service.CreateAsync(options);

                return new PaymentResult
                {
                    Success = true,
                    PaymentCode = session.Id ?? string.Empty,
                    PaymentUrl = session.Url ?? string.Empty,
                    Amount = order.TotalAmount ?? 0,
                    Message = "Redirecting to Stripe Checkout"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tạo Stripe Checkout Session cho đơn {OrderId}", order.OrderId);

                return new PaymentResult
                {
                    Success = false,
                    Message = $"Không thể kết nối tới Stripe: {ex.Message}"
                };
            }
        }
    }
}
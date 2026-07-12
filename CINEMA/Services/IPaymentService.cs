using CINEMA.Models; // Đảm bảo đúng namespace model của bạn

namespace CINEMA.Services
{
    public interface IPaymentService
    {
        // Tạo phiên thanh toán trên Stripe, trả về PaymentResult chứa URL để redirect
        Task<PaymentResult> CreatePaymentAsync(Order order, string successUrl, string
        cancelUrl);
    }
}
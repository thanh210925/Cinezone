using CINEMA.Helpers;
using CINEMA.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;

namespace CINEMA.Services
{
    public interface IVnpayService
    {
        string CreatePaymentUrl(Order model, HttpContext context);
    }

    public class VnpayService : IVnpayService
    {
        private readonly IConfiguration _config;

        public VnpayService(IConfiguration config)
        {
            _config = config;
        }

        public string CreatePaymentUrl(Order order, HttpContext context)
        {
            var timeZoneById = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            var timeNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZoneById);

            var pay = new VnpayLibrary();
            var urlCallBack = _config["Vnpay:ReturnUrl"];
            var tmnCode = _config["Vnpay:TmnCode"];
            var hashSecret = _config["Vnpay:HashSecret"];

            // 🛠️ SỬA 1: Làm tròn số tiền một cách tường minh để tránh sai số thập phân (như .00)
            long finalAmount = Convert.ToInt64(Math.Round((order.TotalAmount ?? 0) * 100));

            pay.AddRequestData("vnp_Version", "2.1.0");
            pay.AddRequestData("vnp_Command", "pay");
            pay.AddRequestData("vnp_TmnCode", tmnCode);
            pay.AddRequestData("vnp_Amount", finalAmount.ToString());
            pay.AddRequestData("vnp_CreateDate", timeNow.ToString("yyyyMMddHHmmss"));
            pay.AddRequestData("vnp_CurrCode", "VND");

            // 🛠️ SỬA 2: Ép cứng IP thành 127.0.0.1 để tránh lỗi nhận diện IPv6 (::1) khi test trên Localhost
            pay.AddRequestData("vnp_IpAddr", "127.0.0.1");

            // 🛠️ SỬA 3: Đổi ngôn ngữ thành "vn" viết thường theo đúng cấu trúc cũ hoặc viết hoa tùy phiên bản
            pay.AddRequestData("vnp_Locale", "vn");

            pay.AddRequestData("vnp_OrderInfo", $"Thanh toan don hang {order.OrderId}");

            // 🛠️ SỬA 4: VNPay 2.1.0 KHÔNG chấp nhận "other". Hãy đổi thành "250000" (Giải trí) hoặc "billpayment"
            pay.AddRequestData("vnp_OrderType", "250000");

            pay.AddRequestData("vnp_ReturnUrl", urlCallBack);

            // 🛠️ SỬA 5: Thêm đuôi thời gian (Ticks) để mã đơn hàng luôn luôn mới, tránh lỗi trùng mã trên Sandbox
            pay.AddRequestData("vnp_TxnRef", order.OrderId.ToString() + "_" + DateTime.Now.Ticks.ToString());

            var paymentUrl = pay.CreateRequestUrl(_config["Vnpay:BaseUrl"], hashSecret);

            return paymentUrl;
        }
    }
}
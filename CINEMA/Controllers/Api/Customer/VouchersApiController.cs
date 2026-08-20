using CINEMA.DTOs;
using CINEMA.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CINEMA.Controllers.Api.CustomerApi
{
    [ApiController]
    [Route("api/vouchers")]
    public class VouchersApiController : ControllerBase
    {
        private readonly CinemaContext _context;

        public VouchersApiController(CinemaContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Lấy danh sách Voucher đang khả dụng
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetVouchers()
        {
            var now = DateTime.Now;
            var vouchers = await _context.Vouchers
                .Where(v => v.IsActive && v.Quantity > v.UsedCount
                       && (v.StartDate == null || v.StartDate <= now)
                       && (v.EndDate == null || v.EndDate >= now))
                .AsNoTracking()
                .Select(v => new
                {
                    v.VoucherId,
                    v.Code,
                    v.DiscountPercent,
                    v.DiscountAmount,
                    v.MinOrderValue,
                    v.StartDate,
                    v.EndDate,
                    v.TermsAndConditions
                })
                .ToListAsync();

            return Ok(vouchers);
        }

        /// <summary>
        /// Kiểm tra mã giảm giá và tính toán số tiền được giảm
        /// </summary>
        [HttpPost("check")]
        public async Task<IActionResult> CheckVoucher([FromBody] VoucherCheckDto model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var now = DateTime.Now;
            var voucher = await _context.Vouchers
                .FirstOrDefaultAsync(v => v.Code.ToUpper() == model.Code.ToUpper() && v.IsActive);

            if (voucher == null)
            {
                return Ok(new VoucherResultDto
                {
                    IsValid = false,
                    Message = "Mã giảm giá không tồn tại hoặc đã bị vô hiệu hóa."
                });
            }

            if (voucher.Quantity <= voucher.UsedCount)
            {
                return Ok(new VoucherResultDto
                {
                    IsValid = false,
                    Message = "Mã giảm giá đã hết lượt sử dụng."
                });
            }

            if (voucher.StartDate.HasValue && voucher.StartDate > now)
            {
                return Ok(new VoucherResultDto
                {
                    IsValid = false,
                    Message = "Mã giảm giá chưa đến đợt áp dụng."
                });
            }

            if (voucher.EndDate.HasValue && voucher.EndDate < now)
            {
                return Ok(new VoucherResultDto
                {
                    IsValid = false,
                    Message = "Mã giảm giá đã hết hạn."
                });
            }

            decimal totalOrder = model.TicketTotal + model.ComboTotal;

            if (totalOrder < voucher.MinOrderValue)
            {
                return Ok(new VoucherResultDto
                {
                    IsValid = false,
                    Message = $"Đơn hàng tối thiểu {voucher.MinOrderValue:N0}đ mới được áp dụng mã này."
                });
            }

            decimal discount = 0;
            if (voucher.DiscountAmount.HasValue && voucher.DiscountAmount.Value > 0)
            {
                discount = voucher.DiscountAmount.Value;
            }
            else if (voucher.DiscountPercent.HasValue && voucher.DiscountPercent.Value > 0)
            {
                discount = totalOrder * (decimal)(voucher.DiscountPercent.Value / 100.0);
            }

            if (discount > totalOrder)
                discount = totalOrder;

            return Ok(new VoucherResultDto
            {
                IsValid = true,
                Message = "Áp dụng mã giảm giá thành công!",
                Code = voucher.Code,
                DiscountAmount = discount,
                FinalTotal = totalOrder - discount
            });
        }
    }
}

using System.Collections.Generic;

namespace CINEMA.Models
{
    public class VoucherCondition
    {
        public int VoucherConditionId { get; set; }
        
        public string Name { get; set; } = ""; // Tên phạm vi áp dụng (VD: "Chỉ áp dụng mua vé", "Khuyến mãi Thứ Ba")
        
        public string? Description { get; set; } // Mô tả chi tiết điều kiện

        // Danh sách các quy tắc động thuộc điều kiện này
        public virtual ICollection<VoucherRule> Rules { get; set; } = new List<VoucherRule>();

        public virtual ICollection<Voucher> Vouchers { get; set; } = new List<Voucher>();
    }
}

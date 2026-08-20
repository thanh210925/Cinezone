namespace CINEMA.Models
{
    public class VoucherRule
    {
        public int VoucherRuleId { get; set; }
        
        public int VoucherConditionId { get; set; }
        public virtual VoucherCondition? VoucherCondition { get; set; }

        public string Field { get; set; } = ""; // e.g., "TicketQuantity", "ComboQuantity", "TotalPrice", "DayOfWeek", "ShowtimeHour"
        public string Operator { get; set; } = ""; // e.g., "Equal", "NotEqual", "GreaterThanOrEqual", "LessThanOrEqual", "GreaterThan", "LessThan", "Contains"
        public string Value { get; set; } = ""; // e.g., "2", "Tuesday", "True"
    }
}

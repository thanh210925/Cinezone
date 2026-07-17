using System;
using System.Collections.Generic;
using CINEMA.Models;

namespace CINEMA.Helpers
{
    public class VoucherEvaluationContext
    {
        public int TicketQuantity { get; set; }
        public int ComboQuantity { get; set; }
        public decimal TicketTotal { get; set; }
        public decimal ComboTotal { get; set; }
        public decimal TotalPrice { get; set; }
        public bool IsGroupBooking { get; set; }
        public int GroupMemberCount { get; set; }
        public string DayOfWeek { get; set; } = "";
        public int ShowtimeHour { get; set; }
    }

    public static class VoucherRuleEvaluator
    {
        public static bool Evaluate(VoucherRule rule, VoucherEvaluationContext context, out string errorMessage)
        {
            errorMessage = "";
            
            object actualValueObj = rule.Field switch
            {
                "TicketQuantity" => context.TicketQuantity,
                "ComboQuantity" => context.ComboQuantity,
                "TicketTotal" => context.TicketTotal,
                "ComboTotal" => context.ComboTotal,
                "TotalPrice" => context.TotalPrice,
                "IsGroupBooking" => context.IsGroupBooking,
                "GroupMemberCount" => context.GroupMemberCount,
                "DayOfWeek" => context.DayOfWeek,
                "ShowtimeHour" => context.ShowtimeHour,
                _ => null
            };

            if (actualValueObj == null)
                return true;

            string actualStr = actualValueObj.ToString() ?? "";
            string ruleVal = rule.Value ?? "";

            // Kiểm tra kiểu dữ liệu
            bool actualIsNum = decimal.TryParse(actualStr, out decimal actualNum);
            bool ruleIsNum = decimal.TryParse(ruleVal, out decimal ruleNum);
            bool isNumeric = actualIsNum && ruleIsNum;

            bool actualIsBool = bool.TryParse(actualStr, out bool actualBool);
            bool ruleIsBool = bool.TryParse(ruleVal, out bool ruleBool);
            bool isBool = actualIsBool && ruleIsBool;

            bool result = false;
            switch (rule.Operator)
            {
                case "Equal":
                    if (isBool) result = (actualBool == ruleBool);
                    else if (isNumeric) result = (actualNum == ruleNum);
                    else result = actualStr.Equals(ruleVal, StringComparison.OrdinalIgnoreCase);
                    break;
                case "NotEqual":
                    if (isBool) result = (actualBool != ruleBool);
                    else if (isNumeric) result = (actualNum != ruleNum);
                    else result = !actualStr.Equals(ruleVal, StringComparison.OrdinalIgnoreCase);
                    break;
                case "GreaterThan":
                    if (isNumeric) result = (actualNum > ruleNum);
                    break;
                case "GreaterThanOrEqual":
                    if (isNumeric) result = (actualNum >= ruleNum);
                    break;
                case "LessThan":
                    if (isNumeric) result = (actualNum < ruleNum);
                    break;
                case "LessThanOrEqual":
                    if (isNumeric) result = (actualNum <= ruleNum);
                    break;
                case "Contains":
                    result = actualStr.Contains(ruleVal, StringComparison.OrdinalIgnoreCase);
                    break;
            }

            if (!result)
            {
                string fieldFriendly = rule.Field switch
                {
                    "TicketQuantity" => "Số lượng vé",
                    "ComboQuantity" => "Số lượng bắp nước",
                    "TicketTotal" => "Tổng tiền vé",
                    "ComboTotal" => "Tổng tiền bắp nước",
                    "TotalPrice" => "Tổng giá trị đơn hàng",
                    "IsGroupBooking" => "Đơn hàng đặt nhóm",
                    "GroupMemberCount" => "Số lượng thành viên nhóm",
                    "DayOfWeek" => "Thứ trong tuần",
                    "ShowtimeHour" => "Giờ bắt đầu chiếu",
                    _ => rule.Field
                };

                string operatorFriendly = rule.Operator switch
                {
                    "Equal" => "phải bằng",
                    "NotEqual" => "phải khác",
                    "GreaterThan" => "phải lớn hơn",
                    "GreaterThanOrEqual" => "phải tối thiểu là",
                    "LessThan" => "phải nhỏ hơn",
                    "LessThanOrEqual" => "phải tối đa là",
                    "Contains" => "phải chứa",
                    _ => rule.Operator
                };

                // Hiển thị giá trị thân thiện cho Boolean hoặc DayOfWeek
                string valueFriendly = ruleVal;
                if (rule.Field == "IsGroupBooking" && isBool)
                {
                    valueFriendly = ruleBool ? "Đặt nhóm" : "Đặt đơn thường";
                }
                else if (rule.Field == "DayOfWeek")
                {
                    valueFriendly = ruleVal switch
                    {
                        "Monday" => "Thứ Hai",
                        "Tuesday" => "Thứ Ba",
                        "Wednesday" => "Thứ Tư",
                        "Thursday" => "Thứ Năm",
                        "Friday" => "Thứ Sáu",
                        "Saturday" => "Thứ Bảy",
                        "Sunday" => "Chủ Nhật",
                        _ => ruleVal
                    };
                }

                errorMessage = $"{fieldFriendly} {operatorFriendly} {valueFriendly}";
                return false;
            }

            return true;
        }
    }
}

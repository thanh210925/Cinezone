using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace CINEMA.Models;

public partial class Customer
{
    public int CustomerId { get; set; }

    public string FullName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string? Phone { get; set; }

    public DateTime? BirthDate { get; set; }

    public string? Gender { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? LastLogin { get; set; }

    public string? Address { get; set; }

    public string? AvatarUrl { get; set; }

    public string? City { get; set; }

    public string? Region { get; set; }
    public string? Avatar { get; set; }
    public decimal TotalSpent { get; set; } = 0;
    public string MembershipLevel { get; set; } = "Đồng";
    public int ReputationScore { get; set; } = 100;
    public string CalculateMembershipLevel()
    {
        if (TotalSpent > 10000000) return "Kim cương";
        if (TotalSpent >= 3000000) return "Bạc";
        return "Đồng";
    }
    // % tiến độ lên hạng
    [NotMapped]
    public int ProgressPercent
    {
        get
        {
            if (TotalSpent < 3000000)
                return (int)(TotalSpent / 3000000 * 100);

            if (TotalSpent < 10000000)
                return (int)((TotalSpent - 3000000) / 7000000 * 100);

            return 100;
        }
    }

    // số tiền còn thiếu để lên hạng
    [NotMapped]
    public decimal NextLevelAmount
    {
        get
        {
            if (TotalSpent < 3000000)
                return 3000000 - TotalSpent;

            if (TotalSpent < 10000000)
                return 10000000 - TotalSpent;

            return 0;
        }
    }

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();

}

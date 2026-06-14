using System;
using System.Collections.Generic;

namespace CINEMA.Models;

public partial class Admin
{
    public int AdminId { get; set; }

    public string FullName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true; // Để khóa tài khoản nhân viên khi cần

    public DateTime? CreatedAt { get; set; }

    public DateTime? LastLogin { get; set; }
}

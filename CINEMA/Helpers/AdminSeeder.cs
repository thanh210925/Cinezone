using System;
using System.Collections.Generic;
using System.Linq;
using CINEMA.Models;
using BCrypt.Net;

namespace CINEMA.Helpers
{
    public static class AdminSeeder
    {
        public static void Seed(CinemaContext context)
        {
            var defaultPasswordHash = BCrypt.Net.BCrypt.HashPassword("123456");

            var seedAdmins = new List<Admin>
            {
                new Admin
                {
                    FullName = "Administrator (Super Admin)",
                    Email = "admin@cinezone.vn",
                    PasswordHash = defaultPasswordHash,
                    Role = "SuperAdmin",
                    Phone = "0900000001",
                    EmployeeCode = "NV001",
                    IsActive = true,
                    CreatedAt = DateTime.Now
                },
                new Admin
                {
                    FullName = "Võ Thành Nguyên (Super Admin)",
                    Email = "Admin1@gmail.com",
                    PasswordHash = defaultPasswordHash,
                    Role = "SuperAdmin",
                    Phone = "0900000002",
                    EmployeeCode = "NV002",
                    IsActive = true,
                    CreatedAt = DateTime.Now
                },
                new Admin
                {
                    FullName = "Quản Lý Phim (Manager)",
                    Email = "manager@cinezone.vn",
                    PasswordHash = defaultPasswordHash,
                    Role = "Manager",
                    Phone = "0900000003",
                    EmployeeCode = "NV003",
                    IsActive = true,
                    CreatedAt = DateTime.Now
                },
                new Admin
                {
                    FullName = "Chăm Sóc Khách Hàng (CRM)",
                    Email = "crm@cinezone.vn",
                    PasswordHash = defaultPasswordHash,
                    Role = "CRM",
                    Phone = "0900000004",
                    EmployeeCode = "NV004",
                    IsActive = true,
                    CreatedAt = DateTime.Now
                },
                new Admin
                {
                    FullName = "Nhân Viên Bán Vé (Staff)",
                    Email = "staff@cinezone.vn",
                    PasswordHash = defaultPasswordHash,
                    Role = "Staff",
                    Phone = "0900000005",
                    EmployeeCode = "NV005",
                    IsActive = true,
                    CreatedAt = DateTime.Now
                }
            };

            bool hasChanges = false;
            foreach (var seedAcc in seedAdmins)
            {
                var existing = context.Admins.FirstOrDefault(a => a.Email.ToLower() == seedAcc.Email.ToLower());
                if (existing == null)
                {
                    context.Admins.Add(seedAcc);
                    hasChanges = true;
                }
                else
                {
                    // Cập nhật mật khẩu 123456 và kích hoạt tài khoản
                    existing.PasswordHash = defaultPasswordHash;
                    existing.IsActive = true;
                    if (string.IsNullOrEmpty(existing.Role))
                    {
                        existing.Role = seedAcc.Role;
                    }
                    if (string.IsNullOrEmpty(existing.EmployeeCode))
                    {
                        existing.EmployeeCode = seedAcc.EmployeeCode;
                    }
                    hasChanges = true;
                }
            }

            if (hasChanges)
            {
                context.SaveChanges();
            }
        }
    }
}

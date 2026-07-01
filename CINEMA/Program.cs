using CINEMA.Controllers;
using CINEMA.Models;
using CINEMA.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace CINEMA
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // 🟢 Add services
            builder.Services.AddControllersWithViews();

            builder.Services.AddDbContext<CinemaContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("CinemaDb")));

            builder.Services.AddDistributedMemoryCache();
            builder.Services.AddSession();

            builder.Services.AddScoped<IVnpayService, VnpayService>();
            builder.Services.AddScoped<StatisticsController>();
            builder.Services.AddHttpClient<CINEMA.Services.GeminiService>();
            // Trong file Program.cs
            builder.Services.AddHttpContextAccessor(); // Thêm dòng này để lấy Session
            // 🟢 AUTH (PHẢI đặt trước Build)
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                options.LoginPath = "/Customer/Login";
            })
            // GG
         ;

            var app = builder.Build();

            // 🟢 Middleware
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthentication();
            app.UseSession();// 🔥 BẮT BUỘC
            app.UseAuthorization();

      

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.Run();
        }
    }
}

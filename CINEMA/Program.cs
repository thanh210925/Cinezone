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
            builder.Services.AddSignalR();

            builder.Services.AddDbContext<CinemaContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("CinemaDb")));

            builder.Services.AddDistributedMemoryCache();
            builder.Services.AddSession();

            builder.Services.AddScoped<IVnpayService, VnpayService>();
            builder.Services.AddScoped<StatisticsController>();
            builder.Services.AddScoped<RecommendationEngine>();
            builder.Services.AddHttpClient<CINEMA.Services.GeminiService>();

            // Email Notifications Configuration
            builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
            builder.Services.AddScoped<IEmailService, EmailService>();
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

            // 🟢 Tự động thêm cột EndDate vào bảng Movies nếu chưa có
            using (var scope = app.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<CinemaContext>();
                try
                {
                    context.Database.ExecuteSqlRaw(@"
                        IF NOT EXISTS (
                            SELECT * FROM sys.columns 
                            WHERE object_id = OBJECT_ID(N'[dbo].[Movies]') 
                            AND name = 'EndDate'
                        )
                        BEGIN
                            ALTER TABLE [dbo].[Movies] ADD [EndDate] [date] NULL;
                        END
                    ");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DB Setup Error]: {ex.Message}");
                }
            }

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

      

            app.MapHub<CINEMA.Hubs.ChatHub>("/chatHub");

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.Run();
        }
    }
}

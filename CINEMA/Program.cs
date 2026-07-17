using CINEMA.Controllers;
using CINEMA.Models;
using CINEMA.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Stripe;

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
            builder.Services.AddScoped<CustomerClusteringService>();
            builder.Services.AddHttpClient<CINEMA.Services.GeminiService>();
            builder.Services.AddScoped<IMovieService, MovieService>();

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
            //Stripe
            builder.Services.AddScoped<IPaymentService, PaymentService>();
            var app = builder.Build();

     

            // 🟢 Middleware
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }
            //Stripe
            // Đọc Secret Key: ưu tiên configuration, sau đó fallback sang biến môi trường
            var stripeSecret = builder.Configuration["Stripe:SecretKey"]
                               ?? Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY");
            if (!string.IsNullOrWhiteSpace(stripeSecret))
            {
                StripeConfiguration.ApiKey = stripeSecret;
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles(new StaticFileOptions
            {
                ServeUnknownFileTypes = true,
                DefaultContentType = "application/octet-stream"
            });

            app.UseRouting();

            app.UseAuthentication();
            app.UseSession();// 🔥 BẮT BUỘC
            app.UseAuthorization();

      

            app.MapHub<CINEMA.Hubs.ChatHub>("/chatHub");
            app.MapHub<CINEMA.Hubs.GroupBookingHub>("/groupBookingHub");

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            using (var scope = app.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<CinemaContext>();
                if (!context.VoucherConditions.Any())
                {
                    var ticketOnly = new VoucherCondition { Name = "Chỉ áp dụng mua vé" };
                    ticketOnly.Rules.Add(new VoucherRule { Field = "TicketQuantity", Operator = "GreaterThanOrEqual", Value = "1" });
                    ticketOnly.Rules.Add(new VoucherRule { Field = "ComboQuantity", Operator = "Equal", Value = "0" });

                    var comboOnly = new VoucherCondition { Name = "Chỉ áp dụng khi mua bắp nước" };
                    comboOnly.Rules.Add(new VoucherRule { Field = "ComboQuantity", Operator = "GreaterThanOrEqual", Value = "1" });
                    comboOnly.Rules.Add(new VoucherRule { Field = "TicketQuantity", Operator = "Equal", Value = "0" });

                    var bothOnly = new VoucherCondition { Name = "Chỉ áp dụng khi mua cả 2 (Vé & Bắp nước)" };
                    bothOnly.Rules.Add(new VoucherRule { Field = "TicketQuantity", Operator = "GreaterThanOrEqual", Value = "1" });
                    bothOnly.Rules.Add(new VoucherRule { Field = "ComboQuantity", Operator = "GreaterThanOrEqual", Value = "1" });

                    var groupOnly = new VoucherCondition { Name = "Chỉ áp dụng khi mua cùng bạn (Rủ bạn đi cùng)" };
                    groupOnly.Rules.Add(new VoucherRule { Field = "IsGroupBooking", Operator = "Equal", Value = "True" });
                    groupOnly.Rules.Add(new VoucherRule { Field = "GroupMemberCount", Operator = "GreaterThanOrEqual", Value = "2" });

                    context.VoucherConditions.AddRange(ticketOnly, comboOnly, bothOnly, groupOnly);
                    context.SaveChanges();
                }
            }

            app.Run();
        }
    }
}

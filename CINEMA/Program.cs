using System.Text;
using System.Text.Json.Serialization;
using CINEMA.Controllers;
using CINEMA.Models;
using CINEMA.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Stripe;

namespace CINEMA
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // 🟢 Add services
            builder.Services.AddControllersWithViews()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
                });
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
            builder.Services.AddScoped<IJwtService, JwtService>();

            // Email Notifications Configuration
            builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
            builder.Services.AddScoped<IEmailService, EmailService>();
            builder.Services.AddScoped<IReminderService, ReminderService>();
            builder.Services.AddScoped<INotificationService, NotificationService>();
            builder.Services.AddHttpContextAccessor();

            // 🟢 Swagger / OpenAPI Configuration
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "Cinezone RESTful Web API",
                    Version = "v1",
                    Description = "Hệ thống API toàn diện cho ứng dụng đặt vé xem phim Cinezone"
                });

                // Cấu hình Nút Authorize Bearer Token trên Swagger UI
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "Nhập Token theo định dạng: Bearer {your_jwt_token}",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer"
                });

                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });
            });

            // 🟢 AUTH (PHẢI đặt trước Build)
            var jwtSettings = builder.Configuration.GetSection("Jwt");
            var secretKey = jwtSettings["SecretKey"] ?? "Cinezone_Super_Secret_Jwt_Security_Key_2026_DotNet8_API!";

            builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                options.LoginPath = "/Customer/Login";
            })
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.RequireHttpsMetadata = false;
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings["Issuer"] ?? "CinezoneAPI",
                    ValidateAudience = true,
                    ValidAudience = jwtSettings["Audience"] ?? "CinezoneClient",
                    ClockSkew = TimeSpan.Zero
                };
            })
            // GG
       ;

            //Stripe
            builder.Services.AddScoped<IPaymentService, PaymentService>();
            var app = builder.Build();

            // 🟢 Tự động kiểm tra & cập nhật cột Permissions cho bảng Admins trong DB nếu thiếu
            using (var scope = app.Services.CreateScope())
            {
                try
                {
                    var db = scope.ServiceProvider.GetRequiredService<CinemaContext>();
                    db.Database.ExecuteSqlRaw(@"
                        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Admins' AND COLUMN_NAME = 'Permissions')
                        BEGIN
                            ALTER TABLE Admins ADD Permissions NVARCHAR(1000) NULL;
                        END
                    ");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Database Auto-Migration Error] {ex.Message}");
                }
            }

            // 🟢 Enable Swagger Middleware
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Cinezone API v1");
                c.RoutePrefix = "swagger";
            });

     

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
            app.MapHub<CINEMA.Hubs.SeatHub>("/seatHub");

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.Run();
        }
    }
}

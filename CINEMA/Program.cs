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
            builder.Services.AddScoped<RecommendationEngine>();
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

            // 🛠️ Tự động kiểm tra và tạo 3 bảng tracking nếu chưa có trong Database
            using (var scope = app.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<CinemaContext>();
                try
                {
                    context.Database.ExecuteSqlRaw(@"
                        IF OBJECT_ID('dbo.UserActivityLogs', 'U') IS NULL
                        BEGIN
                            CREATE TABLE [dbo].[UserActivityLogs] (
                                [LogId] bigint IDENTITY(1,1) NOT NULL,
                                [CustomerId] int NULL,
                                [SessionId] nvarchar(max) NULL,
                                [ActivityType] nvarchar(max) NOT NULL,
                                [MovieId] int NULL,
                                [GenreId] int NULL,
                                [Metadata] nvarchar(max) NULL,
                                [DeviceType] nvarchar(max) NULL,
                                [CreatedAt] datetime2 NOT NULL,
                                CONSTRAINT [PK_UserActivityLogs] PRIMARY KEY ([LogId]),
                                CONSTRAINT [FK_UserActivityLogs_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [dbo].[Customers] ([CustomerId]),
                                CONSTRAINT [FK_UserActivityLogs_Genres_GenreId] FOREIGN KEY ([GenreId]) REFERENCES [dbo].[Genres] ([GenreId]),
                                CONSTRAINT [FK_UserActivityLogs_Movies_MovieId] FOREIGN KEY ([MovieId]) REFERENCES [dbo].[Movies] ([MovieId])
                            );
                        END;

                        IF OBJECT_ID('dbo.UserMovieViews', 'U') IS NULL
                        BEGIN
                            CREATE TABLE [dbo].[UserMovieViews] (
                                [Id] bigint IDENTITY(1,1) NOT NULL,
                                [CustomerId] int NOT NULL,
                                [MovieId] int NOT NULL,
                                [ViewCount] int NOT NULL,
                                [LastViewedAt] datetime2 NOT NULL,
                                CONSTRAINT [PK_UserMovieViews] PRIMARY KEY ([Id]),
                                CONSTRAINT [FK_UserMovieViews_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [dbo].[Customers] ([CustomerId]) ON DELETE CASCADE,
                                CONSTRAINT [FK_UserMovieViews_Movies_MovieId] FOREIGN KEY ([MovieId]) REFERENCES [dbo].[Movies] ([MovieId]) ON DELETE CASCADE
                            );
                            CREATE UNIQUE INDEX [IX_UserMovieViews_CustomerId_MovieId] ON [dbo].[UserMovieViews] ([CustomerId], [MovieId]);
                        END;

                        IF OBJECT_ID('dbo.UserSearchLogs', 'U') IS NULL
                        BEGIN
                            CREATE TABLE [dbo].[UserSearchLogs] (
                                [Id] bigint IDENTITY(1,1) NOT NULL,
                                [CustomerId] int NULL,
                                [Keyword] nvarchar(max) NOT NULL,
                                [ResultCount] int NULL,
                                [CreatedAt] datetime2 NOT NULL,
                                CONSTRAINT [PK_UserSearchLogs] PRIMARY KEY ([Id]),
                                CONSTRAINT [FK_UserSearchLogs_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [dbo].[Customers] ([CustomerId])
                            );
                        END;
                    ");
                }
                catch (Exception)
                {
                    // Bỏ qua lỗi nếu database chưa sẵn sàng hoặc không đủ quyền
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

      

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.Run();
        }
    }
}

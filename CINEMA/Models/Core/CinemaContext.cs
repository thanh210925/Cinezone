using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http; // Thêm thư viện này
using System.Linq;
namespace CINEMA.Models;

public partial class CinemaContext : DbContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CinemaContext()
    {
    }

    public CinemaContext(DbContextOptions<CinemaContext> options, IHttpContextAccessor httpContextAccessor)
        : base(options)
    {
        _httpContextAccessor = httpContextAccessor;

    }
    public virtual DbSet<ActivityLog> ActivityLogs { get; set; }
    public virtual DbSet<Admin> Admins { get; set; }

    public virtual DbSet<Auditorium> Auditoriums { get; set; }

    public virtual DbSet<Combo> Combos { get; set; }

    public virtual DbSet<Customer> Customers { get; set; }

    public virtual DbSet<Genre> Genres { get; set; }

    public virtual DbSet<Movie> Movies { get; set; }

    public virtual DbSet<Order> Orders { get; set; }

    public virtual DbSet<OrderCombo> OrderCombos { get; set; }
    public virtual DbSet<Popup> Popups { get; set; }
    public virtual DbSet<Seat> Seats { get; set; }

    public virtual DbSet<Showtime> Showtimes { get; set; }

    public virtual DbSet<Theater> Theaters { get; set; }

    public virtual DbSet<Ticket> Tickets { get; set; }

    public virtual DbSet<TicketCombo> TicketCombos { get; set; }
    public virtual DbSet<Voucher> Vouchers { get; set; }
    public virtual DbSet<VoucherCondition> VoucherConditions { get; set; }
    public virtual DbSet<VoucherRule> VoucherRules { get; set; }
    public virtual DbSet<Review> Reviews { get; set; }
    public DbSet<UserActivityLog> UserActivityLogs { get; set; }
    public DbSet<UserMovieView> UserMovieViews { get; set; }
    public DbSet<UserSearchLog> UserSearchLogs { get; set; }
    public DbSet<ChatMessage> ChatMessages { get; set; }
    public virtual DbSet<GroupBookingRoom> GroupBookingRooms { get; set; }
    public virtual DbSet<GroupBookingMember> GroupBookingMembers { get; set; }
    public virtual DbSet<RefreshToken> RefreshTokens { get; set; }
    public virtual DbSet<MovieFollower> MovieFollowers { get; set; }
    //    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    //#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
    //        => optionsBuilder.UseSqlServer("Server=YOUR_SERVER;Database=CINEMA;User Id=YOUR_USER;Password=YOUR_PASSWORD;TrustServerCertificate=True;");

    // Các entity KHÔNG được ghi vào ActivityLogs (audit log dành riêng cho Admin).
    // Bao gồm chính ActivityLog và 3 bảng tracking hành vi khách hàng.
    private static bool ShouldSkipAudit(object entity)
    {
        return entity is ActivityLog
            || entity is UserActivityLog
            || entity is UserMovieView
            || entity is UserSearchLog
            || entity is ChatMessage
            || entity is GroupBookingRoom
            || entity is GroupBookingMember;
    }

    public override int SaveChanges()
    {
        var entries = ChangeTracker.Entries()
            .Where(e =>
                (e.State == EntityState.Added ||
         e.State == EntityState.Modified ||
         e.State == EntityState.Deleted))
    .ToList();

        var adminIdStr =
            _httpContextAccessor?
            .HttpContext?
            .Session
            .GetString("AdminId");

        int adminId =
            int.TryParse(adminIdStr, out int id)
            ? id
            : 0;

        // ❗ Chỉ ghi audit log khi có Admin thực sự đăng nhập (tránh vi phạm FK khi khách hàng thao tác)
        if (adminId > 0)
        {
            foreach (var entry in entries)
            {
                if (ShouldSkipAudit(entry.Entity))
                    continue;

                ActivityLogs.Add(
                    new ActivityLog
                    {
                        AdminId = adminId,
                        Action = entry.State.ToString(),
                        Entity = entry.Entity.GetType().Name,
                        EntityId = 0,
                        LogDate = DateTime.Now
                    });
            }
        }

        return base.SaveChanges();
    }
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entries = ChangeTracker.Entries()
        .Where(e =>
            e.State == EntityState.Added ||
            e.State == EntityState.Modified ||
            e.State == EntityState.Deleted)
        .ToList();

        var adminIdStr =
            _httpContextAccessor?
            .HttpContext?
            .Session?
            .GetString("AdminId");

        int adminId =
            int.TryParse(adminIdStr, out int id)
            ? id
            : 0;

        // ❗ Chỉ ghi audit log khi có Admin thực sự đăng nhập (tránh vi phạm FK khi khách hàng thao tác)
        if (adminId > 0)
        {
            foreach (var entry in entries)
            {
                if (ShouldSkipAudit(entry.Entity))
                    continue;

                int entityId = 0;

                try
                {
                    var pk =
                        entry.Metadata
                        .FindPrimaryKey()?
                        .Properties
                        .FirstOrDefault();

                    if (pk != null)
                    {
                        var value =
                            entry.Property(pk.Name)
                                 .CurrentValue;

                        if (value != null)
                            entityId =
                                Convert.ToInt32(value);
                    }
                }
                catch
                {
                    entityId = 0;
                }

                ActivityLogs.Add(
        new ActivityLog
        {
            AdminId = adminId,
            Action = entry.State switch
            {
                EntityState.Added => "THÊM",
                EntityState.Modified => "SỬA",
                EntityState.Deleted => "XÓA",
                _ => ""
            },
            Entity = entry.Entity.GetType().Name,
            EntityId = entityId,
            LogDate = DateTime.Now
        });
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.Property(e => e.ReputationScore)
                  .HasConversion(
                      v => v,
                      v => Convert.ToDouble(v)
                  );
        });

        modelBuilder.Entity<VoucherCondition>(entity =>
        {
            entity.HasKey(e => e.VoucherConditionId);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(150);
            entity.Property(e => e.Description).HasMaxLength(500);
        });

        modelBuilder.Entity<Voucher>(entity =>
        {
            entity.HasOne(d => d.VoucherCondition)
                  .WithMany(p => p.Vouchers)
                  .HasForeignKey(d => d.VoucherConditionId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<VoucherRule>(entity =>
        {
            entity.HasKey(e => e.VoucherRuleId);
            entity.Property(e => e.Field).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Operator).IsRequired().HasMaxLength(30);
            entity.Property(e => e.Value).IsRequired().HasMaxLength(150);

            entity.HasOne(d => d.VoucherCondition)
                  .WithMany(p => p.Rules)
                  .HasForeignKey(d => d.VoucherConditionId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GroupBookingRoom>(entity =>
        {
            entity.HasKey(e => e.RoomId);
            entity.Property(e => e.RoomId).HasMaxLength(20);
            entity.Property(e => e.Status).HasMaxLength(30).HasDefaultValue("Waiting");
            entity.Property(e => e.MaxMembers).HasDefaultValue(10);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");

            entity.HasOne(d => d.Showtime)
                .WithMany()
                .HasForeignKey(d => d.ShowtimeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Creator)
                .WithMany()
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<GroupBookingMember>(entity =>
        {
            entity.HasKey(e => e.MemberId);
            entity.Property(e => e.RoomId).HasMaxLength(20);
            entity.Property(e => e.Status).HasMaxLength(30).HasDefaultValue("Joined");
            entity.Property(e => e.JoinedAt).HasDefaultValueSql("GETDATE()");

            entity.HasOne(d => d.Room)
                .WithMany(p => p.Members)
                .HasForeignKey(d => d.RoomId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Customer)
                .WithMany()
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.Seat)
                .WithMany()
                .HasForeignKey(d => d.SeatId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(d => d.Order)
                .WithMany()
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<UserMovieView>()
            .HasIndex(v => new { v.CustomerId, v.MovieId })
            .IsUnique();

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasKey(e => e.ChatMessageId);
            entity.Property(e => e.MessageText).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
            entity.Property(e => e.IsRead).HasDefaultValue(false);

            entity.HasOne(d => d.Customer)
                .WithMany()
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
        });




        modelBuilder.Entity<Auditorium>(entity =>
        {
            entity.HasKey(e => e.AuditoriumId).HasName("PK__Auditori__6E91B1859F665F38");

            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(50);
            entity.Property(e => e.ScreenType).HasMaxLength(50);

            entity.HasOne(d => d.Theater).WithMany(p => p.Auditoria)
                .HasForeignKey(d => d.TheaterId)
                .HasConstraintName("FK__Auditoriu__Theat__398D8EEE");
        });

        modelBuilder.Entity<Combo>(entity =>
        {
            entity.HasKey(e => e.ComboId).HasName("PK__Combos__DD42582E26B5F1BA");

            entity.Property(e => e.Description).HasMaxLength(200);
            entity.Property(e => e.ImageUrl).HasMaxLength(300);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Price).HasColumnType("decimal(10, 2)");

            entity.HasMany(d => d.Showtimes).WithMany(p => p.Combos)
                .UsingEntity<Dictionary<string, object>>(
                    "ComboShowtime",
                    r => r.HasOne<Showtime>().WithMany()
                        .HasForeignKey("ShowtimeId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK__ComboShow__Showt__52593CB8"),
                    l => l.HasOne<Combo>().WithMany()
                        .HasForeignKey("ComboId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK__ComboShow__Combo__5165187F"),
                    j =>
                    {
                        j.HasKey("ComboId", "ShowtimeId").HasName("PK__ComboSho__CE6F69DC9A4BAFFE");
                        j.ToTable("ComboShowtimes");
                    });
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(e => e.CustomerId).HasName("PK__Customer__A4AE64D84DA01D1E");

            entity.HasIndex(e => e.Email, "UQ__Customer__A9D10534C763062F").IsUnique();

            entity.Property(e => e.Address).HasMaxLength(255);
            entity.Property(e => e.AvatarUrl).HasMaxLength(500);
            entity.Property(e => e.City).HasMaxLength(100);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.Gender).HasMaxLength(10);
            entity.Property(e => e.LastLogin).HasColumnType("datetime");
            entity.Property(e => e.PasswordHash).HasMaxLength(200);
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.Region).HasMaxLength(100);
        });

        modelBuilder.Entity<Genre>(entity =>
        {
            entity.HasKey(e => e.GenreId).HasName("PK__Genres__0385057E94DA3A45");

            entity.Property(e => e.Description).HasMaxLength(200);
            entity.Property(e => e.Name).HasMaxLength(50);
        });

        modelBuilder.Entity<Movie>(entity =>
        {
            entity.HasKey(e => e.MovieId).HasName("PK__Movies__4BD2941A4A7171D7");

            entity.Property(e => e.AgeRating).HasMaxLength(20);
            entity.Property(e => e.Country).HasMaxLength(50);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Language).HasMaxLength(50);
            entity.Property(e => e.PosterUrl).HasMaxLength(300);
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.TrailerUrl).HasMaxLength(300);

            entity.HasMany(d => d.Genres).WithMany(p => p.Movies)
                .UsingEntity<Dictionary<string, object>>(
                    "MovieGenre",
                    r => r.HasOne<Genre>().WithMany()
                        .HasForeignKey("GenreId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK__MovieGenr__Genre__32E0915F"),
                    l => l.HasOne<Movie>().WithMany()
                        .HasForeignKey("MovieId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK__MovieGenr__Movie__31EC6D26"),
                    j =>
                    {
                        j.HasKey("MovieId", "GenreId").HasName("PK__MovieGen__BBEAC44DCA069280");
                        j.ToTable("MovieGenres");
                    });
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.OrderId).HasName("PK__Orders__C3905BCF0301CE9E");

            entity.Property(e => e.ComboTotal).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.PaidAt).HasColumnType("datetime");
            entity.Property(e => e.PaymentMethod).HasMaxLength(100);
            entity.Property(e => e.Status).HasMaxLength(20);
            entity.Property(e => e.TicketTotal).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(12, 2)");

            entity.HasOne(d => d.Customer).WithMany(p => p.Orders)
                .HasForeignKey(d => d.CustomerId)
                .HasConstraintName("FK__Orders__Customer__4BAC3F29");
            entity.Property(e => e.VoucherCode)
      .HasMaxLength(50);

            entity.Property(e => e.DiscountAmount)
                  .HasColumnType("decimal(18,2)"); entity.Property(e => e.VoucherCode)
      .HasMaxLength(50);

            entity.Property(e => e.DiscountAmount)
                  .HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<OrderCombo>(entity =>
        {
            entity.HasKey(e => e.OrderComboId).HasName("PK__OrderCom__4D378B401B930494");

            entity.Property(e => e.UnitPrice).HasColumnType("decimal(10, 2)");

            entity.HasOne(d => d.Combo).WithMany(p => p.OrderCombos)
                .HasForeignKey(d => d.ComboId)
                .HasConstraintName("FK__OrderComb__Combo__5629CD9C");

            entity.HasOne(d => d.Order).WithMany(p => p.OrderCombos)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("FK__OrderComb__Order__5535A963");
        });

        modelBuilder.Entity<Seat>(entity =>
        {
            entity.HasKey(e => e.SeatId).HasName("PK__Seats__311713F3E167E786");

            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.RowLabel).HasMaxLength(5);
            entity.Property(e => e.SeatType).HasMaxLength(20);

            entity.HasOne(d => d.Auditorium).WithMany(p => p.Seats)
                .HasForeignKey(d => d.AuditoriumId)
                .HasConstraintName("FK__Seats__Auditoriu__3D5E1FD2");
        });

        modelBuilder.Entity<Showtime>(entity =>
        {
            entity.HasKey(e => e.ShowtimeId).HasName("PK__Showtime__32D31F2027DD3D86");

            entity.Property(e => e.BasePrice).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.EndTime).HasColumnType("datetime");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Language).HasMaxLength(50);
            entity.Property(e => e.StartTime).HasColumnType("datetime");

            entity.HasOne(d => d.Auditorium).WithMany(p => p.Showtimes)
                .HasForeignKey(d => d.AuditoriumId)
                .HasConstraintName("FK__Showtimes__Audit__4222D4EF");

            entity.HasOne(d => d.Movie).WithMany(p => p.Showtimes)
                .HasForeignKey(d => d.MovieId)
                .HasConstraintName("FK__Showtimes__Movie__412EB0B6");
        });

        modelBuilder.Entity<Theater>(entity =>
        {
            entity.HasKey(e => e.TheaterId).HasName("PK__Theaters__4D68B2190D1064CB");

            entity.Property(e => e.Address).HasMaxLength(200);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.GoogleMapUrl).HasMaxLength(300);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Phone).HasMaxLength(20);
        });

        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.HasKey(e => e.TicketId).HasName("PK__Tickets__712CC6079D9A7F93");

            entity.Property(e => e.BookedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.PaymentStatus).HasMaxLength(20);
            entity.Property(e => e.Price).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.Status).HasMaxLength(20);

            entity.HasOne(d => d.Customer).WithMany(p => p.Tickets)
                .HasForeignKey(d => d.CustomerId)
                .HasConstraintName("FK__Tickets__Custome__47DBAE45");

            entity.HasOne(d => d.Order).WithMany(p => p.Tickets)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_Tickets_Orders");

            entity.HasOne(d => d.Seat).WithMany(p => p.Tickets)
                .HasForeignKey(d => d.SeatId)
                .HasConstraintName("FK__Tickets__SeatId__46E78A0C");

            entity.HasOne(d => d.Showtime).WithMany(p => p.Tickets)
                .HasForeignKey(d => d.ShowtimeId)
                .HasConstraintName("FK__Tickets__Showtim__45F365D3");
        });

        modelBuilder.Entity<TicketCombo>(entity =>
        {
            entity.HasKey(e => new { e.TicketId, e.OrderComboId }).HasName("PK__TicketCo__65FFBEB30515E3E9");

            entity.HasOne(d => d.OrderCombo).WithMany(p => p.TicketCombos)
                .HasForeignKey(d => d.OrderComboId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__TicketCom__Order__59FA5E80");

            entity.HasOne(d => d.Ticket).WithMany(p => p.TicketCombos)
                .HasForeignKey(d => d.TicketId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__TicketCom__Ticke__59063A47");
        });
        modelBuilder.Entity<Popup>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Title).HasMaxLength(200);

            entity.Property(e => e.Description).HasMaxLength(500);

            entity.Property(e => e.ImageUrl).HasMaxLength(300);

            entity.Property(e => e.ButtonLink).HasMaxLength(300);

            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<Branch>(entity =>
        {
            entity.HasKey(e => e.BranchId);

            entity.Property(e => e.BranchCode)
                  .HasMaxLength(20);

            entity.Property(e => e.BranchName)
                  .HasMaxLength(100);

            entity.Property(e => e.Address)
                  .HasMaxLength(255);

            entity.Property(e => e.Phone)
                  .HasMaxLength(20);

            entity.Property(e => e.Email)
                  .HasMaxLength(100);
        });

        modelBuilder.Entity<Position>(entity =>
        {
            entity.HasKey(e => e.PositionId);

            entity.Property(e => e.PositionName)
                  .HasMaxLength(100);

            entity.Property(e => e.Description)
                  .HasMaxLength(255);
        });

        modelBuilder.Entity<Admin>(entity =>
        {
            entity.HasKey(e => e.AdminId)
      .HasName("PK__Admins__719FE4888C53A24F");

            entity.HasIndex(e => e.Email,
                  "UQ__Admins__A9D10534147CE3A1")
                  .IsUnique();

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("(getdate())")
                  .HasColumnType("datetime");

            entity.Property(e => e.Email)
                  .HasMaxLength(100);

            entity.Property(e => e.FullName)
                  .HasMaxLength(100);

            entity.Property(e => e.LastLogin)
                  .HasColumnType("datetime");

            entity.Property(e => e.PasswordHash)
                  .HasMaxLength(200);

            entity.Property(e => e.Phone)
                  .HasMaxLength(20);

            // THÊM PHẦN NÀY
            entity.HasOne(d => d.Branch)
      .WithMany(p => p.Admins)
      .HasForeignKey(d => d.BranchId);

            entity.HasOne(d => d.Position)
                  .WithMany(p => p.Admins)
                  .HasForeignKey(d => d.PositionId);
        });
        OnModelCreatingPartial(modelBuilder);

        modelBuilder.Entity<ActivityLog>(entity =>
        {
            entity.HasKey(e => e.LogId);

            entity.Property(e => e.Action)
                .HasMaxLength(50);

            entity.Property(e => e.Entity)
                .HasMaxLength(100);

            entity.Property(e => e.LogDate)
                .HasColumnType("datetime");

            entity.HasOne(e => e.Admin)
      .WithMany(a => a.ActivityLogs)
      .HasForeignKey(e => e.AdminId)
      .OnDelete(DeleteBehavior.Restrict);
        });


        // Cấu hình bảng Chấm công
        modelBuilder.Entity<Attendance>(entity =>
        {
            entity.HasKey(e => e.AttendanceId);
            entity.Property(e => e.Date).HasColumnType("date");
            entity.Property(e => e.CheckInTime).HasColumnType("datetime");
            entity.Property(e => e.CheckOutTime).HasColumnType("datetime");
            entity.Property(e => e.CheckInPhoto).HasMaxLength(500);
            entity.Property(e => e.CheckOutPhoto).HasMaxLength(500);

            entity.HasOne(d => d.Admin)
                  .WithMany() // Nếu Admin không có danh sách Attendance
                  .HasForeignKey(d => d.AdminId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Cấu hình bảng Nghỉ phép
        modelBuilder.Entity<LeaveRequest>(entity =>
        {
            entity.HasKey(e => e.RequestId);
            entity.Property(e => e.Status).HasMaxLength(50).HasDefaultValue("Chờ duyệt");
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");

            entity.HasOne(d => d.Admin)
                  .WithMany() // Nếu Admin không có danh sách LeaveRequest
                  .HasForeignKey(d => d.AdminId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Cấu hình bảng Lương (Payroll)
        modelBuilder.Entity<Payroll>(entity =>
        {
            entity.ToTable("Payrolls");
            entity.HasKey(e => e.PayrollId);
            entity.Property(e => e.WorkingHours).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.PaidLeaveHours).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.SalaryCoefficient).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.BaseSalaryPerHour).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Bonus).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Deductions).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TotalSalary).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Status).HasMaxLength(20);
            entity.Property(e => e.Notes).HasMaxLength(500);

            entity.HasOne(d => d.Admin)
                  .WithMany(p => p.Payrolls)
                  .HasForeignKey(d => d.AdminId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserActivityLog>()
    .HasKey(x => x.LogId);

    }


    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);




    public DbSet<Branch> Branches { get; set; }
    public DbSet<Position> Positions { get; set; }

    public DbSet<Shift> Shifts { get; set; }
    public DbSet<WorkSchedule> WorkSchedules { get; set; }


    public virtual DbSet<Attendance> Attendance { get; set; }
    public DbSet<FaceEmbedding> FaceEmbeddings { get; set; }
    public virtual DbSet<LeaveRequest> LeaveRequests { get; set; }
    public virtual DbSet<LeaveType> LeaveTypes { get; set; }
    public virtual DbSet<Payroll> Payrolls { get; set; }
}

// Import các thực thể Entity từ tầng Core
using AudioGuide.Core.Entities;
// Import giao diện trừu tượng DbContext từ tầng Core
using AudioGuide.Core.Interfaces;
// Import các thành phần của Entity Framework Core
using Microsoft.EntityFrameworkCore;
// Import đối tượng quản lý Transaction của EF Core
using Microsoft.EntityFrameworkCore.Storage;

namespace AudioGuide.Infrastructure.Data;

// Lớp DbContext quản lý kết nối và ánh xạ CSDL cho hệ thống AudioGuide
public class AppDbContext : DbContext
{
    // Constructor tiếp nhận DbContextOptions để cấu hình chuỗi kết nối và SQL Server
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<AdminAccount> AdminAccounts => Set<AdminAccount>();

    // Bảng dữ liệu điểm tham quan
    public DbSet<Poi> Pois => Set<Poi>();

    // Bảng dữ liệu các bản dịch đa ngôn ngữ cho POI
    public DbSet<PoiTranslation> PoiTranslations => Set<PoiTranslation>();

    // Bảng dữ liệu mã QR gắn với POI
    public DbSet<QrCode> QrCodes => Set<QrCode>();

    // Bảng theo dõi phiên thiết bị của du khách
    public DbSet<DeviceSession> DeviceSessions => Set<DeviceSession>();

    // Bảng nhật ký ghi nhận các lượt quét mã QR theo thời gian thực
    public DbSet<QrScanLog> QrScanLogs => Set<QrScanLog>();

    // Mở transaction bất đồng bộ hỗ trợ hủy qua cancellationToken
    public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        // Khởi tạo transaction cơ sở dữ liệu
        return await Database.BeginTransactionAsync(cancellationToken);
    }

    // Cấu hình các ràng buộc, chỉ mục và quan hệ giữa các bảng bằng Fluent API
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Thực thi cấu hình mặc định của lớp cha
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AdminAccount>(entity =>
        {
            entity.ToTable("AdminAccounts", "dbo",
                table => table.HasCheckConstraint("CK_AdminAccounts_SingleAccount", "[Id] = 1"));
            entity.HasKey(account => account.Id);
            entity.Property(account => account.Id).ValueGeneratedNever();
            entity.Property(account => account.Username).HasMaxLength(100).IsRequired();
            entity.Property(account => account.PasswordHash).HasMaxLength(512).IsRequired();
            entity.Property(account => account.Version).IsConcurrencyToken();
        });

        // 1. Cấu hình bảng POI (Point of Interest)
        modelBuilder.Entity<Poi>(entity =>
        {
            // Thiết lập khóa chính
            entity.HasKey(p => p.Id);
            entity.Property(p => p.RowVersion).IsRowVersion();

            // Cấu hình trường Code bắt buộc, tối đa 50 ký tự và duy nhất
            entity.Property(p => p.Code).HasMaxLength(50).IsRequired();
            entity.HasIndex(p => p.Code).IsUnique();

            // Cấu hình tầng mặc định là 0 (Ngoài trời/Trệt)
            entity.Property(p => p.Floor).HasDefaultValue(0).IsRequired();

            // Cấu hình bán kính kích hoạt tự động mặc định là 5 mét
            entity.Property(p => p.TriggerRadiusMeters).HasDefaultValue(5.0).IsRequired();

            // Cấu hình độ dài tối đa đường dẫn ảnh đại diện
            entity.Property(p => p.ImageUrl).HasMaxLength(500);

            // Chỉ định kiểu dữ liệu địa lý geography trên SQL Server cho thuộc tính Location (NetTopologySuite)
            entity.Property(p => p.Location).HasColumnType("geography");

            // Tạo chỉ mục cho trường Floor để tối ưu lọc danh sách POI theo tầng
            entity.HasIndex(p => p.Floor);

            // Tạo chỉ mục cho trường IsActive để tối ưu lọc POI đang hoạt động
            entity.HasIndex(p => p.IsActive);
        });

        // 2. Cấu hình bảng POI Translations (Thuyết minh đa ngôn ngữ)
        modelBuilder.Entity<PoiTranslation>(entity =>
        {
            // Thiết lập khóa chính
            entity.HasKey(t => t.Id);

            // Cấu hình mã ngôn ngữ (tối đa 10 ký tự, bắt buộc nhập)
            entity.Property(t => t.LanguageCode).HasMaxLength(10).IsRequired();

            // Cấu hình tiêu đề thuyết minh (tối đa 200 ký tự, bắt buộc nhập)
            entity.Property(t => t.Title).HasMaxLength(200).IsRequired();

            // Cấu hình đường dẫn file audio mp3 (tối đa 500 ký tự, bắt buộc nhập)
            entity.Property(t => t.AudioUrl).HasMaxLength(500).IsRequired();

            // Cấu hình thời lượng file audio mặc định 30 giây
            entity.Property(t => t.DurationSeconds).HasDefaultValue(30);

            // Đảm bảo mỗi POI chỉ có duy nhất 1 bản dịch cho từng mã ngôn ngữ
            entity.HasIndex(t => new { t.PoiId, t.LanguageCode }).IsUnique();

            // Cấu hình quan hệ nhiều-1 với POI và xóa tầng (Cascade Delete khi POI bị xóa)
            entity.HasOne(t => t.Poi)
                  .WithMany(p => p.Translations)
                  .HasForeignKey(t => t.PoiId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // 3. Cấu hình bảng QR Code (Quan hệ 1 - 1 với POI)
        modelBuilder.Entity<QrCode>(entity =>
        {
            // Thiết lập khóa chính
            entity.HasKey(q => q.Id);

            // Cấu hình token QR tối đa 64 ký tự và không được trùng lặp
            entity.Property(q => q.QrToken).HasMaxLength(64).IsRequired();
            entity.HasIndex(q => q.QrToken).IsUnique();

            // Đảm bảo mỗi POI chỉ gắn duy nhất với 1 mã QR
            entity.HasIndex(q => q.PoiId).IsUnique();

            // Cấu hình quan hệ 1-1 với POI và xóa cascade
            entity.HasOne(q => q.Poi)
                  .WithOne(p => p.QrCode)
                  .HasForeignKey<QrCode>(q => q.PoiId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // 4. Cấu hình bảng QrScanLog (Lịch sử lượt quét QR thời gian thực)
        modelBuilder.Entity<QrScanLog>(entity =>
        {
            // Thiết lập khóa chính
            entity.HasKey(l => l.Id);
            entity.Property(l=>l.EventType).HasMaxLength(20).IsRequired();
            entity.Property(l=>l.VisitorId).HasMaxLength(100);
            entity.Property(l=>l.LanguageCode).HasMaxLength(10).IsRequired();
            entity.HasIndex(l=>l.EventId).IsUnique().HasFilter("[EventId] IS NOT NULL");
            entity.HasIndex(l=>new {l.EventType,l.ScannedAt});
            entity.HasIndex(l=>new {l.VisitorId,l.ScannedAt});
            entity.HasOne<Poi>().WithMany().HasForeignKey(l=>l.PoiId).OnDelete(DeleteBehavior.SetNull);

            // Cấu hình độ dài trường token QR
            entity.Property(l => l.QrToken).HasMaxLength(64).IsRequired();

            // Cấu hình mã POI lưu snapshot
            entity.Property(l => l.PoiCode).HasMaxLength(50);

            // Cấu hình địa chỉ IP máy khách
            entity.Property(l => l.IpAddress).HasMaxLength(45);

            // Cấu hình chuỗi User-Agent trình duyệt
            entity.Property(l => l.UserAgent).HasMaxLength(500);

            // Thiết lập giá trị mặc định cho thời điểm quét bằng giờ chuẩn UTC trên SQL Server
            entity.Property(l => l.ScannedAt).HasDefaultValueSql("GETUTCDATE()");

            // Tạo chỉ mục cho thời gian quét để phục vụ truy vấn báo cáo và biểu đồ
            entity.HasIndex(l => l.ScannedAt);

            // Tạo chỉ mục cho QrToken để tra cứu nhanh tần suất quét
            entity.HasIndex(l => l.QrToken);
        });

        // 5. Cấu hình bảng DeviceSession (Theo dõi phiên thiết bị)
        modelBuilder.Entity<DeviceSession>(entity =>
        {
            // Thiết lập khóa chính
            entity.HasKey(d => d.Id);

            // Cấu hình mã nhận diện phần cứng/trình duyệt khớp với thuộc tính DeviceFingerprint của Entity
            entity.Property(d => d.DeviceFingerprint).HasMaxLength(100).IsRequired();

            // Cấu hình các thông số mạng và hệ điều hành của thiết bị
            entity.Property(d => d.IpAddress).HasMaxLength(50);
            entity.Property(d => d.DeviceName).HasMaxLength(100);
            entity.Property(d => d.Platform).HasMaxLength(50);
            entity.Property(d => d.Browser).HasMaxLength(50);

            // Cấu hình ngôn ngữ ưa thích mặc định là tiếng Việt
            entity.Property(d => d.PreferredLanguage).HasMaxLength(10).HasDefaultValue("vi");

            // Thiết lập thời gian hoạt động và thời gian tạo mặc định là UTC
            entity.Property(d => d.LastActiveAt).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(d => d.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            // Đánh chỉ mục Unique cho mã vân tay thiết bị để tìm nhanh phiên
            entity.HasIndex(d => d.DeviceFingerprint).IsUnique();

            // Đánh chỉ mục cho thời gian hoạt động gần nhất để thống kê lượng khách đang online
            entity.HasIndex(d => d.LastActiveAt);
        });
    }
}
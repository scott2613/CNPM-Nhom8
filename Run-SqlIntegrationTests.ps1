# Chạy nhóm SqlIntegrationTests của AudioGuide trên database kiểm thử AudioGuideCi.
# Giải nén file này cạnh AudioGuideSolution.sln, rồi chạy bằng PowerShell.

# Nhận tên SQL Server và tùy chọn lưu cấu hình cho Test Explorer.
param(
    # Dùng mặc định cùng instance SQL Server với bản AudioGuide đã gửi.
    [string]$SqlServer = 'localhost\MSSQLSERVER03',

    # Khi có tùy chọn này, lưu biến kết nối kiểm thử cho tài khoản Windows hiện tại.
    [switch]$ConfigureTestExplorer
) # Kết thúc phần tham số.

# Dừng script khi PowerShell gặp lỗi.
$ErrorActionPreference = 'Stop'

# Tính đường dẫn solution dựa vào vị trí file script.
$solution = Join-Path $PSScriptRoot 'AudioGuideSolution.sln'

# Xác nhận người dùng đã đặt script vào thư mục gốc dự án.
if (-not (Test-Path -LiteralPath $solution -PathType Leaf)) {
    # Báo vị trí cần đặt file để các đường dẫn dự án hoạt động.
    throw 'Hãy đặt Run-SqlIntegrationTests.ps1 cạnh AudioGuideSolution.sln rồi chạy lại.'
} # Kết thúc kiểm tra vị trí.

# Kiểm tra lệnh dotnet trước khi chạy công cụ database.
if (-not (Get-Command dotnet -CommandType Application -ErrorAction SilentlyContinue)) {
    # Yêu cầu SDK .NET được khai báo trong global.json.
    throw 'Không tìm thấy dotnet. Hãy cài .NET SDK theo global.json rồi mở lại PowerShell.'
} # Kết thúc kiểm tra dotnet.

# Từ chối tên máy chủ rỗng hoặc chứa dấu chấm phẩy của chuỗi kết nối.
if ([string]::IsNullOrWhiteSpace($SqlServer) -or $SqlServer.Contains(';')) {
    # Chỉ chấp nhận tên instance hoặc địa chỉ máy chủ.
    throw 'Tham số SqlServer chỉ nhận tên máy chủ, ví dụ localhost\MSSQLSERVER03.'
} # Kết thúc kiểm tra tham số.

# Dùng bộ tạo chuỗi kết nối để tách riêng từng thuộc tính.
$connection = New-Object System.Data.Common.DbConnectionStringBuilder

# Chọn instance SQL Server theo tham số đã xác thực.
$connection['Server'] = $SqlServer.Trim()

# Cố định tên database kiểm thử vì test có ghi và dọn dữ liệu.
$connection['Database'] = 'AudioGuideCi'

# Dùng tài khoản Windows hiện tại như cấu hình phát triển của AudioGuide.
$connection['Trusted_Connection'] = $true

# Bật tùy chọn dành cho cấu hình SQL Server phát triển cục bộ đang dùng.
$connection['TrustServerCertificate'] = $true

# Lưu lại hai biến môi trường để hoàn trả sau khi script kết thúc.
$previousDatabaseConnection = $env:ConnectionStrings__DefaultConnection

# Lưu chuỗi kết nối test đã có, nếu có.
$previousTestConnection = $env:AUDIOGUIDE_TEST_SQL

# Chuyển tới thư mục gốc để dotnet đọc global.json và tìm được thư mục sql.
Push-Location -LiteralPath $PSScriptRoot

# Đảm bảo khôi phục thư mục và biến môi trường kể cả khi lệnh thất bại.
try {
    # Cho công cụ Database biết phải tạo schema trong AudioGuideCi.
    $env:ConnectionStrings__DefaultConnection = $connection.ConnectionString

    # Cho bộ test biết chuỗi kết nối riêng của SQL Server kiểm thử.
    $env:AUDIOGUIDE_TEST_SQL = $connection.ConnectionString

    # Hiển thị đích chạy test, gồm máy chủ và tên database.
    Write-Host "Chuẩn bị database AudioGuideCi trên $SqlServer."

    # Tạo database nếu cần và áp dụng schema bằng công cụ sẵn có trong dự án.
    dotnet run --project .\tools\Database\Database.csproj -- ci-create sql

    # Kiểm tra mã thoát vì dotnet là chương trình chạy bên ngoài PowerShell.
    if ($LASTEXITCODE -ne 0) {
        # Dừng trước khi test nếu bước tạo database hoặc áp dụng schema lỗi.
        throw "Chuẩn bị database thất bại (mã thoát: $LASTEXITCODE). Xem lỗi SQL ở phía trên."
    } # Kết thúc kiểm tra chuẩn bị database.

    # Hoàn trả cấu hình mặc định ngay khi công cụ Database dùng xong.
    $env:ConnectionStrings__DefaultConnection = $previousDatabaseConnection

    # Chạy riêng nhóm SqlIntegrationTests bằng biến AUDIOGUIDE_TEST_SQL.
    dotnet test .\AudioGuide.Tests\AudioGuide.Tests.csproj --filter 'FullyQualifiedName~AudioGuide.Tests.SqlIntegrationTests'

    # Xác nhận kết quả thực thi của dotnet test.
    if ($LASTEXITCODE -ne 0) {
        # Báo lỗi và giữ nguyên chi tiết test do dotnet in ra.
        throw "SqlIntegrationTests thất bại (mã thoát: $LASTEXITCODE). Xem kết quả test ở phía trên."
    } # Kết thúc kiểm tra kết quả test.

    # Chỉ lưu cấu hình lâu dài nếu người dùng chọn ConfigureTestExplorer.
    if ($ConfigureTestExplorer) {
        # Lưu biến test cho tài khoản Windows hiện tại để Visual Studio có thể đọc khi mở lại.
        [Environment]::SetEnvironmentVariable('AUDIOGUIDE_TEST_SQL', $connection.ConnectionString, 'User')

        # Nhắc mở lại Visual Studio để Test Explorer nhận biến môi trường đã lưu.
        Write-Host 'Đã lưu cấu hình kiểm thử. Hãy đóng và mở lại Visual Studio, rồi chạy SqlIntegrationTests trong Test Explorer.'
    } # Kết thúc tùy chọn Test Explorer.
} # Kết thúc quá trình chuẩn bị và chạy test.
finally { # Chạy khối dọn cấu hình trong mọi trường hợp.
    # Khôi phục cấu hình database của phiên PowerShell như trước khi chạy script.
    $env:ConnectionStrings__DefaultConnection = $previousDatabaseConnection

    # Khôi phục giá trị test trong phiên hiện tại. Giá trị User được lưu riêng nếu đã chọn.
    $env:AUDIOGUIDE_TEST_SQL = $previousTestConnection

    # Quay về thư mục ban đầu của PowerShell.
    Pop-Location
} # Hoàn tất dọn cấu hình tạm.

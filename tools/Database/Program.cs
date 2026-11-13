using AudioGuide.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;
var mode=args.FirstOrDefault()??"";
var options=new DbContextOptionsBuilder<AppDbContext>().UseSqlServer("Server=localhost;Database=AudioGuideDb;Trusted_Connection=True",o=>o.UseNetTopologySuite()).Options;
if(mode=="generate"){using var db=new AppDbContext(options);Console.Write(db.Database.GenerateCreateScript());return;}
if(mode=="ci-create"){
 var raw=Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")??throw new InvalidOperationException("Missing test connection");
 var cs=new SqlConnectionStringBuilder(raw);
 if(cs.InitialCatalog!="AudioGuideCi")throw new InvalidOperationException("ci-create only accepts AudioGuideCi.");
 cs.InitialCatalog="master";
 for(var attempt=0;;attempt++)try{await using var master=new SqlConnection(cs.ConnectionString);await master.OpenAsync();await using var cmd=new SqlCommand("IF DB_ID(N'AudioGuideCi') IS NULL CREATE DATABASE AudioGuideCi",master);await cmd.ExecuteNonQueryAsync();break;}catch(SqlException) when(attempt<24){await Task.Delay(2500);}
 mode="fresh";
}
if(mode is not ("fresh" or "upgrade" or "accounts"))throw new ArgumentException("Usage: Database fresh|upgrade|accounts [sql-directory]; generate prints the fresh schema. Database must already exist.");
var connection=Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")??throw new InvalidOperationException("Set ConnectionStrings__DefaultConnection.");
var folder=args.Length>1?args[1]:"sql";
await using var sql=new SqlConnection(connection);await sql.OpenAsync();await using var tx=(SqlTransaction)await sql.BeginTransactionAsync();
async Task<object?> Execute(string statement){await using var cmd=new SqlCommand(statement,sql,tx){CommandTimeout=120};return await cmd.ExecuteScalarAsync();}
await Execute("DECLARE @r int; EXEC @r=sp_getapplock @Resource=N'AudioGuideSchema',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=30000; IF @r<0 THROW 51000,'Schema lock failed',1;");
await Execute("IF OBJECT_ID('dbo.SchemaVersions') IS NULL CREATE TABLE dbo.SchemaVersions(Name nvarchar(200) PRIMARY KEY, Sha256 char(64) NOT NULL, AppliedAtUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME());");
if (mode != "accounts")
    await ApplyAsync(mode == "fresh" ? "001_InitialSchema.sql" : "002_UpgradeLegacy.sql");
await ApplyAsync("005_AdminAccounts.sql");
await tx.CommitAsync();

async Task ApplyAsync(string file)
{
    var content = await File.ReadAllTextAsync(Path.Combine(folder, file));
    if (file == "005_AdminAccounts.sql")
        content = content.ReplaceLineEndings("\n");
    var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
    await using var check = new SqlCommand("SELECT Sha256 FROM dbo.SchemaVersions WHERE Name=@name", sql, tx);
    check.Parameters.AddWithValue("@name", file);
    if (await check.ExecuteScalarAsync() is string previous)
    {
        if (previous != hash)
            throw new InvalidOperationException("Applied script has changed; add a new version instead: " + file);
        Console.WriteLine("Already applied: " + file);
        return;
    }

    if (file is "001_InitialSchema.sql" or "002_UpgradeLegacy.sql")
    {
        var exists = (int)(await Execute("SELECT COUNT(*) FROM sys.tables WHERE name='Pois' AND schema_id=SCHEMA_ID('dbo')"))!;
        if (mode == "fresh" && exists > 0)
            throw new InvalidOperationException("Existing database detected. Use upgrade after backup.");
        if (mode == "upgrade" && exists == 0)
            throw new InvalidOperationException("No legacy schema. Use fresh.");
    }

    foreach (var batch in Regex.Split(content, @"^\s*GO\s*(?:--[^\r\n]*)?$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
        if (!string.IsNullOrWhiteSpace(batch))
            await Execute(batch);

    await using var insert = new SqlCommand("INSERT dbo.SchemaVersions(Name,Sha256) VALUES(@name,@hash)", sql, tx);
    insert.Parameters.AddWithValue("@name", file);
    insert.Parameters.AddWithValue("@hash", hash);
    await insert.ExecuteNonQueryAsync();
    Console.WriteLine("Applied: " + file);
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HongBeautyPOS.Infrastructure;

/// <summary>Used only by `dotnet ef` so tooling does not need to boot the web host. Reads DATABASE_URL like the app does.</summary>
public sealed class PosDbContextFactory : IDesignTimeDbContextFactory<PosDbContext>
{
	public PosDbContext CreateDbContext(string[] args)
	{
		var url = Environment.GetEnvironmentVariable("DATABASE_URL") ?? "Host=localhost;Database=design;Username=design;Password=design";
		var options = new DbContextOptionsBuilder<PosDbContext>().UseNpgsql(DatabaseConnection.Normalize(url)).Options;
		return new PosDbContext(options);
	}
}

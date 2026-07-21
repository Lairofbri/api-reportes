using Microsoft.EntityFrameworkCore;

namespace ReportesAPI.Datos;

public class ReportesDbContext : DbContext
{
    public ReportesDbContext(DbContextOptions<ReportesDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("public");
    }
}

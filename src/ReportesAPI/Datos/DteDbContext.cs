using Microsoft.EntityFrameworkCore;

namespace ReportesAPI.Datos;

public class DteDbContext : DbContext
{
    public DteDbContext(DbContextOptions<DteDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("public");
        modelBuilder.HasAnnotation("Relational:DbName", "dte");
    }
}

using Microsoft.EntityFrameworkCore;

namespace ReportesAPI.Datos;

public class PosDbContext : DbContext
{
    public PosDbContext(DbContextOptions<PosDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("public");
        modelBuilder.HasAnnotation("Relational:DbName", "pos");
    }
}

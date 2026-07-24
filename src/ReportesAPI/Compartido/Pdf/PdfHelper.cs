using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReportesAPI.Compartido.MultiTenant;

namespace ReportesAPI.Compartido.Pdf;

public static class PdfHelper
{
    public static async Task<string> GetTenantNombreAsync(DbContext db, Guid? tenantId)
    {
        if (tenantId is null) return "Mi Empresa";

        using var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT nombre FROM tenants WHERE id = @id";
        cmd.Parameters.Add(new NpgsqlParameter("@id", tenantId));
        var result = await cmd.ExecuteScalarAsync();
        return result?.ToString() ?? "Mi Empresa";
    }
}

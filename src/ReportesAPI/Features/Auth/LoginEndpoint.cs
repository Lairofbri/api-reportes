using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using ReportesAPI.Datos;

namespace ReportesAPI.Features.Auth;

public static class LoginEndpoint
{
    private static string _connString = string.Empty;

    public static void Map(WebApplication app, string posConnString)
    {
        _connString = posConnString;

        app.MapPost("/api/auth/login", async (HttpContext context, IConfiguration config, LoginRequest body) =>
        {
            var jwtSecret = config["JWT_SECRET"];

            if (string.IsNullOrEmpty(jwtSecret) || jwtSecret.Length < 64)
                return Results.Json(new { error = "JWT_SECRET no configurado correctamente." }, statusCode: 500);

            if (string.IsNullOrWhiteSpace(body.Email) || string.IsNullOrWhiteSpace(body.Password))
                return Results.Json(new { error = "Email y password requeridos." }, statusCode: 400);

            UsuarioRow? usuario;
            await using (var conn = new NpgsqlConnection(_connString))
            {
                await conn.OpenAsync();
                await using var cmd = new NpgsqlCommand("""
                    SELECT u.id, u.tenant_id, u.nombre, u.email, u.password_hash, u.rol,
                           u.activo, u.sucursal_id, u.bloqueado_hasta
                    FROM usuarios u
                    WHERE u.email = @email AND u.tenant_id = @tenantId
                    """, conn);
                cmd.Parameters.AddWithValue("@email", body.Email.ToLowerInvariant().Trim());
                cmd.Parameters.AddWithValue("@tenantId", NpgsqlTypes.NpgsqlDbType.Uuid, body.TenantId);

                await using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    return Results.Json(new { error = "Credenciales inválidas." }, statusCode: 401);

                usuario = new UsuarioRow
                {
                    Id = reader.GetGuid(0),
                    TenantId = reader.GetGuid(1),
                    Nombre = reader.GetString(2),
                    Email = reader.GetString(3),
                    PasswordHash = reader.IsDBNull(4) ? null : reader.GetString(4),
                    Rol = reader.GetString(5),
                    Activo = reader.GetBoolean(6),
                    SucursalId = reader.IsDBNull(7) ? null : reader.GetGuid(7),
                    BloqueadoHasta = reader.IsDBNull(8) ? null : reader.GetDateTime(8)
                };
            }

            if (!usuario.Activo)
                return Results.Json(new { error = "Usuario desactivado." }, statusCode: 401);

            if (usuario.BloqueadoHasta.HasValue && usuario.BloqueadoHasta.Value > DateTime.UtcNow)
                return Results.Json(new { error = "Usuario bloqueado temporalmente.", bloqueado_hasta = usuario.BloqueadoHasta }, statusCode: 423);

            if (string.IsNullOrEmpty(usuario.PasswordHash) || !BCrypt.Net.BCrypt.Verify(body.Password, usuario.PasswordHash))
            {
                await RegistrarIntentoFallido(usuario.Id, usuario.TenantId);
                return Results.Json(new { error = "Credenciales inválidas." }, statusCode: 401);
            }

            var accessToken = GenerarAccessToken(usuario, jwtSecret);
            var refreshToken = await CrearRefreshToken(usuario.Id, usuario.TenantId);

            context.Response.Cookies.Append("refresh_token", refreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = "/api/auth",
                MaxAge = TimeSpan.FromDays(7)
            });

            return Results.Ok(new
            {
                access_token = accessToken,
                usuario = new
                {
                    id = usuario.Id,
                    nombre = usuario.Nombre,
                    email = usuario.Email,
                    rol = usuario.Rol,
                    tenant_id = usuario.TenantId,
                    sucursal_id = usuario.SucursalId
                }
            });
        });
    }

    private static string GenerarAccessToken(UsuarioRow usuario, string jwtSecret)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var claims = new List<Claim>
        {
            new("sub", usuario.Id.ToString()),
            new("tenant_id", usuario.TenantId.ToString()),
            new("rol", usuario.Rol),
            new("email", usuario.Email)
        };

        if (usuario.SucursalId.HasValue)
            claims.Add(new("sucursal_id", usuario.SucursalId.Value.ToString()));

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static async Task<string> CrearRefreshToken(Guid usuarioId, Guid tenantId)
    {
        var tokenBytes = RandomNumberGenerator.GetBytes(64);
        var token = Convert.ToHexStringLower(tokenBytes);
        var hash = Convert.ToHexStringLower(SHA256.HashData(tokenBytes));

        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("""
            INSERT INTO refresh_tokens (usuario_id, token_hash, activo, expira_en, tenant_id)
            VALUES (@usuarioId, @hash, TRUE, @expiraEn, @tenantId)
            """, conn);
        cmd.Parameters.AddWithValue("@usuarioId", NpgsqlTypes.NpgsqlDbType.Uuid, usuarioId);
        cmd.Parameters.AddWithValue("@hash", hash);
        cmd.Parameters.AddWithValue("@expiraEn", DateTime.UtcNow.AddDays(7));
        cmd.Parameters.AddWithValue("@tenantId", NpgsqlTypes.NpgsqlDbType.Uuid, tenantId);
        await cmd.ExecuteNonQueryAsync();

        return token;
    }

    private static async Task RegistrarIntentoFallido(Guid usuarioId, Guid tenantId)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_connString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand("""
                UPDATE usuarios SET intentos_fallidos = COALESCE(intentos_fallidos, 0) + 1,
                    bloqueado_hasta = CASE
                        WHEN COALESCE(intentos_fallidos, 0) + 1 >= 5 THEN NOW() + INTERVAL '15 minutes'
                        ELSE bloqueado_hasta
                    END
                WHERE id = @id AND tenant_id = @tenantId
                """, conn);
            cmd.Parameters.AddWithValue("@id", NpgsqlTypes.NpgsqlDbType.Uuid, usuarioId);
            cmd.Parameters.AddWithValue("@tenantId", NpgsqlTypes.NpgsqlDbType.Uuid, tenantId);
            await cmd.ExecuteNonQueryAsync();
        }
        catch
        {
            // Non-critical, continue
        }
    }
}

public record LoginRequest(string Email, string Password, Guid TenantId);

internal class UsuarioRow
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public string Rol { get; set; } = string.Empty;
    public bool Activo { get; set; }
    public Guid? SucursalId { get; set; }
    public DateTime? BloqueadoHasta { get; set; }
}

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

namespace ReportesAPI.Features.Auth;

public static class RefreshEndpoint
{
    private static string _connString = string.Empty;

    public static void Map(WebApplication app, string posConnString)
    {
        _connString = posConnString;

        app.MapPost("/api/auth/refresh", async (HttpContext context, IConfiguration config) =>
        {
            var jwtSecret = config["JWT_SECRET"];

            if (string.IsNullOrEmpty(jwtSecret) || jwtSecret.Length < 64)
                return Results.Json(new { error = "JWT_SECRET no configurado correctamente." }, statusCode: 500);

            var refreshToken = context.Request.Cookies["refresh_token"];

            if (string.IsNullOrEmpty(refreshToken))
                return Results.Json(new { error = "Refresh token no proporcionado." }, statusCode: 401);

            var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));

            RefreshTokenRow? tokenRow;
            await using (var conn = new NpgsqlConnection(_connString))
            {
                await conn.OpenAsync();
                await using var cmd = new NpgsqlCommand("""
                    SELECT id, usuario_id, token_hash, activo, creado_en, expira_en, tenant_id
                    FROM refresh_tokens
                    WHERE token_hash = @hash
                    """, conn);
                cmd.Parameters.AddWithValue("@hash", hash);

                await using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    return Results.Json(new { error = "Refresh token inválido." }, statusCode: 401);

                tokenRow = new RefreshTokenRow
                {
                    Id = reader.GetGuid(0),
                    UsuarioId = reader.GetGuid(1),
                    TokenHash = reader.GetString(2),
                    Activo = reader.GetBoolean(3),
                    CreadoEn = reader.GetDateTime(4),
                    ExpiraEn = reader.GetDateTime(5),
                    TenantId = reader.GetGuid(6)
                };
            }

            if (!tokenRow.Activo)
            {
                await RevocarTodasLasSesiones(tokenRow.UsuarioId, tokenRow.TenantId);
                return Results.Json(new { error = "Sesión revocada por posible robo de token." }, statusCode: 401);
            }

            if (tokenRow.ExpiraEn < DateTime.UtcNow)
            {
                await DesactivarToken(tokenRow.Id);
                return Results.Json(new { error = "Refresh token expirado." }, statusCode: 401);
            }

            await DesactivarToken(tokenRow.Id);

            var usuario = await ObtenerUsuario(tokenRow.UsuarioId, tokenRow.TenantId);

            if (usuario is null)
                return Results.Json(new { error = "Usuario no encontrado." }, statusCode: 401);

            var accessToken = GenerarAccessToken(usuario, jwtSecret);
            var nuevoRefreshToken = await CrearRefreshToken(usuario.Id, usuario.TenantId);

            context.Response.Cookies.Append("refresh_token", nuevoRefreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = "/api/auth",
                MaxAge = TimeSpan.FromDays(7)
            });

            return Results.Ok(new { access_token = accessToken });
        });
    }

    private static string GenerarAccessToken(UsuarioRefreshRow usuario, string jwtSecret)
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

    private static async Task DesactivarToken(Guid tokenId)
    {
        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "UPDATE refresh_tokens SET activo = FALSE WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("@id", NpgsqlTypes.NpgsqlDbType.Uuid, tokenId);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task RevocarTodasLasSesiones(Guid usuarioId, Guid tenantId)
    {
        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "UPDATE refresh_tokens SET activo = FALSE WHERE usuario_id = @usuarioId AND tenant_id = @tenantId", conn);
        cmd.Parameters.AddWithValue("@usuarioId", NpgsqlTypes.NpgsqlDbType.Uuid, usuarioId);
        cmd.Parameters.AddWithValue("@tenantId", NpgsqlTypes.NpgsqlDbType.Uuid, tenantId);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<UsuarioRefreshRow?> ObtenerUsuario(Guid usuarioId, Guid tenantId)
    {
        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("""
            SELECT id, tenant_id, nombre, email, rol, sucursal_id
            FROM usuarios
            WHERE id = @id AND tenant_id = @tenantId AND activo = TRUE
            """, conn);
        cmd.Parameters.AddWithValue("@id", NpgsqlTypes.NpgsqlDbType.Uuid, usuarioId);
        cmd.Parameters.AddWithValue("@tenantId", NpgsqlTypes.NpgsqlDbType.Uuid, tenantId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        return new UsuarioRefreshRow
        {
            Id = reader.GetGuid(0),
            TenantId = reader.GetGuid(1),
            Nombre = reader.GetString(2),
            Email = reader.GetString(3),
            Rol = reader.GetString(4),
            SucursalId = reader.IsDBNull(5) ? null : reader.GetGuid(5)
        };
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
}

internal class RefreshTokenRow
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public bool Activo { get; set; }
    public DateTime CreadoEn { get; set; }
    public DateTime ExpiraEn { get; set; }
    public Guid TenantId { get; set; }
}

internal class UsuarioRefreshRow
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    public Guid? SucursalId { get; set; }
}

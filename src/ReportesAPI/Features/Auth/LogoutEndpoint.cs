using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace ReportesAPI.Features.Auth;

public static class LogoutEndpoint
{
    private static string _connString = string.Empty;

    public static void Map(WebApplication app, string posConnString)
    {
        _connString = posConnString;

        app.MapPost("/api/auth/logout", async (HttpContext context) =>
        {
            var refreshToken = context.Request.Cookies["refresh_token"];

            if (!string.IsNullOrEmpty(refreshToken))
            {
                var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));

                await using var conn = new NpgsqlConnection(_connString);
                await conn.OpenAsync();
                await using var cmd = new NpgsqlCommand(
                    "UPDATE refresh_tokens SET activo = FALSE WHERE token_hash = @hash", conn);
                cmd.Parameters.AddWithValue("@hash", hash);
                await cmd.ExecuteNonQueryAsync();
            }

            context.Response.Cookies.Append("refresh_token", "", new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = "/api/auth",
                Expires = DateTimeOffset.UnixEpoch
            });

            return Results.Ok(new { mensaje = "Sesión cerrada." });
        });
    }
}

namespace ReportesAPI.Compartido.Utilidades;

public static class DateTimeHelper
{
    public static DateTime ToUtc(DateTime dt) => DateTime.SpecifyKind(dt, DateTimeKind.Utc);

    public static DateOnly ToDateOnly(DateTime dt) => DateOnly.FromDateTime(dt);
}

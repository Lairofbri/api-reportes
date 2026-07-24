using PdfSharpCore;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;

namespace ReportesAPI.Compartido.Pdf;

public class PdfBuilder : IDisposable
{
    private readonly PdfDocument _doc;
    private const double Margin = 40;
    private const double PageWidth = 595;
    private const double PageHeight = 842;
    private const double UsableWidth = PageWidth - 2 * Margin;
    private double _y;
    private double _pageNumber;
    private PdfPage? _page;
    private XGraphics? _gfx;
    private string _empresa = "";
    private string _reporte = "";
    private string _periodo = "";
    private byte[]? _logoSrc;
    private readonly double _headerAccentHeight = 6;

    private static readonly XColor ColorAccent = XColor.FromArgb(245, 158, 11);
    private static readonly XColor ColorAccentDark = XColor.FromArgb(180, 115, 0);
    private static readonly XColor ColorText = XColor.FromArgb(26, 26, 26);
    private static readonly XColor ColorTextSecondary = XColor.FromArgb(102, 102, 102);
    private static readonly XColor ColorBorder = XColor.FromArgb(220, 220, 225);
    private static readonly XColor ColorZebra = XColor.FromArgb(248, 248, 250);
    private static readonly XColor ColorHeaderBg = ColorAccent;
    private static readonly XColor ColorTotalBg = XColor.FromArgb(245, 243, 235);

    private static readonly XFont FontEmpresa = new("DejaVu Sans", 16, XFontStyle.Bold);
    private static readonly XFont FontReporte = new("DejaVu Sans", 12, XFontStyle.Regular);
    private static readonly XFont FontPeriodo = new("DejaVu Sans", 8, XFontStyle.Regular);
    private static readonly XFont FontHeader = new("DejaVu Sans", 8, XFontStyle.Bold);
    private static readonly XFont FontRow = new("DejaVu Sans", 7.5, XFontStyle.Regular);
    private static readonly XFont FontTotal = new("DejaVu Sans", 8, XFontStyle.Bold);
    private static readonly XFont FontFooter = new("DejaVu Sans", 6.5, XFontStyle.Regular);
    private static readonly XFont FontEmpty = new("DejaVu Sans", 9, XFontStyle.Regular);

    public PdfBuilder()
    {
        _doc = new PdfDocument();
    }

    public PdfBuilder Empresa(string nombre)
    {
        _empresa = nombre;
        return this;
    }

    public PdfBuilder Reporte(string nombre)
    {
        _reporte = nombre;
        return this;
    }

    public PdfBuilder Periodo(string texto)
    {
        _periodo = texto;
        return this;
    }

    public PdfBuilder Logo(byte[] src)
    {
        _logoSrc = src;
        return this;
    }

    public PdfBuilder Titulo(string value)
    {
        _doc.Info.Title = value;
        return this;
    }

    public PdfBuilder Author(string value)
    {
        _doc.Info.Author = value;
        return this;
    }

    public byte[] Generar()
    {
        using var ms = new MemoryStream();
        _doc.Save(ms);
        return ms.ToArray();
    }

    public PdfBuilder Encabezado()
    {
        NuevaPagina();

        _y = Margin + 12;

        var penAccent = new XPen(ColorAccent, _headerAccentHeight);
        _gfx!.DrawLine(penAccent, Margin, _y, Margin + UsableWidth, _y);
        _y += _headerAccentHeight + 16;

        if (_logoSrc is not null)
        {
            try
            {
                using var ms = new MemoryStream(_logoSrc);
                var img = XImage.FromStream(() => ms);
                _gfx.DrawImage(img, Margin, _y - 4, 48, 48);
            }
            catch { }
        }

        var logoOffset = _logoSrc is not null ? 56 : 0;
        var textX = Margin + logoOffset;

        _gfx.DrawString(_empresa, FontEmpresa, new XSolidBrush(ColorText), textX, _y + 4);
        _y += FontEmpresa.GetHeight() + 6;

        if (!string.IsNullOrEmpty(_reporte))
        {
            _gfx.DrawString(_reporte, FontReporte, new XSolidBrush(ColorAccent), textX, _y);
            _y += FontReporte.GetHeight() + 4;
        }

        if (!string.IsNullOrEmpty(_periodo))
        {
            _gfx.DrawString(_periodo, FontPeriodo, new XSolidBrush(ColorTextSecondary), textX, _y);
            _y += FontPeriodo.GetHeight() + 12;
        }

        var linePen = new XPen(ColorBorder, 0.5);
        _gfx.DrawLine(linePen, Margin, _y, Margin + UsableWidth, _y);
        _y += 10;

        return this;
    }

    public PdfBuilder Tabla(string[] headers, IEnumerable<string[]> rows, string[]? totalRow = null)
    {
        var rowList = rows?.ToList() ?? [];

        if (rowList.Count == 0)
        {
            _gfx!.DrawString("Sin datos para el periodo seleccionado.", FontEmpty, new XSolidBrush(ColorTextSecondary), Margin, _y);
            _y += FontEmpty.GetHeight() + 12;
            return this;
        }

        var colCount = headers.Length;
        var colWidth = UsableWidth / colCount;
        var pen = new XPen(ColorBorder, 0.5);
        var headerH = FontHeader.GetHeight() + 8;
        var rowH = FontRow.GetHeight() + 5;
        var totalH = FontTotal.GetHeight() + 8;

        var tableTop = _y;
        var totalHeight = headerH + rowList.Count * rowH + (totalRow is not null ? totalH : 0);

        if (_y + totalHeight > PageHeight - Margin - 20)
        {
            PiePagina("");
            Encabezado();
            tableTop = _y;
        }

        var headerBrush = new XSolidBrush(ColorHeaderBg);
        for (int i = 0; i < colCount; i++)
        {
            var x = Margin + i * colWidth;
            _gfx!.DrawRectangle(headerBrush, x, _y, colWidth, headerH);
            _gfx.DrawString(headers[i], FontHeader, XBrushes.White, x + 5, _y + 6);
            _gfx.DrawRectangle(pen, x, _y, colWidth, headerH);
        }
        _y += headerH;

        for (int r = 0; r < rowList.Count; r++)
        {
            var values = rowList[r].ToArray();
            if (values.Length == 0) continue;

            if (_y + rowH > PageHeight - Margin - 20)
            {
                PiePagina("");
                Encabezado();
                for (int hi = 0; hi < colCount; hi++)
                {
                    var hx = Margin + hi * colWidth;
                    _gfx!.DrawRectangle(headerBrush, hx, _y, colWidth, headerH);
                    _gfx.DrawString(headers[hi], FontHeader, XBrushes.White, hx + 5, _y + 6);
                    _gfx.DrawRectangle(pen, hx, _y, colWidth, headerH);
                }
                _y += headerH;
            }

            var bg = (r % 2 == 0) ? XBrushes.Transparent : new XSolidBrush(ColorZebra);
            for (int i = 0; i < Math.Min(values.Length, colCount); i++)
            {
                var x = Margin + i * colWidth;
                _gfx!.DrawRectangle(bg, x, _y, colWidth, rowH);
                _gfx.DrawString(values[i], FontRow, new XSolidBrush(ColorText), x + 5, _y + 4);
                _gfx.DrawRectangle(pen, x, _y, colWidth, rowH);
            }
            _y += rowH;
        }

        if (totalRow is not null && totalRow.Length > 0)
        {
            var totalBg = new XSolidBrush(ColorTotalBg);
            var totalPen = new XPen(ColorAccentDark, 0.5);

            for (int i = 0; i < Math.Min(totalRow.Length, colCount); i++)
            {
                var x = Margin + i * colWidth;
                _gfx!.DrawRectangle(totalBg, x, _y, colWidth, totalH);
                _gfx.DrawString(totalRow[i], FontTotal, new XSolidBrush(ColorText), x + 5, _y + 6);
                _gfx.DrawRectangle(totalPen, x, _y, colWidth, totalH);
            }
            _y += totalH;
        }

        _y += 8;
        return this;
    }

    public PdfBuilder PiePagina(string texto)
    {
        var yPos = PageHeight - Margin - 6;

        var linePen = new XPen(ColorBorder, 0.5);
        _gfx!.DrawLine(linePen, Margin, yPos, Margin + UsableWidth, yPos);

        _pageNumber++;

        if (!string.IsNullOrEmpty(texto))
        {
            _gfx.DrawString(texto, FontFooter, new XSolidBrush(ColorTextSecondary), Margin, yPos + 10);
        }

        _gfx.DrawString($"Página {_pageNumber}", FontFooter, new XSolidBrush(ColorTextSecondary), Margin + UsableWidth - 50, yPos + 10);

        return this;
    }

    private void NuevaPagina()
    {
        _page = _doc.AddPage();
        _page.Size = PageSize.A4;
        _gfx = XGraphics.FromPdfPage(_page);
    }

    public void Dispose()
    {
        _doc?.Dispose();
    }
}

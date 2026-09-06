using System.Text.RegularExpressions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using GumpEditor.ClientData;
using GumpEditor.Models;

namespace GumpEditor.Rendering
{
    public sealed class GumpCanvas : Control
    {
        private GumpElement _selectedElement;

        private bool _dragging;

        private Point _dragStart;

        private int _elementStartX;

        private int _elementStartY;

        private double _zoom = 1.0;

        private readonly Dictionary<string, Bitmap> _gumpArtCache =
            new Dictionary<string, Bitmap>();

        public GumpDocument Document { get; set; }

        public GumpArtReader ArtReader { get; set; }

        public HueReader HueReader { get; set; }

        public UoFontReader UoFontReader { get; set; }

        public GumpElement SelectedElement
        {
            get
            {
                return _selectedElement;
            }
        }

        public double Zoom
        {
            get
            {
                return _zoom;
            }

            set
            {
                _zoom =
                    Math.Max(
                        0.25,
                        Math.Min(
                            4.0,
                            value));

                Invalidate();
            }
        }

        public bool ShowGrid { get; set; } = true;

        public int GridSize { get; set; } = 5;

        /// <summary>
        /// Visualização final do Gump.
        /// Quando ativa, remove elementos auxiliares
        /// do editor e mostra somente o resultado visual.
        /// </summary>
        public bool ClientPreviewMode { get; set; }

        /// <summary>
        /// Fundo externo utilizado pelo modo cliente.
        /// </summary>
        public Color ClientPreviewBackground { get; set; } =
            Color.FromArgb(45, 45, 45);

        public event EventHandler SelectedElementChanged;

        public GumpCanvas()
        {
            DoubleBuffered = true;

            BackColor =
                Color.FromArgb(
                    45,
                    45,
                    48);

            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer,
                true);
        }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        Graphics g = e.Graphics;

        g.Clear(BackColor);

        g.SmoothingMode = SmoothingMode.None;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.CompositingMode = CompositingMode.SourceOver;

        if (Document == null)
            return;

        const int ClientWidth = 800;
        const int ClientHeight = 600;

        const int ClientX = 20;
        const int ClientY = 20;

        float zoom = (float)_zoom;

        Rectangle clientRect = new Rectangle(
            ClientX,
            ClientY,
            (int)(ClientWidth * zoom),
            (int)(ClientHeight * zoom)
        );

        // ====================================================
        // FUNDO DA ÁREA DO CLIENTE
        // ====================================================

        using (Brush clientBrush =
            new SolidBrush(Color.FromArgb(25, 25, 28)))
        {
            g.FillRectangle(clientBrush, clientRect);
        }

        GraphicsState state = g.Save();

        g.TranslateTransform(ClientX, ClientY);
        g.ScaleTransform(zoom, zoom);

        // ====================================================
        // ÁREA REAL DO GUMP
        // ====================================================

        using (Brush gumpBrush =
            new SolidBrush(Color.FromArgb(18, 18, 20)))
        {
            gumpBrush.GetType();

            g.FillRectangle(
                gumpBrush,
                0,
                0,
                Document.Width,
                Document.Height
            );
        }

        if (ShowGrid)
            DrawGrid(g);

        // ====================================================
        // ELEMENTOS DO GUMP
        // ====================================================

        if (Document.Elements != null)
        {
            foreach (GumpElement element in Document.Elements)
            {
                if (element == null)
                    continue;

                if (element.Type == GumpElementType.Page)
                    continue;

                // Página 0 = elemento global.
                // Página atual = elemento específico.
                if (element.Page != 0 &&
                    element.Page != Document.CurrentPage)
                    continue;

                DrawElement(g, element);
            }
        }
        else
        {
            foreach (GumpElement element
                in Document.GetElementsForPage(Document.CurrentPage))
            {
                if (element == null)
                    continue;

                if (element.Type == GumpElementType.Page)
                    continue;

                DrawElement(g, element);
            }
        }

        g.Restore(state);

        // ====================================================
        // SELEÇÃO
        // ====================================================

        if (!ClientPreviewMode)
            DrawSelection(g);

        // ====================================================
        // BORDA DA TELA VIRTUAL
        // ====================================================

        using (Pen borderPen =
            new Pen(Color.FromArgb(90, 90, 95)))
        {
            g.DrawRectangle(
                borderPen,
                clientRect.X,
                clientRect.Y,
                clientRect.Width - 1,
                clientRect.Height - 1
            );
        }
    }




private void DrawGrid(Graphics g)
        {
            int size =
                Math.Max(
                    1,
                    GridSize);

            using (
                var pen =
                    new Pen(
                        Color.FromArgb(
                            55,
                            55,
                            60)))
            {
                for (
                    int x = 0;
                    x <= Document.Width;
                    x += size)
                {
                    g.DrawLine(
                        pen,
                        x,
                        0,
                        x,
                        Document.Height);
                }

                for (
                    int y = 0;
                    y <= Document.Height;
                    y += size)
                {
                    g.DrawLine(
                        pen,
                        0,
                        y,
                        Document.Width,
                        y);
                }
            }
        }

        private void DrawElement(
            Graphics g,
            GumpElement element)
        {
            switch (element.Type)
            {
                case GumpElementType.Background:
                    DrawBackground(
                        g,
                        element);
                    break;

                case GumpElementType.BookBackground:
                    DrawBookBackground(
                        g,
                        element);
                    break;

                case GumpElementType.Image:
                    DrawArt(
                        g,
                        element);
                    break;

                case GumpElementType.ImageTiled:
                    DrawTiled(
                        g,
                        element);
                    break;

                case GumpElementType.Button:
                    DrawArt(
                        g,
                        element);
                    break;

                case GumpElementType.Checkbox:
                    DrawControlArt(
                        g,
                        element,
                        "CHECKBOX");
                    break;

                case GumpElementType.Radio:
                    DrawControlArt(
                        g,
                        element,
                        "RADIO");
                    break;

                case GumpElementType.ImageTiledButton:
                    DrawControlArt(
                        g,
                        element,
                        "TILED BUTTON");
                    break;

                case GumpElementType.Label:
                case GumpElementType.LabelCropped:
                    DrawLabel(
                        g,
                        element);
                    break;

                case GumpElementType.Html:
                case GumpElementType.HtmlLocalized:
                case GumpElementType.TextEntry:
                    DrawText(
                        g,
                        element);
                    break;

                case GumpElementType.AlphaRegion:
                    DrawPlaceholder(
                        g,
                        element,
                        "ALPHA REGION");
                    break;

                case GumpElementType.Item:
                    DrawPlaceholder(
                        g,
                        element,
                        "ITEM " + element.ArtId);
                    break;

                case GumpElementType.ItemProperty:
                    DrawPlaceholder(
                        g,
                        element,
                        "ITEM PROPERTY");
                    break;

                case GumpElementType.Tooltip:
                    DrawPlaceholder(
                        g,
                        element,
                        "TOOLTIP");
                    break;

                case GumpElementType.Page:
                    break;

                default:

                    // Alguns scripts antigos/customizados possuem
                    // AddBookBackground sem um tipo dedicado no
                    // parser. Reconhecemos pelo texto original.

                    /*
 * addbookbackground é um helper do script original e não
 * é um tipo visual do cliente.
 *
 * O fundo real do livro chega ao parser como AddImage().
 *
 * Portanto não mostramos UNKNOWN no preview.
 */

                    break;
            }
        }

        private Bitmap GetArt(
            int id,
            int hue)
        {
            if (ArtReader == null ||
                id <= 0)
            {
                return null;
            }

            string key =
                id +
                ":" +
                hue;

            Bitmap cached;

            if (_gumpArtCache.TryGetValue(
                key,
                out cached))
            {
                return cached;
            }

            Bitmap original =
                ArtReader.GetGump(id);

            if (original == null)
                return null;

            Bitmap result =
                original;

            if (hue > 0 &&
                HueReader != null)
            {
                Bitmap hued =
                    HueReader.ApplyHue(
                        original,
                        hue);

                if (hued != null)
                    result = hued;
            }

            _gumpArtCache[key] =
                result;

            return result;
        }

        private void DrawArt(
            Graphics g,
            GumpElement element)
        {
            Bitmap bitmap =
                GetArt(
                    element.ArtId,
                    element.Hue);

            if (bitmap == null)
            {
                DrawPlaceholder(
                    g,
                    element,
                    "ART " + element.ArtId);

                return;
            }

            g.DrawImageUnscaled(
                bitmap,
                element.X,
                element.Y);
        }

        private void DrawControlArt(
            Graphics g,
            GumpElement element,
            string name)
        {
            Bitmap bitmap =
                GetArt(
                    element.ArtId,
                    element.Hue);

            if (bitmap != null)
            {
                g.DrawImageUnscaled(
                    bitmap,
                    element.X,
                    element.Y);
            }
            else
            {
                DrawPlaceholder(
                    g,
                    element,
                    name +
                    " " +
                    element.ArtId);
            }
        }
        private void DrawBackground(
            Graphics g,
            GumpElement element)
        {
            int x = element.X;
            int y = element.Y;

            int width = Math.Max(1, element.Width);
            int height = Math.Max(1, element.Height);

            /*
             * AddBackground é composto por 9 artes:
             *
             * 0 1 2
             * 3 4 5
             * 6 7 8
             *
             * Exemplo do livro:
             *
             * 9270 9271 9272
             * 9273 9274 9275
             * 9276 9277 9278
             */

            Bitmap[] parts = new Bitmap[9];

            for (int i = 0; i < 9; i++)
            {
                parts[i] = GetArt(
                    element.ArtId + i,
                    0);
            }

            Bitmap topLeft = parts[0];
            Bitmap top = parts[1];
            Bitmap topRight = parts[2];

            Bitmap left = parts[3];
            Bitmap center = parts[4];
            Bitmap right = parts[5];

            Bitmap bottomLeft = parts[6];
            Bitmap bottom = parts[7];
            Bitmap bottomRight = parts[8];

            /*
             * Obtém as dimensões diretamente das Bitmap.
             */

            int leftWidth = 0;

            if (topLeft != null)
                leftWidth = Math.Max(
                    leftWidth,
                    topLeft.Width);

            if (bottomLeft != null)
                leftWidth = Math.Max(
                    leftWidth,
                    bottomLeft.Width);

            int rightWidth = 0;

            if (topRight != null)
                rightWidth = Math.Max(
                    rightWidth,
                    topRight.Width);

            if (bottomRight != null)
                rightWidth = Math.Max(
                    rightWidth,
                    bottomRight.Width);

            int topHeight = 0;

            if (topLeft != null)
                topHeight = Math.Max(
                    topHeight,
                    topLeft.Height);

            if (topRight != null)
                topHeight = Math.Max(
                    topHeight,
                    topRight.Height);

            int bottomHeight = 0;

            if (bottomLeft != null)
                bottomHeight = Math.Max(
                    bottomHeight,
                    bottomLeft.Height);

            if (bottomRight != null)
                bottomHeight = Math.Max(
                    bottomHeight,
                    bottomRight.Height);

            /*
             * Cantos.
             */

            if (topLeft != null)
            {
                DrawPart(
                    g,
                    topLeft,
                    x,
                    y,
                    topLeft.Width,
                    topLeft.Height);
            }

            if (topRight != null)
            {
                DrawPart(
                    g,
                    topRight,
                    x + width - topRight.Width,
                    y,
                    topRight.Width,
                    topRight.Height);
            }

            if (bottomLeft != null)
            {
                DrawPart(
                    g,
                    bottomLeft,
                    x,
                    y + height - bottomLeft.Height,
                    bottomLeft.Width,
                    bottomLeft.Height);
            }

            if (bottomRight != null)
            {
                DrawPart(
                    g,
                    bottomRight,
                    x + width - bottomRight.Width,
                    y + height - bottomRight.Height,
                    bottomRight.Width,
                    bottomRight.Height);
            }

            int middleWidth =
                Math.Max(
                    0,
                    width -
                    leftWidth -
                    rightWidth);

            int middleHeight =
                Math.Max(
                    0,
                    height -
                    topHeight -
                    bottomHeight);

            /*
             * Parte superior.
             */

            if (top != null &&
                middleWidth > 0 &&
                topHeight > 0)
            {
                Tile(
                    g,
                    top,
                    x + leftWidth,
                    y,
                    middleWidth,
                    topHeight);
            }

            /*
             * Parte inferior.
             */

            if (bottom != null &&
                middleWidth > 0 &&
                bottomHeight > 0)
            {
                Tile(
                    g,
                    bottom,
                    x + leftWidth,
                    y + height - bottomHeight,
                    middleWidth,
                    bottomHeight);
            }

            /*
             * Lateral esquerdo.
             */

            if (left != null &&
                middleHeight > 0 &&
                leftWidth > 0)
            {
                Tile(
                    g,
                    left,
                    x,
                    y + topHeight,
                    leftWidth,
                    middleHeight);
            }

            /*
             * Lateral direito.
             */

            if (right != null &&
                middleHeight > 0 &&
                rightWidth > 0)
            {
                Tile(
                    g,
                    right,
                    x + width - rightWidth,
                    y + topHeight,
                    rightWidth,
                    middleHeight);
            }

            /*
             * Centro.
             */

            if (center != null &&
                middleWidth > 0 &&
                middleHeight > 0)
            {
                Tile(
                    g,
                    center,
                    x + leftWidth,
                    y + topHeight,
                    middleWidth,
                    middleHeight);
            }
        }
        private static void DrawPart(
            Graphics g,
            Bitmap bitmap,
            int x,
            int y,
            int width,
            int height)
        {
            if (bitmap == null ||
                width <= 0 ||
                height <= 0)
                return;

            int sourceWidth =
                Math.Min(
                    width,
                    bitmap.Width);

            int sourceHeight =
                Math.Min(
                    height,
                    bitmap.Height);

            g.DrawImage(
                bitmap,
                new Rectangle(
                    x,
                    y,
                    sourceWidth,
                    sourceHeight),
                new Rectangle(
                    0,
                    0,
                    sourceWidth,
                    sourceHeight),
                GraphicsUnit.Pixel);
        }

        private static void Tile(
            Graphics g,
            Bitmap bitmap,
            int x,
            int y,
            int width,
            int height)
        {
            if (bitmap == null ||
                width <= 0 ||
                height <= 0)
                return;

            for (
                int yy = y;
                yy < y + height;
                yy += bitmap.Height)
            {
                for (
                    int xx = x;
                    xx < x + width;
                    xx += bitmap.Width)
                {
                    int w =
                        Math.Min(
                            bitmap.Width,
                            x + width - xx);

                    int h =
                        Math.Min(
                            bitmap.Height,
                            y + height - yy);

                    if (w <= 0 || h <= 0)
                        continue;

                    g.DrawImage(
                        bitmap,
                        new Rectangle(
                            xx,
                            yy,
                            w,
                            h),
                        new Rectangle(
                            0,
                            0,
                            w,
                            h),
                        GraphicsUnit.Pixel);
                }
            }
        }

        /// <summary>
        /// Renderiza AddBookBackground como uma peça de fundo
        /// permanente do Gump.
        ///
        /// O background de livro não deve desaparecer quando
        /// mudamos de página.
        /// </summary>
        private void DrawBookBackground(
            Graphics g,
            GumpElement element)
        {
            int artId = element.ArtId;

            if (artId <= 0)
            {
                DrawPlaceholder(
                    g,
                    element,
                    "BOOK BACKGROUND");

                return;
            }

            Bitmap bitmap =
                GetArt(
                    artId,
                    element.Hue);

            if (bitmap == null)
            {
                DrawPlaceholder(
                    g,
                    element,
                    "BOOK " + artId);

                return;
            }

            int width =
                element.Width > 0
                    ? element.Width
                    : bitmap.Width;

            int height =
                element.Height > 0
                    ? element.Height
                    : bitmap.Height;

            g.DrawImage(
                bitmap,
                new Rectangle(
                    element.X,
                    element.Y,
                    width,
                    height),
                new Rectangle(
                    0,
                    0,
                    bitmap.Width,
                    bitmap.Height),
                GraphicsUnit.Pixel);
        }
        private void DrawTiled(
            Graphics g,
            GumpElement element)
        {
            Bitmap bitmap =
                GetArt(
                    element.ArtId,
                    element.Hue);

            if (bitmap == null)
            {
                DrawPlaceholder(
                    g,
                    element,
                    "TILED " +
                    element.ArtId);

                return;
            }

            Tile(
                g,
                bitmap,
                element.X,
                element.Y,
                Math.Max(
                    1,
                    element.Width),
                Math.Max(
                    1,
                    element.Height));
        }

        private void DrawLabel(
    Graphics g,
    GumpElement element)
{
    Color color =
        GetTextColor(
            element.Hue);

    /*
     * Preferimos a fonte real do Ultima Online.
     */

    if (UoFontReader != null)
    {
        try
        {
            int fontId = element.Font;

            if (element.Parameters != null &&
                element.Parameters.Count > 2)
            {
                int.TryParse(
                    element.Parameters[2],
                    out fontId);
            }

            if (fontId >= 0 &&
                fontId < UoFontReader.Fonts.Count)
            {
                Bitmap bitmap =
                    UoFontReader.RenderText(
                        fontId,
                        element.Text ?? "",
                        color,
                        1.0f);

                if (bitmap != null)
                {
                    g.DrawImageUnscaled(
                        bitmap,
                        element.X,
                        element.Y);

                    bitmap.Dispose();

                    return;
                }
            }
        }
        catch
        {
            /*
             * Se a fonte UO nao puder ser renderizada,
             * usamos o fallback abaixo.
             */
        }
    }

    /*
     * O texto do Gump deve usar exclusivamente a fonte grafica
     * do Ultima Online.
     *
     * Nao usamos Graphics.DrawString() aqui porque isso utiliza
     * uma fonte instalada no Windows e altera a aparencia original
     * do cliente.
     */
    return;
}
        private void DrawText(
            Graphics g,
            GumpElement element)
        {
        // Texto vazio nao deve gerar <font></font> nem bitmap.
        if (element == null || string.IsNullOrEmpty(element.Text))
            return;

            Rectangle bounds =
                element.Bounds;

            Color color =
                GetTextColor(
                    element.Hue);

            /*
             * ========================================================
             * RENDERIZAÇÃO DE TEXTO DO ULTIMA ONLINE
             * ========================================================
             *
             * Não usamos System.Drawing.Font para texto do Gump.
             *
             * O cliente do Ultima Online utiliza os arquivos de fonte
             * do próprio cliente. Portanto, quando UoFontReader está
             * disponível, o texto precisa passar pelo RenderText().
             *
             * Escala 1.0:
             *
             * O tamanho real da fonte é preservado.
             * O zoom do editor é aplicado posteriormente pelo Canvas.
             */

            if (UoFontReader == null)
            {
                /*
                 * Não usamos Graphics.DrawString() aqui.
                 *
                 * O fallback Windows altera completamente a aparência
                 * do texto do UO e foi uma das causas do texto ficar
                 * grande, grosso e desfocado.
                 */
                return;
            }

            string sourceText =
                element.Text ?? string.Empty;

            /*
             * ========================================================
             * TRATAMENTO BÁSICO DO HTML
             * ========================================================
             *
             * AddHtml do UO pode conter comandos de formatação.
             *
             * Nesta primeira etapa não tentamos reproduzir um navegador.
             * Apenas transformamos as tags em algo que o renderer UO
             * consegue desenhar corretamente.
             */

            sourceText =
                System.Net.WebUtility.HtmlDecode(
                    sourceText);

            /*
             * <BR> representa quebra de linha.
             */
            sourceText =
                System.Text.RegularExpressions.Regex.Replace(
                    sourceText,
                    @"<\s*br\s*/?\s*>",
                    "\n",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            /*
             * Remove tags de formatação que não devem aparecer
             * literalmente na tela.
             *
             * A fonte real continuará sendo a fonte UO.
             */
            sourceText =
                System.Text.RegularExpressions.Regex.Replace(
                    sourceText,
                    @"</?\s*(center|left|right|justify|b|strong|i|em|u|big|small|basefont|font|p|div|span)\b[^>]*>",
                    string.Empty,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            /*
             * Remove qualquer outra tag HTML restante.
             */
            sourceText =
                System.Text.RegularExpressions.Regex.Replace(
                    sourceText,
                    @"<[^>]+>",
                    string.Empty);

            /*
             * Normaliza quebras de linha.
             */
            sourceText =
                sourceText.Replace(
                    "\r\n",
                    "\n");

            sourceText =
                sourceText.Replace(
                    "\r",
                    "\n");

            /*
             * Evita espaços artificiais causados pelo HTML.
             */
            sourceText =
                sourceText.Trim();

            if (sourceText.Length == 0)
            {
                if (!ClientPreviewMode)
                {
                    using (
                        var pen =
                            new Pen(
                                Color.FromArgb(
                                    130,
                                    160,
                                    160,
                                    160)))
                    {
                        g.DrawRectangle(
                            pen,
                            bounds);
                    }
                }

                return;
            }

            /*
             * ========================================================
             * FONTE
             * ========================================================
             *
             * AddHtml normalmente não carrega um fontId da mesma
             * maneira que AddLabel.
             *
             * Para HTML utilizamos Font 0 como fonte padrão do cliente.
             *
             * AddLabel continua usando a lógica própria já existente.
             */

            int fontId = element.Font;

            if (fontId < 0 ||
                fontId >= UoFontReader.Fonts.Count)
            {
                fontId = 0;}
            /*
             * TextSize controla a escala visual dos glyphs
             * reais do Ultima Online.
             *
             * 12 = 100%
             * 18 = 150%
             * 24 = 200%
             */
            float scale =
                GetUoTextScale(
                    element);

            /*
             * ========================================================
             * RENDERIZAÇÃO DAS LINHAS
             * ========================================================
             *
             * RenderText() gera exatamente os pixels da fonte UO.
             *
             * Não usamos DrawString().
             * Não redimensionamos o bitmap.
             */

            string[] lines =
                sourceText.Split(
                    new[] { '\n' });

            int originalLineHeight =
                UoFontReader.Fonts[fontId].Height;

            if (originalLineHeight <= 0)
            {
                originalLineHeight = 16;
            }

            int lineHeight =
                Math.Max(
                    1,
                    (int)Math.Ceiling(
                        originalLineHeight *
                        scale));

            int currentY =
                bounds.Y;

            for (int i = 0;
                 i < lines.Length;
                 i++)
            {
                string line =
                    lines[i];

                if (line == null)
                {
                    line = string.Empty;
                }

                /*
                 * Não desenhamos linhas vazias, mas avançamos
                 * verticalmente como o cliente faria.
                 */
                if (line.Length > 0)
                {
                    Bitmap bitmap = null;

                    try
                    {
                        bitmap =
                            UoFontReader.RenderText(
                                fontId,
                                line,
                                color,
                                scale);

                        if (bitmap != null)
                        {
                            int drawX =
                                bounds.X;

                            /*
                             * Centralização horizontal quando o
                             * elemento possui largura definida.
                             *
                             * O tamanho da fonte não é alterado.
                             */
                            if (bitmap.Width < bounds.Width)
                            {
                                if (element.TextAlign == 1)
                                {
                                    drawX =
                                        bounds.X +
                                        ((bounds.Width -
                                          bitmap.Width) / 2);
                                }
                                else if (element.TextAlign == 2)
                                {
                                    drawX =
                                        bounds.Right -
                                        bitmap.Width;
                                }
                            }

                            /*
                             * Não deixar o texto ultrapassar a área
                             * vertical do AddHtml.
                             */
                            if (currentY < bounds.Bottom &&
                                currentY + bitmap.Height <= bounds.Bottom)
                            {
                                g.DrawImageUnscaled(
                                    bitmap,
                                    drawX,
                                    currentY);
                            }
                        }
                    }
                    finally
                    {
                        if (bitmap != null)
                        {
                            bitmap.Dispose();
                        }
                    }
                }

                currentY += lineHeight;

                if (currentY >= bounds.Bottom)
                {
                    break;
                }
            }

            /*
             * O retângulo abaixo é somente uma ferramenta do editor.
             * Ele desaparece no ClientPreviewMode.
             */
            if (!ClientPreviewMode)
            {
                using (
                    var pen =
                        new Pen(
                            Color.FromArgb(
                                130,
                                160,
                                160,
                                160)))
                {
                    g.DrawRectangle(
                        pen,
                        bounds);
                }
            }
        }
private enum UoHtmlAlignment
{
    Left,
    Center,
    Right
}

private sealed class UoHtmlLine
{
    public string Text { get; set; }

    public int FontId { get; set; }

    public Color Color { get; set; }

    public UoHtmlAlignment Alignment { get; set; }
}

private int GetUoFontId(
    GumpElement element)
{
    /*
     * Primeiro tentamos encontrar uma informação explícita
     * de fonte nos parâmetros.
     *
     * Não interpretamos X/Y/Largura/Altura como fonte.
     */

    if (element != null &&
        element.Parameters != null)
    {
        foreach (string parameter in element.Parameters)
        {
            if (string.IsNullOrWhiteSpace(parameter))
            {
                continue;
            }

            string value =
                parameter.Trim();

            int parsed;

            if (int.TryParse(value, out parsed) &&
                parsed >= 0 &&
                parsed < 10)
            {
                /*
                 * Só utilizamos parâmetros isolados que representem
                 * uma fonte UO válida.
                 */
                if (element.Type ==
                    GumpElementType.Label)
                {
                    /*
                     * Label normalmente não possui fontId explícito
                     * no comando. Mantemos Font 0.
                     */
                    continue;
                }
            }
        }
    }

    return 0;
}

private List<UoHtmlLine> ParseUoHtml(
    string html,
    Color defaultColor,
    int defaultFont)
{
    var result =
        new List<UoHtmlLine>();

    if (string.IsNullOrWhiteSpace(html))
    {
        return result;
    }

    string text = html;

    /*
     * Normalização de quebras de linha.
     */

    text = Regex.Replace(
        text,
        @"<\s*br\s*/?\s*>",
        "\n",
        RegexOptions.IgnoreCase);

    text = Regex.Replace(
        text,
        @"<\s*/\s*p\s*>",
        "\n",
        RegexOptions.IgnoreCase);

    text = Regex.Replace(
        text,
        @"<\s*p(?:\s+[^>]*)?>",
        "",
        RegexOptions.IgnoreCase);

    /*
     * Divide o HTML em linhas antes de retirar as tags.
     */

    string[] rawLines =
        text.Split(
            new[] { '\n' },
            StringSplitOptions.None);

    foreach (string rawLine in rawLines)
    {
        string lineHtml =
            rawLine ?? string.Empty;

        int fontId =
            defaultFont;

        if (fontId < 0 ||
            UoFontReader == null ||
            fontId >= UoFontReader.Fonts.Count)
        {
            fontId = 0;
        }

        Color color =
            defaultColor;

        UoHtmlAlignment alignment =
            UoHtmlAlignment.Left;

        /*
         * face="N"
         */

        Match faceMatch =
            Regex.Match(
                lineHtml,
                @"face\s*=\s*[""']?(\d+)",
                RegexOptions.IgnoreCase);

        if (faceMatch.Success)
        {
            int parsed;

            if (int.TryParse(
                faceMatch.Groups[1].Value,
                out parsed))
            {
                if (UoFontReader != null &&
                    parsed >= 0 &&
                    parsed < UoFontReader.Fonts.Count)
                {
                    fontId = parsed;
                }
            }
        }

        /*
         * color="#FFFFFF"
         */

        Match colorMatch =
            Regex.Match(
                lineHtml,
                @"color\s*=\s*[""']?#?([0-9A-Fa-f]{6})",
                RegexOptions.IgnoreCase);

        if (colorMatch.Success)
        {
            try
            {
                int rgb =
                    Convert.ToInt32(
                        colorMatch.Groups[1].Value,
                        16);

                color =
                    Color.FromArgb(
                        255,
                        (rgb >> 16) & 0xFF,
                        (rgb >> 8) & 0xFF,
                        rgb & 0xFF);
            }
            catch
            {
                color = defaultColor;
            }
        }

        /*
         * Alinhamento.
         */

        if (Regex.IsMatch(
            lineHtml,
            @"<\s*center\b",
            RegexOptions.IgnoreCase))
        {
            alignment =
                UoHtmlAlignment.Center;
        }
        else if (Regex.IsMatch(
            lineHtml,
            @"<\s*right\b",
            RegexOptions.IgnoreCase))
        {
            alignment =
                UoHtmlAlignment.Right;
        }

        /*
         * Remove tags HTML sem destruir o conteúdo.
         */

        string plain =
            Regex.Replace(
                lineHtml,
                @"<[^>]+>",
                "");

        plain =
            System.Net.WebUtility.HtmlDecode(
                plain);

        /*
         * Remove caracteres de controle e espaços artificiais.
         */

        plain =
            plain.Replace(
                "\r",
                "");

        /*
         * Uma tag  vazia não gera linha.
         */

        if (string.IsNullOrWhiteSpace(plain))
        {
            continue;
        }

        result.Add(
            new UoHtmlLine
            {
                Text = plain,
                FontId = fontId,
                Color = color,
                Alignment = alignment
            });
    }

    return result;
}

private void DrawEditorBounds(
    Graphics g,
    Rectangle bounds)
{
    using (
        var pen =
            new Pen(
                Color.FromArgb(
                    130,
                    160,
                    160,
                    160)))
    {
        g.DrawRectangle(
            pen,
            bounds);
    }
}

private Color GetTextColor(
            int hue)
        {
            if (hue <= 0 ||
                HueReader == null)
                return Color.White;

            HueData data =
                HueReader.GetHue(
                    hue);

            if (data == null)
                return Color.White;

            return data.Colors[31];
        }

        private void DrawPlaceholder(
            Graphics g,
            GumpElement element,
            string text)
        {
            Rectangle bounds =
                element.Bounds;

            using (
                var brush =
                    new SolidBrush(
                        Color.FromArgb(
                            70,
                            100,
                            100,
                            100)))
            using (
                var pen =
                    new Pen(
                        Color.FromArgb(
                            210,
                            230,
                            230,
                            230)))
            using (
                var textBrush =
                    new SolidBrush(
                        Color.White))
            {
                g.FillRectangle(
                    brush,
                    bounds);

                g.DrawRectangle(
                    pen,
                    bounds);

                g.DrawString(
                    text,
                    Font,
                    textBrush,
                    bounds.X + 3,
                    bounds.Y + 3);
            }
        }

        private void DrawSelection(
            Graphics g)
        {
            if (_selectedElement == null)
                return;

            Rectangle bounds =
                GetElementBounds(
                    _selectedElement);

            const int ClientX = 20;
            const int ClientY = 20;

            float zoom =
                (float)_zoom;

            /*
             * Os elementos do Gump são desenhados dentro da
             * mesma transformação utilizada pelo OnPaint():
             *
             *     Translate(ClientX, ClientY)
             *     Scale(Zoom)
             *
             * A seleção precisa utilizar exatamente a mesma
             * transformação para ficar sobre o objeto.
             */

            GraphicsState state =
                g.Save();

            try
            {
                g.TranslateTransform(
                    ClientX,
                    ClientY);

                g.ScaleTransform(
                    zoom,
                    zoom);

                /*
                 * Compensamos a espessura da linha pelo zoom.
                 *
                 * Assim a seleção continua visualmente com
                 * aproximadamente 2 pixels na tela.
                 */

                using (
                    var pen =
                        new Pen(
                            Color.Lime,
                            2.0f / zoom))
                {
                    pen.DashStyle =
                        DashStyle.Dash;

                    g.DrawRectangle(
                        pen,
                        bounds);
                }
            }
            finally
            {
                g.Restore(state);
            }
        }

        protected override void OnMouseDown(
            MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (Document == null ||
                e.Button != MouseButtons.Left)
                return;

            Point point =
                ScreenToDocument(
                    e.Location);

            GumpElement element =
                FindElementAt(
                    point);

            _selectedElement =
                element;

            SelectedElementChanged?.Invoke(
                this,
                EventArgs.Empty);

            if (element != null)
            {
                _dragging = true;

                _dragStart =
                    point;

                _elementStartX =
                    element.X;

                _elementStartY =
                    element.Y;

                Cursor =
                    Cursors.SizeAll;
            }
            else
            {
                _dragging = false;

                Cursor =
                    Cursors.Default;
            }

            Invalidate();
        }

        protected override void OnMouseMove(
            MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (!_dragging ||
                _selectedElement == null)
                return;

            Point point =
                ScreenToDocument(
                    e.Location);

            int dx =
                point.X -
                _dragStart.X;

            int dy =
                point.Y -
                _dragStart.Y;

            int x =
                _elementStartX +
                dx;

            int y =
                _elementStartY +
                dy;

            int grid =
                Math.Max(
                    1,
                    GridSize);

            x =
                (int)Math.Round(
                    x /
                    (double)grid) *
                grid;

            y =
                (int)Math.Round(
                    y /
                    (double)grid) *
                grid;

            _selectedElement.X =
                x;

            _selectedElement.Y =
                y;

            if (Document != null)
                Document.IsModified = true;

            SelectedElementChanged?.Invoke(
                this,
                EventArgs.Empty);

            Invalidate();
        }

        protected override void OnMouseUp(
            MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (e.Button ==
                MouseButtons.Left)
            {
                _dragging = false;

                Cursor =
                    Cursors.Default;
            }
        }

    private Point ScreenToDocument(Point point)
    {
        const int ClientX = 20;
        const int ClientY = 20;

        return new Point(
            (int)((point.X - ClientX) / _zoom),
            (int)((point.Y - ClientY) / _zoom)
        );
    }



    private GumpElement FindElementAt(Point point)
    {
        if (Document == null)
            return null;

        var elements = new List<GumpElement>();

        if (Document.Elements != null)
        {
            foreach (GumpElement element in Document.Elements)
            {
                if (element == null)
                    continue;

                if (element.Type == GumpElementType.Page)
                    continue;

                // Página 0 é global.
                // A página atual também deve ser considerada.
                if (element.Page != 0 &&
                    element.Page != Document.CurrentPage)
                    continue;

                elements.Add(element);
            }
        }
        else
        {
            foreach (GumpElement element
                in Document.GetElementsForPage(Document.CurrentPage))
            {
                if (element == null)
                    continue;

                if (element.Type == GumpElementType.Page)
                    continue;

                elements.Add(element);
            }
        }

        // Percorre de trás para frente para respeitar layering.
        for (int i = elements.Count - 1; i >= 0; i--)
        {
            GumpElement element = elements[i];

            Rectangle bounds = GetElementBounds(element);

            if (bounds.Contains(point))
                return element;
        }

        return null;
    }

        private Rectangle GetElementBounds(
            GumpElement element)
        {
            if (element == null)
                return Rectangle.Empty;

            int width =
                element.Width;

            int height =
                element.Height;

            if (
                (element.Type ==
                    GumpElementType.Image ||
                 element.Type ==
                    GumpElementType.Button ||
                 element.Type ==
                    GumpElementType.Checkbox ||
                 element.Type ==
                    GumpElementType.Radio ||
                 element.Type ==
                    GumpElementType.ImageTiledButton ||
                 element.Type ==
                    GumpElementType.Item)
                &&
                ArtReader != null &&
                element.ArtId > 0)
            {
                Bitmap bitmap =
                    GetArt(
                        element.ArtId,
                        element.Hue);

                if (bitmap != null)
                {
                    width =
                        Math.Max(
                            width,
                            bitmap.Width);

                    height =
                        Math.Max(
                            height,
                            bitmap.Height);
                }
            }

            if (width <= 0)
                width = 80;

            if (height <= 0)
                height = 20;

            return new Rectangle(
                element.X,
                element.Y,
                width,
                height);
        }

        public void SelectElement(
            GumpElement element)
        {
            _selectedElement =
                element;

            SelectedElementChanged?.Invoke(
                this,
                EventArgs.Empty);

            Invalidate();
        }

        public void ClearSelection()
        {
            _selectedElement = null;

            _dragging = false;

            Cursor =
                Cursors.Default;

            SelectedElementChanged?.Invoke(
                this,
                EventArgs.Empty);

            Invalidate();
        }

        public void ClearArtCache()
        {
            foreach (
                Bitmap bitmap
                in _gumpArtCache.Values)
            {
                if (bitmap != null)
                    bitmap.Dispose();
            }

            _gumpArtCache.Clear();

            Invalidate();
        }

        public void RefreshCanvas()
        {
            Invalidate();
        }
        /// <summary>
        /// Calcula a escala visual usando os glyphs bitmap
        /// reais do Ultima Online.
        ///
        /// 12 = 100%
        /// 18 = 150%
        /// 24 = 200%
        /// </summary>
        private static float GetUoTextScale(
            GumpElement element)
        {
            if (element == null ||
                element.TextSize <= 0)
            {
                return 1.0f;
            }

            float scale =
                element.TextSize /
                12.0f;

            if (scale < 0.25f)
                scale = 0.25f;

            if (scale > 4.0f)
                scale = 4.0f;

            return scale;
        }
    }
}










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

        private readonly List<GumpElement> _selectedElements =
            new List<GumpElement>();

        private readonly Dictionary<GumpElement, Point> _dragStartPositions =
            new Dictionary<GumpElement, Point>();
        private bool _dragging;

        private Point _dragStart;



        private double _zoom = 1.0;

        private int _viewportWidth = 800;
        private int _viewportHeight = 600;

        public int ViewportWidth
        {
            get
            {
                return _viewportWidth;
            }
            set
            {
                _viewportWidth =
                    Math.Max(1, value);

                Invalidate();
            }
        }

        public int ViewportHeight
        {
            get
            {
                return _viewportHeight;
            }
            set
            {
                _viewportHeight =
                    Math.Max(1, value);

                Invalidate();
            }
        }

        public bool TestMode { get; set; }

        // ============================================================
        // CLIPBOARD INTERNO DO EDITOR
        // ============================================================
        //
        // NÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â£o usamos Clipboard do Windows para armazenar o objeto.
        // Mantemos uma cÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â³pia completa do GumpElement.
        //
        // Isso permite copiar qualquer elemento do Gump sem perder
        // propriedades especÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â­ficas.
        // ============================================================

        private GumpElement _clipboardElement;
        private readonly Dictionary<string, Bitmap> _gumpArtCache =
            new Dictionary<string, Bitmap>();

        public GumpDocument Document { get; set; }

        public GumpArtReader ArtReader { get; set; }

        public HueReader HueReader { get; set; }

        public UoFontReader UoFontReader { get; set; }        public GumpElement SelectedElement
        {
            get
            {
                return _selectedElement;
            }
        }

        public IReadOnlyList<GumpElement> SelectedElements
        {
            get
            {
                return _selectedElements.AsReadOnly();
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
        /// VisualizaÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â§ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â£o final do Gump.
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

        g.SmoothingMode =
            SmoothingMode.None;

        g.InterpolationMode =
            InterpolationMode.NearestNeighbor;

        g.PixelOffsetMode =
            PixelOffsetMode.Half;

        g.CompositingMode =
            CompositingMode.SourceOver;

        if (Document == null)
            return;

        int ClientWidth =
            Math.Max(1, _viewportWidth);

        int ClientHeight =
            Math.Max(1, _viewportHeight);

        const int ClientX = 20;
        const int ClientY = 20;

        float zoom =
            (float)_zoom;

        Rectangle clientRect =
            new Rectangle(
                ClientX,
                ClientY,
                (int)(ClientWidth * zoom),
                (int)(ClientHeight * zoom));

        using (Brush clientBrush =
               new SolidBrush(
                   Color.FromArgb(
                       25,
                       25,
                       28)))
        {
            g.FillRectangle(
                clientBrush,
                clientRect);
        }

        GraphicsState state =
            g.Save();

        g.TranslateTransform(
            ClientX,
            ClientY);

        g.ScaleTransform(
            zoom,
            zoom);

        // ====================================================
        // AREA REAL DO GUMP
        // ====================================================
        using (Brush gumpBrush =
               new SolidBrush(
                   Color.FromArgb(
                       18,
                       18,
                       20)))
        {
            g.FillRectangle(
                gumpBrush,
                0,
                0,
                Document.Width,
                Document.Height);
        }

        if (ShowGrid)
            DrawGrid(g);

        if (Document.Elements != null)
        {
            foreach (GumpElement element
                     in Document.Elements)
            {
                if (element == null)
                    continue;

                if (element.Type ==
                    GumpElementType.Page)
                {
                    continue;
                }

                if (element.Page != 0 &&
                    element.Page !=
                    Document.CurrentPage)
                {
                    continue;
                }

                DrawElement(
                    g,
                    element);
            }
        }
        else
        {
            foreach (GumpElement element
                     in Document.GetElementsForPage(
                         Document.CurrentPage))
            {
                if (element == null)
                    continue;

                if (element.Type ==
                    GumpElementType.Page)
                {
                    continue;
                }

                DrawElement(
                    g,
                    element);
            }
        }

        g.Restore(state);

        if (!ClientPreviewMode)
            DrawSelection(g);

        using (Pen borderPen =
               new Pen(
                   Color.FromArgb(
                       90,
                       90,
                       95)))
        {
            g.DrawRectangle(
                borderPen,
                clientRect.X,
                clientRect.Y,
                clientRect.Width - 1,
                clientRect.Height - 1);
        }
    }




        private void DrawGrid(Graphics g)
        {
            int size =
                Math.Max(
                    1,
                    GridSize);

            int width =
                Math.Max(
                    Document != null
                        ? Document.Width
                        : 0,
                    _viewportWidth);

            int height =
                Math.Max(
                    Document != null
                        ? Document.Height
                        : 0,
                    _viewportHeight);

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
                    x <= width;
                    x += size)
                {
                    g.DrawLine(
                        pen,
                        x,
                        0,
                        x,
                        height);
                }

                for (
                    int y = 0;
                    y <= height;
                    y += size)
                {
                    g.DrawLine(
                        pen,
                        0,
                        y,
                        width,
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
 * addbookbackground ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â© um helper do script original e nÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â£o
 * ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â© um tipo visual do cliente.
 *
 * O fundo real do livro chega ao parser como AddImage().
 *
 * Portanto nÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â£o mostramos UNKNOWN no preview.
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
             * AddBackground ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â© composto por 9 artes:
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
             * ObtÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â©m as dimensÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Âµes diretamente das Bitmap.
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
        /// Renderiza AddBookBackground como uma peÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â§a de fundo
        /// permanente do Gump.
        ///
        /// O background de livro nÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â£o deve desaparecer quando
        /// mudamos de pÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡gina.
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
    /*
     * ============================================================
     * FONTE REAL DO ULTIMA ONLINE
     * ============================================================
     *
     * Font = -1 significa:
     *
     *     PADRAO DO CLIENTE
     *
     * No cliente moderno do UO / ClassicUO, quando a fonte
     * nao e especificada (0xFF), o cliente utiliza Font 1.
     *
     * Portanto:
     *
     *     element.Font == -1
     *             |
     *             v
     *         Font 1
     *
     * Font >= 0 continua sendo uma fonte explicitamente escolhida.
     * ============================================================
     */

    Color color =
        GetTextColor(
            element.Hue);

    if (UoFontReader == null)
    {
        return;
    }

    try
    {
        int fontId =
            element.Font;

        /*
         * Font -1 = PADRAO DO CLIENTE.
         *
         * ClassicUO moderno resolve 0xFF para Font 1.
         */
        if (fontId < 0)
        {
            fontId = 1;
        }

        /*
         * Protecao caso o arquivo de fontes possua menos fontes.
         */
        if (fontId >= UoFontReader.Fonts.Count)
        {
            fontId = 1;

            if (fontId >= UoFontReader.Fonts.Count)
            {
                fontId = 0;
            }
        }

        string text =
            element.Text ?? string.Empty;

        Bitmap bitmap =
            UoFontReader.RenderText(
                fontId,
                text,
                color,
                1.0f);

        if (bitmap == null)
        {
            return;
        }

        /*
         * AddLabel trabalha diretamente com X/Y.
         */
        g.DrawImageUnscaled(
            bitmap,
            element.X,
            element.Y);

        bitmap.Dispose();
    }
    catch
    {
        /*
         * Nunca deixar uma falha de fonte interromper
         * a renderizacao completa do Gump.
         */
    }
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
             * RENDERIZAÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢O DE TEXTO DO ULTIMA ONLINE
             * ========================================================
             *
             * NÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â£o usamos System.Drawing.Font para texto do Gump.
             *
             * O cliente do Ultima Online utiliza os arquivos de fonte
             * do prÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â³prio cliente. Portanto, quando UoFontReader estÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡
             * disponÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â­vel, o texto precisa passar pelo RenderText().
             *
             * Escala 1.0:
             *
             * O tamanho real da fonte ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â© preservado.
             * O zoom do editor ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â© aplicado posteriormente pelo Canvas.
             */

            if (UoFontReader == null)
            {
                /*
                 * NÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â£o usamos Graphics.DrawString() aqui.
                 *
                 * O fallback Windows altera completamente a aparÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Âªncia
                 * do texto do UO e foi uma das causas do texto ficar
                 * grande, grosso e desfocado.
                 */
                return;
            }

            string sourceText =
                element.Text ?? string.Empty;
            // Fonte padrão para AddHtml.
            // Sem face="N", utiliza Font 0.

            int htmlFontId = 1;

            Match htmlFaceMatch =
                Regex.Match(
                    sourceText,
                    @"face\s*=\s*[""']?(\d+)",
                    RegexOptions.IgnoreCase);

            if (htmlFaceMatch.Success)
            {
                int parsedFontId;

                if (int.TryParse(
                    htmlFaceMatch.Groups[1].Value,
                    out parsedFontId))
                {
                    if (UoFontReader != null &&
                        parsedFontId >= 0 &&
                        parsedFontId < UoFontReader.Fonts.Count)
                    {
                        htmlFontId = parsedFontId;
                    }
                }
            }


            /*
             * ========================================================
             * TRATAMENTO BÃƒÆ’Ã†â€™Ãƒâ€šÃ‚ÂSICO DO HTML
             * ========================================================
             *
             * AddHtml do UO pode conter comandos de formataÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â§ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â£o.
             *
             * Nesta primeira etapa nÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â£o tentamos reproduzir um navegador.
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
             * Remove tags de formataÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â§ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â£o que nÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â£o devem aparecer
             * literalmente na tela.
             *
             * A fonte real continuarÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ sendo a fonte UO.
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
             * Evita espaÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â§os artificiais causados pelo HTML.
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
             * AddHtml normalmente nÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â£o carrega um fontId da mesma
             * maneira que AddLabel.
             *
             * Para HTML utilizamos Font 0 como fonte padrÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â£o do cliente.
             *
             * AddLabel continua usando a lÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â³gica prÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â³pria jÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡ existente.
             */

            int fontId = htmlFontId;

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
             * RENDERIZAÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢O DAS LINHAS
             * ========================================================
             *
             * RenderText() gera exatamente os pixels da fonte UO.
             *
             * NÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â£o usamos DrawString().
             * NÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â£o redimensionamos o bitmap.
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
                 * NÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â£o desenhamos linhas vazias, mas avanÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â§amos
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
                             * CentralizaÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â§ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â£o horizontal quando o
                             * elemento possui largura definida.
                             *
                             * O tamanho da fonte nÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â£o ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â© alterado.
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
                             * NÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â£o deixar o texto ultrapassar a ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡rea
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
             * O retÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ngulo abaixo ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â© somente uma ferramenta do editor.
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
     * Primeiro tentamos encontrar uma informaÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â§ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â£o explÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â­cita
     * de fonte nos parÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢metros.
     *
     * NÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â£o interpretamos X/Y/Largura/Altura como fonte.
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
                 * SÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â³ utilizamos parÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢metros isolados que representem
                 * uma fonte UO vÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡lida.
                 */
                if (element.Type ==
                    GumpElementType.Label)
                {
                    /*
                     * Label normalmente nÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â£o possui fontId explÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â­cito
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
     * NormalizaÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â§ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â£o de quebras de linha.
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
         * Remove tags HTML sem destruir o conteÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Âºdo.
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
         * Remove caracteres de controle e espaÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â§os artificiais.
         */

        plain =
            plain.Replace(
                "\r",
                "");

        /*
         * Uma tag  vazia nÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â£o gera linha.
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
            if (_selectedElements.Count == 0)
                return;

            const int ClientX = 20;
            const int ClientY = 20;

            float zoom = (float)_zoom;
            GraphicsState state = g.Save();

            try
            {
                g.TranslateTransform(ClientX, ClientY);
                g.ScaleTransform(zoom, zoom);

                for (int i = 0; i < _selectedElements.Count; i++)
                {
                    GumpElement element = _selectedElements[i];
                    if (element == null)
                        continue;

                    Rectangle bounds = GetElementBounds(element);
                    Color borderColor = i == 0 ? Color.Lime : Color.Gold;

                    using (var pen = new Pen(borderColor, 2.0f / zoom))
                    {
                        pen.DashStyle = DashStyle.Dash;
                        g.DrawRectangle(pen, bounds);
                    }
                }
            }
            finally
            {
                g.Restore(state);
            }
        }

        // ============================================================
        // COPIAR ELEMENTO
        // ============================================================

        public bool CopySelectedElement()
        {
            if (_selectedElement == null)
                return false;

            _clipboardElement =
                CloneElement(_selectedElement);

            return _clipboardElement != null;
        }

        // ============================================================
        // RECORTAR ELEMENTO
        // ============================================================

        public bool CutSelectedElement()
        {
            if (_selectedElement == null ||
                Document == null)
            {
                return false;
            }

            _clipboardElement =
                CloneElement(_selectedElement);

            if (_clipboardElement == null)
                return false;

            Document.RemoveElement(
                _selectedElement);

            _selectedElement = null;

            if (Document != null)
                Document.IsModified = true;

            SelectedElementChanged?.Invoke(
                this,
                EventArgs.Empty);

            Invalidate();

            return true;
        }

        // ============================================================
        // COLAR ELEMENTO
        // ============================================================

        public bool PasteElement()
        {
            if (_clipboardElement == null ||
                Document == null)
            {
                return false;
            }

            GumpElement element =
                CloneElement(_clipboardElement);

            if (element == null)
                return false;

            /*
             * A cÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â³pia comeÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â§a 10 pixels deslocada.
             */
            element.X += 10;
            element.Y += 10;

            /*
             * O elemento pertence ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â  pÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡gina atualmente selecionada.
             */
            element.Page =
                Document.CurrentPage;

            /*
             * A linha de origem nÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â£o representa mais uma posiÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â§ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â£o
             * confiÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡vel do arquivo original.
             */
            element.SourceLine = 0;

            Document.AddElement(element);

            Document.IsModified = true;

            _selectedElement = element;

            SelectedElementChanged?.Invoke(
                this,
                EventArgs.Empty);

            Invalidate();

            return true;
        }

        // ============================================================
        // CÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…â€œPIA PROFUNDA
        // ============================================================

        private static GumpElement CloneElement(
            GumpElement source)
        {
            if (source == null)
                return null;

            var clone = new GumpElement
            {
                Type = source.Type,
                CommandName = source.CommandName ?? string.Empty,

                X = source.X,
                Y = source.Y,

                Width = source.Width,
                Height = source.Height,

                Hue = source.Hue,

                Text = source.Text ?? string.Empty,

                ArtId = source.ArtId,
                ButtonId = source.ButtonId,

                Page = source.Page,

                SourceLine = source.SourceLine,
                SourceCode = source.SourceCode ?? string.Empty,

                Font = source.Font,
                TextSize = source.TextSize,
                TextAlign = source.TextAlign,

                Bold = source.Bold,
                Italic = source.Italic,
                Underline = source.Underline
            };

            if (source.Parameters != null)
            {
                foreach (string parameter in source.Parameters)
                {
                    clone.Parameters.Add(
                        parameter ?? string.Empty);
                }
            }

            return clone;
        }

        private bool TryExecuteTestButton(
            GumpElement element)
        {
            if (!TestMode ||
                element == null ||
                element.Type !=
                GumpElementType.Button)
            {
                return false;
            }

            string buttonType =
                string.Empty;

            string parameter =
                "0";

            if (element.Parameters != null &&
                element.Parameters.Count > 5)
            {
                buttonType =
                    element.Parameters[5] ??
                    string.Empty;
            }

            if (element.Parameters != null &&
                element.Parameters.Count > 6)
            {
                parameter =
                    element.Parameters[6] ??
                    "0";
            }

            if (buttonType.IndexOf(
                    "GumpButtonType.Page",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                int targetPage;

                if (!int.TryParse(
                        parameter,
                        out targetPage))
                {
                    targetPage = 0;
                }

                if (targetPage > 0 &&
                    Document != null)
                {
                    Document.CurrentPage =
                        targetPage;

                    SelectedElementChanged?.Invoke(
                        this,
                        EventArgs.Empty);

                    Invalidate();

                    return true;
                }
            }

            if (buttonType.IndexOf(
                    "GumpButtonType.Close",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                TestMode = false;

                System.Media.SystemSounds.Beep.Play();

                Invalidate();

                return true;
            }

            if (buttonType.IndexOf(
                    "GumpButtonType.Reply",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                System.Media.SystemSounds.Beep.Play();

                return true;
            }

            return true;
        }
        protected override void OnMouseDown(
            MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (Document == null || e.Button != MouseButtons.Left)
                return;

            Point point = ScreenToDocument(e.Location);
            GumpElement element = FindElementAt(point);

            if (TestMode && element != null && element.Type == GumpElementType.Button)
            {
                TryExecuteTestButton(element);
                _selectedElements.Clear();
                _selectedElement = null;
                _dragStartPositions.Clear();
                _dragging = false;
                Cursor = Cursors.Hand;
                Invalidate();
                return;
            }

            bool control = (ModifierKeys & Keys.Control) == Keys.Control;

            if (element == null)
            {
                if (!control)
                {
                    _selectedElements.Clear();
                    _selectedElement = null;
                }

                _dragStartPositions.Clear();
                _dragging = false;
                Cursor = Cursors.Default;
                SelectedElementChanged?.Invoke(this, EventArgs.Empty);
                Invalidate();
                return;
            }

            int existingIndex = _selectedElements.IndexOf(element);

            if (control)
            {
                if (existingIndex >= 0)
                    _selectedElements.RemoveAt(existingIndex);
                else
                    _selectedElements.Add(element);

                _selectedElement = _selectedElements.Count > 0 ? _selectedElements[0] : null;
                _dragStartPositions.Clear();
                _dragging = false;
                Cursor = Cursors.Default;
                SelectedElementChanged?.Invoke(this, EventArgs.Empty);
                Invalidate();
                return;
            }

            if (existingIndex < 0)
            {
                _selectedElements.Clear();
                _selectedElements.Add(element);
            }

            _selectedElement = _selectedElements.Count > 0 ? _selectedElements[0] : null;
            _dragStart = point;
            _dragStartPositions.Clear();

            foreach (GumpElement selected in _selectedElements)
            {
                if (selected != null)
                {
                    _dragStartPositions[selected] = new Point(selected.X, selected.Y);
                }
            }

            _dragging = _selectedElements.Count > 0;
            Cursor = _dragging ? Cursors.SizeAll : Cursors.Default;
            SelectedElementChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }
        protected override void OnMouseMove(
            MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (!_dragging || _selectedElements.Count == 0)
                return;

            Point point = ScreenToDocument(e.Location);
            int dx = point.X - _dragStart.X;
            int dy = point.Y - _dragStart.Y;

            int grid = Math.Max(1, GridSize);
            GumpElement anchor = _selectedElement != null ? _selectedElement : _selectedElements[0];

            Point anchorStart;
            if (!_dragStartPositions.TryGetValue(anchor, out anchorStart))
                return;

            int anchorX = anchorStart.X + dx;
            int anchorY = anchorStart.Y + dy;

            anchorX = (int)Math.Round(anchorX / (double)grid) * grid;
            anchorY = (int)Math.Round(anchorY / (double)grid) * grid;

            int snappedDx = anchorX - anchorStart.X;
            int snappedDy = anchorY - anchorStart.Y;

            foreach (GumpElement selected in _selectedElements)
            {
                if (selected == null)
                    continue;

                Point start;
                if (!_dragStartPositions.TryGetValue(selected, out start))
                    continue;

                selected.X = start.X + snappedDx;
                selected.Y = start.Y + snappedDy;
            }

            if (Document != null)
                Document.IsModified = true;

            SelectedElementChanged?.Invoke(this, EventArgs.Empty);
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

                // PÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡gina 0 ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â© global.
                // A pÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡gina atual tambÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â©m deve ser considerada.
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

        // Percorre de trÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¡s para frente para respeitar layering.
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
        public Rectangle GetVisualBounds(
            GumpElement element)
        {
            return GetElementBounds(element);
        }

        public void SelectElement(
            GumpElement element)
        {
            _selectedElements.Clear();
            _dragStartPositions.Clear();

            if (element != null)
                _selectedElements.Add(element);

            _selectedElement = element;
            SelectedElementChanged?.Invoke(
                this,
                EventArgs.Empty);
            Invalidate();
        }
        public void ClearSelection()
        {
            _selectedElements.Clear();
            _dragStartPositions.Clear();
            _selectedElement = null;
            _dragging = false;
            Cursor = Cursors.Default;

            SelectedElementChanged?.Invoke(this, EventArgs.Empty);
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











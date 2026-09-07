using System;
using System.Drawing;
using System.Text.RegularExpressions;

namespace GumpEditor.Rendering
{
    /// <summary>
    /// Renderizador simples de HTML usado pelos Gumps.
    ///
    /// O objetivo aqui é NÃO desenhar as tags HTML na tela.
    /// O HTML original continua sendo preservado no elemento.
    /// </summary>
    public static class GumpHtmlRenderer
    {
        public class HtmlStyle
        {
            public int Font = 3;
            public int Size = 10;
            public Color Color = Color.White;
            public ContentAlignment Alignment = ContentAlignment.TopLeft;
            public bool Bold;
            public bool Italic;
        }

        /// <summary>
        /// Remove as tags HTML deixando somente o texto visível.
        /// </summary>
        public static string StripTags(string html)
        {
            if (string.IsNullOrEmpty(html))
                return string.Empty;

            string text = html;

            // Quebras HTML
            text = Regex.Replace(text, @"<\s*br\s*/?\s*>", "\r\n",
                RegexOptions.IgnoreCase);

            text = Regex.Replace(text, @"</\s*p\s*>", "\r\n",
                RegexOptions.IgnoreCase);

            // Entidades básicas
            text = text.Replace("&nbsp;", " ");
            text = text.Replace("&amp;", "&");
            text = text.Replace("&lt;", "<");
            text = text.Replace("&gt;", ">");
            text = text.Replace("&quot;", "\"");

            // Remove comentários
            text = Regex.Replace(text, @"<!--.*?-->",
                string.Empty,
                RegexOptions.Singleline);

            // Remove todas as tags
            text = Regex.Replace(text, @"<[^>]+>", string.Empty);

            return text.Trim();
        }

        /// <summary>
        /// Tenta identificar propriedades comuns do HTML de Gumps.
        /// </summary>
        public static HtmlStyle ParseStyle(string html)
        {
            HtmlStyle style = new HtmlStyle();

            if (string.IsNullOrEmpty(html))
                return style;

            Match face = Regex.Match(
                html,
                @"(?:face|font)\s*=\s*[""']?(\d+)",
                RegexOptions.IgnoreCase);

            if (face.Success)
            {
                int value;
                if (int.TryParse(face.Groups[1].Value, out value))
                    style.Font = value;
            }

            Match size = Regex.Match(
                html,
                @"size\s*=\s*[""']?(\d+)",
                RegexOptions.IgnoreCase);

            if (size.Success)
            {
                int value;
                if (int.TryParse(size.Groups[1].Value, out value))
                    style.Size = Math.Max(1, value);
            }

            if (Regex.IsMatch(html, @"<\s*b\s*>|<\s*strong\s*>",
                RegexOptions.IgnoreCase))
                style.Bold = true;

            if (Regex.IsMatch(html, @"<\s*i\s*>|<\s*em\s*>",
                RegexOptions.IgnoreCase))
                style.Italic = true;

            if (Regex.IsMatch(html, @"text-align\s*:\s*center|<\s*center\s*>",
                RegexOptions.IgnoreCase))
            {
                style.Alignment = ContentAlignment.TopCenter;
            }
            else if (Regex.IsMatch(html, @"text-align\s*:\s*right",
                RegexOptions.IgnoreCase))
            {
                style.Alignment = ContentAlignment.TopRight;
            }

            return style;
        }
    }
}
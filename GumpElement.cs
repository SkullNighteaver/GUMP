using System;
using System.Collections.Generic;
using System.Drawing;

namespace GumpEditor.Models
{
    public sealed class GumpElement
    {
        public GumpElementType Type { get; set; }

        public string CommandName { get; set; } = string.Empty;

        public List<string> Parameters { get; } =
            new List<string>();

        public int X { get; set; }

        public int Y { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public int Hue { get; set; }

        public string Text { get; set; } = string.Empty;

        public int ArtId { get; set; }

        public int ButtonId { get; set; }

        public int Page { get; set; }

        public int SourceLine { get; set; }

        public string SourceCode { get; set; } = string.Empty;

        // =====================================================
        // PROPRIEDADES VISUAIS DO EDITOR
        // =====================================================

        /// <summary>
        /// Fonte do fonts.mul.
        /// Valores normalmente utilizados pelo cliente: 0-9.
        /// </summary>
        public int Font { get; set; } = -1;

        /// <summary>
        /// Tamanho lógico/auxiliar do texto.
        /// A renderização das fontes UO usa o tamanho natural
        /// armazenado no fonts.mul.
        /// </summary>
        public int TextSize { get; set; } = 12;

        /// <summary>
        /// 0 = esquerda
        /// 1 = centro
        /// 2 = direita
        /// </summary>
        public int TextAlign { get; set; } = 0;

        public bool Bold { get; set; }

        public bool Italic { get; set; }

        public bool Underline { get; set; }

        public Rectangle Bounds
        {
            get
            {
                int width = Width > 0 ? Width : 80;
                int height = Height > 0 ? Height : 20;

                return new Rectangle(
                    X,
                    Y,
                    width,
                    height);
            }
        }

        public string GetParameter(int index)
        {
            if (index < 0 || index >= Parameters.Count)
                return string.Empty;

            return Parameters[index];
        }

        public int GetIntParameter(int index)
        {
            if (index < 0 || index >= Parameters.Count)
                return 0;

            string value = Parameters[index];

            if (int.TryParse(value, out int number))
                return number;

            if (value.StartsWith(
                    "0x",
                    StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(
                    value.Substring(2),
                    System.Globalization.NumberStyles.HexNumber,
                    null,
                    out number))
            {
                return number;
            }

            return 0;
        }

        public override string ToString()
        {
            string name =
                !string.IsNullOrWhiteSpace(CommandName)
                    ? CommandName
                    : Type.ToString();

            if (ArtId > 0)
                name += " | Art " + ArtId;

            if (!string.IsNullOrWhiteSpace(Text))
            {
                string text = Text
                    .Replace("\r", " ")
                    .Replace("\n", " ");

                if (text.Length > 35)
                    text = text.Substring(0, 35) + "...";

                name += " | " + text;
            }

            return name +
                   " | (" +
                   X +
                   "," +
                   Y +
                   ")";
        }
    }
}
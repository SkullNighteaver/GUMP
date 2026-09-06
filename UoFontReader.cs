using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace GumpEditor.ClientData
{
    public sealed class UoGlyph
    {
        public int Width { get; }
        public int Height { get; }
        public Bitmap Image { get; }

        public UoGlyph(int width, int height, Bitmap image)
        {
            Width = width;
            Height = height;
            Image = image;
        }
    }

    public sealed class UoFont
    {
        public int Id { get; }
        public byte Header { get; }
        public int Height { get; private set; }

        private readonly UoGlyph[] _glyphs = new UoGlyph[224];

        public UoFont(int id, byte header)
        {
            Id = id;
            Header = header;
        }

        public void SetGlyph(int index, UoGlyph glyph)
        {
            if (index < 0 || index >= _glyphs.Length)
                return;

            _glyphs[index] = glyph;

            if (glyph != null && glyph.Height > Height)
                Height = glyph.Height;
        }

        public UoGlyph GetGlyph(char character)
        {
            int index = character - 0x20;

            if (index < 0 || index >= 224)
                return null;

            return _glyphs[index];
        }

        public UoGlyph GetGlyph(int index)
        {
            if (index < 0 || index >= 224)
                return null;

            return _glyphs[index];
        }

        public int MeasureText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            int width = 0;

            foreach (char c in text)
            {
                if (c == ' ')
                {
                    width += 8;
                    continue;
                }

                UoGlyph glyph = GetGlyph(c);

                if (glyph != null)
                    width += glyph.Width;
                else
                    width += 8;

                width++;
            }

            return Math.Max(1, width);
        }
    }

    public sealed class UoFontReader : IDisposable
    {
        private const int FontCount = 10;
        private const int GlyphCount = 224;
        private const int FirstCharacter = 0x20;

        private readonly List<UoFont> _fonts =
            new List<UoFont>();

        public IReadOnlyList<UoFont> Fonts
        {
            get { return _fonts.AsReadOnly(); }
        }

        public string ClientDirectory { get; }

        public bool IsLoaded
        {
            get { return _fonts.Count == FontCount; }
        }

        public UoFontReader(string clientDirectory)
        {
            ClientDirectory = clientDirectory;

            Load();
        }

        public UoFont GetFont(int id)
        {
            if (id < 0 || id >= _fonts.Count)
                return null;

            return _fonts[id];
        }

        private void Load()
        {
            _fonts.Clear();

            string path =
                Path.Combine(
                    ClientDirectory,
                    "fonts.mul");

            if (!File.Exists(path))
                return;

            byte[] data = File.ReadAllBytes(path);

            int position = 0;

            for (int fontId = 0;
                 fontId < FontCount;
                 fontId++)
            {
                if (position >= data.Length)
                    break;

                byte header = data[position++];

                var font =
                    new UoFont(
                        fontId,
                        header);

                for (int glyphIndex = 0;
                     glyphIndex < GlyphCount;
                     glyphIndex++)
                {
                    if (position + 3 > data.Length)
                        return;

                    int width = data[position++];
                    int height = data[position++];

                    // Byte desconhecido existente no formato.
                    position++;

                    Bitmap bitmap = null;

                    if (width > 0 && height > 0)
                    {
                        int pixelCount =
                            width *
                            height;

                        int byteCount =
                            pixelCount * 2;

                        if (position + byteCount >
                            data.Length)
                        {
                            return;
                        }

                        bitmap =
                            DecodeGlyph(
                                width,
                                height,
                                data,
                                position);

                        position += byteCount;
                    }

                    font.SetGlyph(
                        glyphIndex,
                        new UoGlyph(
                            width,
                            height,
                            bitmap));
                }

                _fonts.Add(font);
            }
        }

        private static Bitmap DecodeGlyph(
            int width,
            int height,
            byte[] data,
            int position)
        {
            var bitmap =
                new Bitmap(
                    width,
                    height,
                    PixelFormat.Format32bppArgb);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int offset =
                        position +
                        ((y * width + x) * 2);

                    ushort value =
                        BitConverter.ToUInt16(
                            data,
                            offset);

                    if (value == 0)
                    {
                        bitmap.SetPixel(
                            x,
                            y,
                            Color.Transparent);

                        continue;
                    }

                    int r =
                        ((value >> 10) & 0x1F) * 255 / 31;

                    int g =
                        ((value >> 5) & 0x1F) * 255 / 31;

                    int b =
                        (value & 0x1F) * 255 / 31;

                    bitmap.SetPixel(
                        x,
                        y,
                        Color.FromArgb(
                            255,
                            r,
                            g,
                            b));
                }
            }

            return bitmap;
        }

        public Bitmap RenderText(
            int fontId,
            string text,
            Color color,
            float scale)
        {
            UoFont font = GetFont(fontId);

            if (font == null)
                return null;

            if (string.IsNullOrEmpty(text))
                text = "AaBbCc 0123";

            int width =
                (int)Math.Ceiling(
                    font.MeasureText(text) *
                    Math.Max(0.1f, scale));

            int height =
                Math.Max(
                    1,
                    (int)Math.Ceiling(
                        Math.Max(
                            1,
                            font.Height) *
                        Math.Max(0.1f, scale)));

            var output =
                new Bitmap(
                    width,
                    height,
                    PixelFormat.Format32bppArgb);

            using (Graphics graphics =
                   Graphics.FromImage(output))
            {
                graphics.Clear(Color.Transparent);

                float x = 0;

                foreach (char character in text)
                {
                    if (character == ' ')
                    {
                        x += 8 * scale;
                        continue;
                    }

                    UoGlyph glyph =
                        font.GetGlyph(character);

                    if (glyph == null ||
                        glyph.Image == null)
                    {
                        x += 8 * scale;
                        continue;
                    }

                    int drawWidth =
                        Math.Max(
                            1,
                            (int)Math.Round(
                                glyph.Image.Width *
                                scale));

                    int drawHeight =
                        Math.Max(
                            1,
                            (int)Math.Round(
                                glyph.Image.Height *
                                scale));

                    using (var tinted =
                           TintGlyph(
                               glyph.Image,
                               color))
                    {
                        graphics.DrawImage(
                            tinted,
                            new Rectangle(
                                (int)Math.Round(x),
                                0,
                                drawWidth,
                                drawHeight));
                    }

                    x +=
                        (glyph.Width + 1) *
                        scale;
                }
            }

            return output;
        }

        private static Bitmap TintGlyph(
            Bitmap source,
            Color color)
        {
            var result =
                new Bitmap(
                    source.Width,
                    source.Height,
                    PixelFormat.Format32bppArgb);

            for (int y = 0;
                 y < source.Height;
                 y++)
            {
                for (int x = 0;
                     x < source.Width;
                     x++)
                {
                    Color pixel =
                        source.GetPixel(x, y);

                    if (pixel.A == 0)
                    {
                        result.SetPixel(
                            x,
                            y,
                            Color.Transparent);

                        continue;
                    }

                    result.SetPixel(
                        x,
                        y,
                        Color.FromArgb(
                            pixel.A,
                            color.R,
                            color.G,
                            color.B));
                }
            }

            return result;
        }

        public void Dispose()
        {
            foreach (UoFont font in _fonts)
            {
                for (int i = 0;
                     i < GlyphCount;
                     i++)
                {
                    UoGlyph glyph =
                        font.GetGlyph(i);

                    if (glyph != null &&
                        glyph.Image != null)
                    {
                        glyph.Image.Dispose();
                    }
                }
            }

            _fonts.Clear();
        }
    }
}

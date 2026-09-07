using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace GumpEditor.ClientData
{
    public sealed class HueData
    {
        public int Id { get; set; }
        public Color[] Colors { get; set; }
        public string Name { get; set; }
    }

    public sealed class HueReader
    {
        private readonly List<HueData> _hues =
            new List<HueData>();

        private static readonly byte[] Table =
        {
            0x00, 0x08, 0x10, 0x18,
            0x20, 0x29, 0x31, 0x39,
            0x41, 0x4A, 0x52, 0x5A,
            0x62, 0x6A, 0x73, 0x7B,
            0x83, 0x8B, 0x94, 0x9C,
            0xA4, 0xAC, 0xB4, 0xBD,
            0xC5, 0xCD, 0xD5, 0xDE,
            0xE6, 0xEE, 0xF6, 0xFF
        };

        public IReadOnlyList<HueData> Hues
        {
            get { return _hues; }
        }

        public bool IsLoaded { get; private set; }

        public HueReader(string path)
        {
            Load(path);
        }

        private void Load(string path)
        {
            _hues.Clear();

            if (!File.Exists(path))
                return;

            try
            {
                using (
                    var fs =
                        File.OpenRead(path))
                using (
                    var br =
                        new BinaryReader(fs))
                {
                    int id = 1;

                    while (
                        fs.Position + 4 <= fs.Length)
                    {
                        br.ReadUInt32();

                        for (int slot = 0; slot < 8; slot++)
                        {
                            if (fs.Position + 88 > fs.Length)
                                break;

                            var colors =
                                new Color[32];

                            for (int i = 0; i < 32; i++)
                            {
                                ushort value =
                                    br.ReadUInt16();

                                int r =
                                    Table[(value >> 10) & 0x1F];

                                int g =
                                    Table[(value >> 5) & 0x1F];

                                int b =
                                    Table[value & 0x1F];

                                colors[i] =
                                    Color.FromArgb(
                                        255,
                                        r,
                                        g,
                                        b);
                            }

                            br.ReadUInt16();
                            br.ReadUInt16();

                            byte[] nameBytes =
                                br.ReadBytes(20);

                            string name =
                                System.Text.Encoding.ASCII
                                    .GetString(nameBytes)
                                    .TrimEnd('\0', ' ');

                            _hues.Add(
                                new HueData
                                {
                                    Id = id++,
                                    Colors = colors,
                                    Name = name
                                });
                        }
                    }
                }

                IsLoaded = _hues.Count > 0;
            }
            catch
            {
                IsLoaded = false;
            }
        }

        public HueData GetHue(int id)
        {
            if (id <= 0)
                return null;

            int index = id - 1;

            if (index < 0 ||
                index >= _hues.Count)
            {
                return null;
            }

            return _hues[index];
        }

        public Bitmap ApplyHue(
            Bitmap source,
            int hue)
        {
            if (source == null || hue <= 0)
                return source;

            var data =
                GetHue(hue);

            if (data == null)
                return source;

            var result =
                new Bitmap(
                    source.Width,
                    source.Height,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            for (
                int y = 0;
                y < source.Height;
                y++)
            {
                for (
                    int x = 0;
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

                    int gray =
                        (int)(
                            pixel.R * 0.299 +
                            pixel.G * 0.587 +
                            pixel.B * 0.114);

                    int index =
                        gray * 31 / 255;

                    Color hueColor =
                        data.Colors[index];

                    result.SetPixel(
                        x,
                        y,
                        Color.FromArgb(
                            pixel.A,
                            hueColor.R,
                            hueColor.G,
                            hueColor.B));
                }
            }

            return result;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace GumpEditor.ClientData;

public sealed class GumpArtReader
{
    private const uint UopMagic = 0x0050594D;

    private readonly string _filePath;
    private readonly Dictionary<ulong, UopEntry> _entries = new();

    public bool IsLoaded { get; private set; }
    public string LastStatus { get; private set; } = string.Empty;

    public GumpArtReader(string filePath)
    {
        _filePath = filePath;
        Load();
    }

    private void Log(string text)
    {
        LastStatus = text;

        try
        {
            File.AppendAllText(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gump_debug.log"),
                $"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}"
            );
        }
        catch
        {
        }
    }

    private void Load()
    {
        _entries.Clear();

        if (!File.Exists(_filePath))
        {
            Log("UOP NÃO EXISTE: " + _filePath);
            return;
        }

        try
        {
            using var fs = File.OpenRead(_filePath);
            using var br = new BinaryReader(fs);

            uint magic = br.ReadUInt32();

            if (magic != UopMagic)
            {
                Log($"MAGIC UOP INVÁLIDO: 0x{magic:X8}");
                return;
            }

            uint version = br.ReadUInt32();
            uint timestamp = br.ReadUInt32();
            long nextBlock = br.ReadInt64();
            uint blockSize = br.ReadUInt32();
            int count = br.ReadInt32();

            Log($"UOP OK | Version={version} | Blocks={count} | NextBlock=0x{nextBlock:X}");

            int total = 0;

            while (nextBlock != 0)
            {
                fs.Seek(nextBlock, SeekOrigin.Begin);

                int filesCount = br.ReadInt32();
                nextBlock = br.ReadInt64();

                total += filesCount;

                for (int i = 0; i < filesCount; i++)
                {
                    long offset = br.ReadInt64();
                    int headerLength = br.ReadInt32();
                    int compressedLength = br.ReadInt32();
                    int decompressedLength = br.ReadInt32();
                    ulong hash = br.ReadUInt64();
                    uint dataHash = br.ReadUInt32();
                    short flag = br.ReadInt16();

                    if (offset == 0)
                        continue;

                    offset += headerLength;

                    int extra1 = 0;
                    int extra2 = 0;

                    if (flag != 3)
                    {
                        long oldPosition = fs.Position;

                        fs.Seek(offset, SeekOrigin.Begin);

                        extra1 = br.ReadInt32();
                        extra2 = br.ReadInt32();

                        offset += 8;
                        compressedLength -= 8;

                        fs.Seek(oldPosition, SeekOrigin.Begin);
                    }

                    _entries[hash] = new UopEntry
                    {
                        Offset = offset,
                        CompressedLength = compressedLength,
                        DecompressedLength = decompressedLength,
                        Flag = flag,
                        Width = extra1,
                        Height = extra2
                    };
                }
            }

            IsLoaded = true;

            Log($"ENTRADAS UOP: {_entries.Count}");
            Log($"HASH ESPERADO 2269: {CreateHash("build/gumpartlegacymul/00002269.tga"):X16}");

            ulong targetHash =
                CreateHash("build/gumpartlegacymul/00002269.tga");

            if (_entries.TryGetValue(targetHash, out var entry))
            {
                Log(
                    $"ART 2269 ENCONTRADO | " +
                    $"Offset={entry.Offset} | " +
                    $"Compressed={entry.CompressedLength} | " +
                    $"Decompressed={entry.DecompressedLength} | " +
                    $"Flag={entry.Flag} | " +
                    $"Size={entry.Width}x{entry.Height}"
                );
            }
            else
            {
                Log("ART 2269 NÃO ENCONTRADO PELO HASH.");
            }
        }
        catch (Exception ex)
        {
            Log("ERRO AO LER UOP: " + ex);
        }
    }

    public Bitmap GetGump(int id)
    {
        try
        {
            ulong hash =
                CreateHash(
                    $"build/gumpartlegacymul/{id:D8}.tga"
                );

            Log($"GetGump({id}) HASH={hash:X16}");

            if (!_entries.TryGetValue(hash, out var entry))
            {
                Log($"GetGump({id}): HASH NÃO ENCONTRADO.");
                return null;
            }

            Log(
                $"GetGump({id}): ENTRADA OK | " +
                $"Offset={entry.Offset} | " +
                $"Compressed={entry.CompressedLength} | " +
                $"Decompressed={entry.DecompressedLength} | " +
                $"Flag={entry.Flag}"
            );

            byte[] compressed;

            using (var fs = File.OpenRead(_filePath))
            {
                fs.Seek(entry.Offset, SeekOrigin.Begin);

                compressed = new byte[entry.CompressedLength];

                int read = 0;

                while (read < compressed.Length)
                {
                    int n = fs.Read(
                        compressed,
                        read,
                        compressed.Length - read
                    );

                    if (n <= 0)
                        break;

                    read += n;
                }
            }

            if (compressed.Length == 0)
            {
                Log("Dados comprimidos vazios.");
                return null;
            }

            byte[] data = compressed;

            if (entry.Flag == 1 || entry.Flag == 3)
            {
                data = DecompressZlib(
                    compressed,
                    entry.DecompressedLength
                );

                Log($"ZLIB OK: {data.Length} bytes.");
            }

            if (entry.Flag == 3)
            {
                data = BwtDecompress(data);

                Log($"BWT OK: {data.Length} bytes.");
            }

            int width = entry.Width;
            int height = entry.Height;

            int position = 0;

            if (entry.Flag == 3)
            {
                if (data.Length < 8)
                {
                    Log("Dados BWT menores que 8 bytes.");
                    return null;
                }

                width = BitConverter.ToInt32(data, 0);
                height = BitConverter.ToInt32(data, 4);

                position = 8;
            }

            if (width <= 0 || height <= 0)
            {
                Log($"DIMENSÕES INVÁLIDAS: {width}x{height}");
                return null;
            }

            Log($"DIMENSÕES FINAIS: {width}x{height}");

            if (position + height * 4 > data.Length)
            {
                Log("RowLookup ultrapassa tamanho do buffer.");
                return null;
            }

            // Os offsets do RowLookup sao relativos ao inicio
            // da area que contem a tabela de linhas.
            int start = position;

            // O tamanho total restante e calculado ANTES de
            // consumir o RowLookup, exatamente como no cliente.
            int len = data.Length - position;
            int halfLen = len / 4;

            int[] rowLookup = new int[height];

            Buffer.BlockCopy(
                data,
                position,
                rowLookup,
                0,
                height * 4
            );

            position += height * 4;

            using var bitmap =
                new Bitmap(
                    width,
                    height,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb
                );

            for (int y = 0; y < height; y++)
            {
                // IMPORTANTE:
                // RowLookup aponta para offsets relativos ao
                // inicio da area ANTES da tabela RowLookup.
                int rowOffset = rowLookup[y] * 4;

                int p = start + rowOffset;

                if (p < 0 || p >= data.Length)
                    continue;

                int next =
                    y < height - 1
                        ? rowLookup[y + 1]
                        : halfLen;

                int gsize =
                    next - rowLookup[y];

                if (gsize < 0)
                    continue;

                int pixelX = 0;

                for (int i = 0; i < gsize; i++)
                {
                    if (p + 4 > data.Length)
                        break;

                    ushort value =
                        BitConverter.ToUInt16(data, p);

                    ushort run =
                        BitConverter.ToUInt16(data, p + 2);

                    p += 4;

                    if (run == 0)
                        continue;

                    Color color = Color.Transparent;

                    if (value != 0)
                        color = Color16ToColor(value);

                    for (
                        int x = 0;
                        x < run && pixelX < width;
                        x++
                    )
                    {
                        if (value != 0)
                        {
                            bitmap.SetPixel(
                                pixelX,
                                y,
                                color
                            );
                        }

                        pixelX++;
                    }
                }
            }
            Log($"ART {id} DECODIFICADO COM SUCESSO.");

            return new Bitmap(bitmap);
        }
        catch (Exception ex)
        {
            Log(
                $"ERRO GetGump({id}): " +
                ex.GetType().Name +
                " - " +
                ex.Message
            );

            return null;
        }
    }

    private static byte[] DecompressZlib(
        byte[] input,
        int expectedLength)
    {
        using var source =
            new MemoryStream(input);

        using var output =
            new MemoryStream(
                expectedLength > 0
                    ? expectedLength
                    : 4096
            );

        /*
         * O UOP usa ZLib.
         *
         * O cabeçalho ZLib possui 2 bytes.
         * O DeflateStream do .NET precisa receber
         * somente o fluxo DEFLATE.
         */
        source.Position = 2;

        using (
            var deflate =
                new DeflateStream(
                    source,
                    CompressionMode.Decompress
                )
        )
        {
            deflate.CopyTo(output);
        }

        byte[] result =
            output.ToArray();

        if (
            expectedLength > 0 &&
            result.Length != expectedLength
        )
        {
            throw new InvalidDataException(
                $"ZLib tamanho inválido. " +
                $"Esperado={expectedLength}, " +
                $"Obtido={result.Length}"
            );
        }

        return result;
    }
    private static byte[] BwtDecompress(byte[] buffer)
    {
        using var reader =
            new BinaryReader(
                new MemoryStream(buffer)
            );

        _ = reader.ReadUInt32();

        byte firstChar =
            reader.ReadByte();

        ushort[] table =
            new ushort[256 * 256];

        BuildTable(
            table,
            firstChar
        );

        byte[] list =
            new byte[reader.BaseStream.Length - 4];

        int i = 0;

        while (
            reader.BaseStream.Position <
            reader.BaseStream.Length
        )
        {
            byte currentValue =
                firstChar;

            ushort value =
                table[currentValue];

            if (currentValue > 0)
            {
                do
                {
                    table[currentValue] =
                        table[currentValue - 1];

                }
                while (--currentValue > 0);
            }

            table[0] = value;

            list[i++] =
                (byte)value;

            firstChar =
                reader.ReadByte();
        }

        return InternalBwtDecompress(
            list,
            0
        );
    }

    private static void BuildTable(
        ushort[] table,
        byte startValue)
    {
        int index = 0;

        byte firstByte =
            startValue;

        byte secondByte = 0;

        for (
            int i = 0;
            i < 256 * 256;
            i++
        )
        {
            ushort value =
                (ushort)(
                    firstByte +
                    (secondByte << 8)
                );

            table[index++] =
                value;

            firstByte++;

            if (firstByte == 0)
                secondByte++;
        }

        SpanSort(table);
    }

    private static void SpanSort(
        ushort[] table)
    {
        if (table == null || table.Length <= 1)
            return;

        // O código anterior usava Bubble Sort em 65.536 elementos.
        // Isso causava bilhões de comparações e travava o editor.
        //
        // Array.Sort utiliza um algoritmo otimizado e reduz
        // drasticamente o tempo de preparação da tabela BWT.
        Array.Sort(table);
    }

    private static byte[] InternalBwtDecompress(
        byte[] input,
        uint len)
    {
        char[] symbolTable =
            new char[256];

        char[] frequency =
            new char[256];

        int[] partialInput =
            new int[256 * 3];

        for (
            int i = 0;
            i < 256;
            i++
        )
        {
            symbolTable[i] =
                (char)i;
        }

        Buffer.BlockCopy(
            input,
            0,
            partialInput,
            0,
            1024
        );

        int sum = 0;

        for (
            int i = 0;
            i < 256;
            i++
        )
        {
            sum +=
                partialInput[i];
        }

        if (len == 0)
        {
            len =
                (uint)sum;
        }

        if (sum != len)
        {
            throw new InvalidDataException(
                $"BWT tamanho inválido. Sum={sum}, Len={len}"
            );
        }

        byte[] output =
            new byte[len];

        int count = 0;

        int nonZeroCount = 0;

        for (
            int i = 0;
            i < 256;
            i++
        )
        {
            if (
                partialInput[i] != 0
            )
            {
                nonZeroCount++;
            }
        }

        Frequency(
            partialInput,
            frequency
        );

        for (
            int i = 0,
            m = 0;
            i < nonZeroCount;
            ++i
        )
        {
            byte freq =
                (byte)frequency[i];

            symbolTable[
                input[m + 1024]
            ] =
                (char)freq;

            partialInput[
                freq + 256
            ] =
                m + 1;

            m +=
                partialInput[freq];

            partialInput[
                freq + 512
            ] =
                m;
        }

        byte val =
            (byte)symbolTable[0];

        if (len != 0)
        {
            do
            {
                int firstValRef =
                    partialInput[
                        val + 256
                    ];

                output[count] =
                    val;

                if (
                    firstValRef >=
                    partialInput[
                        val + 512
                    ]
                )
                {
                    if (
                        nonZeroCount-- > 0
                    )
                    {
                        ShiftLeft(
                            symbolTable,
                            nonZeroCount
                        );

                        val =
                            (byte)symbolTable[0];
                    }
                }
                else
                {
                    char idx =
                        (char)
                        input[
                            firstValRef +
                            1024
                        ];

                    partialInput[
                        val + 256
                    ]++;

                    if (idx != 0)
                    {
                        ShiftLeft(
                            symbolTable,
                            idx
                        );

                        symbolTable[
                            (byte)idx
                        ] =
                            (char)val;

                        val =
                            (byte)symbolTable[0];
                    }
                }

                count++;

            }
            while (
                count < len
            );
        }

        return output;
    }

    private static void Frequency(
        int[] input,
        char[] output)
    {
        int[] temp =
            new int[256];

        Array.Copy(
            input,
            0,
            temp,
            0,
            256
        );

        for (
            int i = 0;
            i < 256;
            i++
        )
        {
            uint value = 0;

            byte index = 0;

            for (
                int j = 0;
                j < 256;
                j++
            )
            {
                if (
                    temp[j] >
                    value
                )
                {
                    index =
                        (byte)j;

                    value =
                        (uint)temp[j];
                }
            }

            if (value == 0)
                break;

            output[i] =
                (char)index;

            temp[index] = 0;
        }
    }

    private static void ShiftLeft(
        char[] input,
        int max)
    {
        for (
            int i = 0;
            i < max;
            ++i
        )
        {
            input[i] =
                input[i + 1];
        }
    }
    private static Color Color16ToColor(
        ushort value)
    {
        // Conversão oficial utilizada pelo ClassicUO/UO SDK.
        // O GumpArt usa 5 bits por canal.
        //
        // Não usamos multiplicação simples por 255/31,
        // pois os valores exibidos pelo cliente original
        // utilizam esta tabela específica.

        byte[] table =
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

        int r =
            table[(value >> 10) & 0x1F];

        int g =
            table[(value >> 5) & 0x1F];

        int b =
            table[value & 0x1F];

        return Color.FromArgb(
            255,
            r,
            g,
            b
        );
    }

    private static ulong CreateHash(
        string s)
    {
        uint eax = 0;
        uint ecx = 0;
        uint edx = 0;
        uint ebx = 0;
        uint esi = 0;
        uint edi = 0;

        ebx =
            edi =
            esi =
                (uint)s.Length +
                0xDEADBEEF;

        int i = 0;

        for (; i + 12 < s.Length; i += 12)
        {
            edi =
                (uint)(
                    (s[i + 7] << 24) |
                    (s[i + 6] << 16) |
                    (s[i + 5] << 8) |
                    s[i + 4]
                ) + edi;

            esi =
                (uint)(
                    (s[i + 11] << 24) |
                    (s[i + 10] << 16) |
                    (s[i + 9] << 8) |
                    s[i + 8]
                ) + esi;

            edx =
                (uint)(
                    (s[i + 3] << 24) |
                    (s[i + 2] << 16) |
                    (s[i + 1] << 8) |
                    s[i]
                ) - esi;

            edx =
                (edx + ebx) ^
                (esi >> 28) ^
                (esi << 4);

            esi += edi;

            edi =
                (edi - edx) ^
                (edx >> 26) ^
                (edx << 6);

            edx += esi;

            esi =
                (esi - edi) ^
                (edi >> 24) ^
                (edi << 8);

            edi += edx;

            ebx =
                (edx - esi) ^
                (esi >> 16) ^
                (esi << 16);

            esi += edi;

            edi =
                (edi - ebx) ^
                (ebx >> 13) ^
                (ebx << 19);

            ebx += esi;

            esi =
                (esi - edi) ^
                (edi >> 28) ^
                (edi << 4);

            edi += ebx;
        }

        if (s.Length - i > 0)
        {
            switch (s.Length - i)
            {
                case 12:
                    esi += (uint)s[i + 11] << 24;
                    goto case 11;

                case 11:
                    esi += (uint)s[i + 10] << 16;
                    goto case 10;

                case 10:
                    esi += (uint)s[i + 9] << 8;
                    goto case 9;

                case 9:
                    esi += s[i + 8];
                    goto case 8;

                case 8:
                    edi += (uint)s[i + 7] << 24;
                    goto case 7;

                case 7:
                    edi += (uint)s[i + 6] << 16;
                    goto case 6;

                case 6:
                    edi += (uint)s[i + 5] << 8;
                    goto case 5;

                case 5:
                    edi += s[i + 4];
                    goto case 4;

                case 4:
                    ebx += (uint)s[i + 3] << 24;
                    goto case 3;

                case 3:
                    ebx += (uint)s[i + 2] << 16;
                    goto case 2;

                case 2:
                    ebx += (uint)s[i + 1] << 8;
                    goto case 1;

                case 1:
                    ebx += s[i];
                    break;
            }

            esi =
                (esi ^ edi) -
                ((edi >> 18) ^ (edi << 14));

            ecx =
                (esi ^ ebx) -
                ((esi >> 21) ^ (esi << 11));

            edi =
                (edi ^ ecx) -
                ((ecx >> 7) ^ (ecx << 25));

            esi =
                (esi ^ edi) -
                ((edi >> 16) ^ (edi << 16));

            edx =
                (esi ^ ecx) -
                ((esi >> 28) ^ (esi << 4));

            edi =
                (edi ^ edx) -
                ((edx >> 18) ^ (edx << 14));

            eax =
                (esi ^ edi) -
                ((edi >> 8) ^ (edi << 24));

            return
                ((ulong)edi << 32) |
                eax;
        }

        return
            ((ulong)esi << 32) |
            eax;
    }

    private sealed class UopEntry
    {
        public long Offset;
        public int CompressedLength;
        public int DecompressedLength;
        public short Flag;
        public int Width;
        public int Height;
    }
}







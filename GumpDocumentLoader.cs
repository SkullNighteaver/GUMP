using System;
using System.IO;
using GumpEditor.Models;

namespace GumpEditor.Parsing;

/// <summary>
/// Carrega um arquivo C# de Gump e transforma seu conteÃºdo
/// em um GumpDocument utilizando o GumpParser.
/// </summary>
public sealed class GumpDocumentLoader
{
    private readonly GumpParser _parser;

    public GumpDocumentLoader()
    {
        _parser = new GumpParser();
    }

    /// <summary>
    /// Abre e interpreta um arquivo C# de Gump.
    /// </summary>
    public GumpDocument Load(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException(
                "O caminho do arquivo nÃ£o pode estar vazio.",
                nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(
                "O arquivo C# informado nÃ£o foi encontrado.",
                filePath);
        }

        var source = File.ReadAllText(filePath);

        var document = new GumpDocument
        {
            FilePath = filePath,
            SourceCode = source,
            Name = Path.GetFileNameWithoutExtension(filePath),
            IsModified = false
        };

        var elements = _parser.Parse(source);

        foreach (var element in elements)
        {
            document.Elements.Add(element);
        }

        DetectSize(document);

        return document;
    }

    /// <summary>
    /// Analisa os elementos encontrados e determina um tamanho
    /// inicial adequado para a Ã¡rea de ediÃ§Ã£o.
    /// </summary>
    private static void DetectSize(GumpDocument document)
    {
        var maxRight = 0;
        var maxBottom = 0;

        foreach (var element in document.Elements)
        {
            if (element.Type == GumpElementType.Page)
            {
                continue;
            }

            var right = element.X + Math.Max(element.Width, 1);
            var bottom = element.Y + Math.Max(element.Height, 1);

            if (right > maxRight)
            {
                maxRight = right;
            }

            if (bottom > maxBottom)
            {
                maxBottom = bottom;
            }
        }

        // MantÃ©m um tamanho mÃ­nimo razoÃ¡vel para ediÃ§Ã£o.
        document.Width = Math.Max(300, maxRight + 20);
        document.Height = Math.Max(200, maxBottom + 20);
    }
}

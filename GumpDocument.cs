using System;
using System.Collections.Generic;
using GumpEditor.Models;

namespace GumpEditor.Models;

/// <summary>
/// Representa um Gump completo carregado pelo editor.
/// 
/// Guarda:
/// - caminho do arquivo C#;
/// - cÃ³digo-fonte original;
/// - elementos encontrados pelo Parser;
/// - tamanho do Gump;
/// - pÃ¡gina atualmente selecionada.
/// </summary>
public sealed class GumpDocument
{
    /// <summary>
    /// Caminho do arquivo C# aberto.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// CÃ³digo-fonte original do arquivo.
    /// </summary>
    public string SourceCode { get; set; } = string.Empty;

    /// <summary>
    /// Nome do Gump/classe.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Largura visual do Gump.
    /// </summary>
    public int Width { get; set; } = 600;

    /// <summary>
    /// Altura visual do Gump.
    /// </summary>
    public int Height { get; set; } = 600;

    /// <summary>
    /// PÃ¡gina atualmente exibida no editor.
    /// </summary>
    public int CurrentPage { get; set; } = 0;

    /// <summary>
    /// Elementos encontrados no cÃ³digo-fonte.
    /// </summary>
    public List<GumpElement> Elements { get; } = new();

    /// <summary>
    /// Indica se existem alteraÃ§Ãµes ainda nÃ£o salvas.
    /// </summary>
    public bool IsModified { get; set; }

    /// <summary>
    /// Retorna todas as pÃ¡ginas existentes no Gump.
    /// </summary>
    public IEnumerable<int> GetPages()
    {
        var pages = new HashSet<int>();

        // A pÃ¡gina 0 Ã© uma pÃ¡gina vÃ¡lida do sistema de Gumps.
        pages.Add(0);

        foreach (var element in Elements)
        {
            if (element.Type == GumpElementType.Page)
            {
                pages.Add(element.Page);
            }
        }

        return pages;
    }

    /// <summary>
    /// Retorna somente os elementos pertencentes Ã  pÃ¡gina informada.
    /// Elementos estruturais como AddBackground podem existir
    /// antes de AddPage e, nesse caso, pertencem Ã  pÃ¡gina 0.
    /// </summary>
    public IEnumerable<GumpElement> GetElementsForPage(int page)
    {
        foreach (var element in Elements)
        {
            if (element.Type == GumpElementType.Page)
            {
                continue;
            }

            if (element.Page == page)
            {
                yield return element;
            }
        }
    }

    /// <summary>
    /// Remove todos os elementos do documento.
    /// </summary>
    public void Clear()
    {
        Elements.Clear();

        CurrentPage = 0;
        IsModified = false;
    }

    /// <summary>
    /// Adiciona um elemento ao documento.
    /// </summary>
    public void AddElement(GumpElement element)
    {
        if (element == null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        Elements.Add(element);

        IsModified = true;
    }

    /// <summary>
    /// Remove um elemento do documento.
    /// </summary>
    public bool RemoveElement(GumpElement element)
    {
        if (element == null)
        {
            return false;
        }

        var removed = Elements.Remove(element);

        if (removed)
        {
            IsModified = true;
        }

        return removed;
    }

    /// <summary>
    /// ObtÃ©m o primeiro elemento do tipo informado.
    /// </summary>
    public GumpElement FindFirst(GumpElementType type)
    {
        foreach (var element in Elements)
        {
            if (element.Type == type)
            {
                return element;
            }
        }

        return null;
    }

    /// <summary>
    /// Retorna uma descriÃ§Ã£o resumida do documento.
    /// </summary>
    public override string ToString()
    {
        var name = string.IsNullOrWhiteSpace(Name)
            ? "Gump sem nome"
            : Name;

        return $"{name} - {Width}x{Height} - {Elements.Count} elementos";
    }
}


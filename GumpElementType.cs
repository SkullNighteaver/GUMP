namespace GumpEditor.Models;

/// <summary>
/// Tipo de elemento encontrado dentro de um Gump do ModernUO.
/// </summary>
public enum GumpElementType
{
    Unknown,

    // Estrutura
    Page,

    // Fundos e imagens
    Background,
        BookBackground,
    Image,
    ImageTiled,
    ImageTiledButton,

    // Texto
    Label,
    LabelCropped,
    Html,
    HtmlLocalized,

    // Controles
    Button,
    Checkbox,
    Radio,
    TextEntry,

    // Outros
    AlphaRegion,
    Item,
    ItemProperty,
    Tooltip
}


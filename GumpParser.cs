using GumpEditor.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace GumpEditor.Parsing
{
    /// <summary>
    /// Parser completo dos comandos AddXXX de Gumps.
    /// </summary>
    public sealed class GumpParser
    {
        private static readonly Dictionary<string, int> Constants =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        private sealed class CommandMatch
        {
            public int Position;
            public string Command;
            public string Arguments;
            public string SourceCode;
        }

        public List<GumpElement> Parse(string source)
        {
            Constants.Clear();

            var elements = new List<GumpElement>();

            if (string.IsNullOrWhiteSpace(source))
                return elements;

            ParseConstants(source);

            var commands = FindCommands(source);

            var currentPage = 0;

            foreach (var command in commands)
            {
                var args = SplitArguments(command.Arguments);

                if (command.Command.Equals(
                    "AddPage",
                    StringComparison.OrdinalIgnoreCase))
                {
                    var page = ArgInt(args, 0);

                    currentPage = page;

                    var pageElement = new GumpElement
                    {
                        Type = GumpElementType.Page,
                        CommandName = command.Command,
                        Page = page,
                        SourceLine = GetLineNumber(source, command.Position),
                        SourceCode = command.SourceCode
                    };

                    foreach (var parameter in args)
                    {
                        pageElement.Parameters.Add(parameter);
                    }

                    elements.Add(pageElement);

                    continue;
                }

                var element = ParseCommand(
                    command,
                    args,
                    currentPage,
                    source);

                if (element != null)
                    elements.Add(element);
            }

            return elements;
        }

        private static GumpElement ParseCommand(
            CommandMatch command,
            List<string> args,
            int page,
            string source)
        {
            string name = command.Command.ToLowerInvariant();

            GumpElement element;

            switch (name)
            {
                case "addbackground":
                    element = new GumpElement
                    {
                        Type = GumpElementType.Background,
                        X = ArgInt(args, 0),
                        Y = ArgInt(args, 1),
                        Width = ArgInt(args, 2),
                        Height = ArgInt(args, 3),
                        ArtId = ArgInt(args, 4)
                    };
                    break;

                case "addimage":
                    element = new GumpElement
                    {
                        Type = GumpElementType.Image,
                        X = ArgInt(args, 0),
                        Y = ArgInt(args, 1),
                        ArtId = ArgInt(args, 2),
                        Hue = ArgInt(args, 3)
                    };
                    break;

                case "addimagetiled":
                    element = new GumpElement
                    {
                        Type = GumpElementType.ImageTiled,
                        X = ArgInt(args, 0),
                        Y = ArgInt(args, 1),
                        Width = ArgInt(args, 2),
                        Height = ArgInt(args, 3),
                        ArtId = ArgInt(args, 4)
                    };
                    break;

                case "addlabel":
                    element = new GumpElement
                    {
                        Type = GumpElementType.Label,
                        X = ArgInt(args, 0),
                        Y = ArgInt(args, 1),
                        Hue = ArgInt(args, 2),
                        Text = ArgString(args, 3)
                    };
                    break;

                case "addlabelcropped":
                    element = new GumpElement
                    {
                        Type = GumpElementType.LabelCropped,
                        X = ArgInt(args, 0),
                        Y = ArgInt(args, 1),
                        Width = ArgInt(args, 2),
                        Height = ArgInt(args, 3),
                        Hue = ArgInt(args, 4),
                        Text = ArgString(args, 5)
                    };
                    break;

                case "addhtml":
                    element = new GumpElement
                    {
                        Type = GumpElementType.Html,
                        X = ArgInt(args, 0),
                        Y = ArgInt(args, 1),
                        Width = ArgInt(args, 2),
                        Height = ArgInt(args, 3),
                        Text = ArgString(args, 4)
                    };
                    break;

                case "addhtmllocalized":
                    element = new GumpElement
                    {
                        Type = GumpElementType.HtmlLocalized,
                        X = ArgInt(args, 0),
                        Y = ArgInt(args, 1),
                        Width = ArgInt(args, 2),
                        Height = ArgInt(args, 3),
                        Text = "Cliloc: " + ArgInt(args, 4)
                    };
                    break;

                case "addbutton":
                    element = new GumpElement
                    {
                        Type = GumpElementType.Button,
                        X = ArgInt(args, 0),
                        Y = ArgInt(args, 1),
                        ArtId = ArgInt(args, 2),
                        ButtonId = ArgInt(args, 4)
                    };

                    element.Width = 1;
                    element.Height = 1;
                    break;

                case "addcheckbox":
                    element = new GumpElement
                    {
                        Type = GumpElementType.Checkbox,
                        X = ArgInt(args, 0),
                        Y = ArgInt(args, 1),
                        ArtId = ArgInt(args, 2),
                        ButtonId = ArgInt(args, 4),
                        Width = 1,
                        Height = 1
                    };
                    break;

                case "addradio":
                    element = new GumpElement
                    {
                        Type = GumpElementType.Radio,
                        X = ArgInt(args, 0),
                        Y = ArgInt(args, 1),
                        ArtId = ArgInt(args, 2),
                        ButtonId = ArgInt(args, 4),
                        Width = 1,
                        Height = 1
                    };
                    break;

                case "addtextentry":
                    element = new GumpElement
                    {
                        Type = GumpElementType.TextEntry,
                        X = ArgInt(args, 0),
                        Y = ArgInt(args, 1),
                        Width = ArgInt(args, 2),
                        Height = ArgInt(args, 3),
                        Hue = ArgInt(args, 4),
                        ButtonId = ArgInt(args, 5),
                        Text = ArgString(args, 6)
                    };
                    break;

                case "addalpharegion":
                    element = new GumpElement
                    {
                        Type = GumpElementType.AlphaRegion,
                        X = ArgInt(args, 0),
                        Y = ArgInt(args, 1),
                        Width = ArgInt(args, 2),
                        Height = ArgInt(args, 3)
                    };
                    break;

                case "additem":
                    element = new GumpElement
                    {
                        Type = GumpElementType.Item,
                        X = ArgInt(args, 0),
                        Y = ArgInt(args, 1),
                        ArtId = ArgInt(args, 2),
                        Hue = ArgInt(args, 3)
                    };
                    break;

                case "additemproperty":
                    element = new GumpElement
                    {
                        Type = GumpElementType.ItemProperty,
                        Text = "Serial: " + ArgInt(args, 0),
                        Width = 140,
                        Height = 24
                    };
                    break;

                case "addtooltip":
                    element = new GumpElement
                    {
                        Type = GumpElementType.Tooltip,
                        Text = "Cliloc: " + ArgInt(args, 0),
                        Width = 140,
                        Height = 24
                    };
                    break;

                case "addimagetiledbutton":
                    element = new GumpElement
                    {
                        Type = GumpElementType.ImageTiledButton,
                        X = ArgInt(args, 0),
                        Y = ArgInt(args, 1),
                        ArtId = ArgInt(args, 2),
                        ButtonId = ArgInt(args, 4),
                        Width = args.Count > 7 ? ArgInt(args, 7) : 40,
                        Height = args.Count > 8 ? ArgInt(args, 8) : 40
                    };
                    break;

                case "addbookbackground":
                    element = new GumpElement
                    {
                        Type = GumpElementType.BookBackground,
                        X = 75,
                        Y = 6,
                        ArtId = 0x08AC,
                        Width = 0,
                        Height = 0
                    };
                    break;

                default:
                    // NÃO ignoramos mais comandos AddXXX desconhecidos.
                    element = new GumpElement
                    {
                        Type = GumpElementType.Unknown,
                        X = args.Count > 0 ? ArgInt(args, 0) : 0,
                        Y = args.Count > 1 ? ArgInt(args, 1) : 0,
                        Width = 120,
                        Height = 24,
                        Text = command.Command
                    };
                    break;
            }

            // Mantém todas as informações originais do comando.
            element.CommandName = command.Command;

            element.Parameters.Clear();

            foreach (var parameter in args)
            {
                element.Parameters.Add(parameter);
            }

            element.Page = page;
            element.SourceLine = GetLineNumber(source, command.Position);
            element.SourceCode = command.SourceCode;

            return element;
        }

        // ============================================================
        // LOCALIZA TODOS OS AddXXX
        // ============================================================

        private static List<CommandMatch> FindCommands(string source)
        {
            var result = new List<CommandMatch>();

            int position = 0;

            while (position < source.Length)
            {
                int index = source.IndexOf(
                    "Add",
                    position,
                    StringComparison.OrdinalIgnoreCase);

                if (index < 0)
                    break;

                if (index > 0 &&
                    (char.IsLetterOrDigit(source[index - 1]) ||
                     source[index - 1] == '_'))
                {
                    position = index + 3;
                    continue;
                }

                int nameEnd = index + 3;

                while (
                    nameEnd < source.Length &&
                    (char.IsLetterOrDigit(source[nameEnd]) ||
                     source[nameEnd] == '_'))
                {
                    nameEnd++;
                }

                string command =
                    source.Substring(
                        index,
                        nameEnd - index);

                if (nameEnd >= source.Length)
                    break;

                int open = nameEnd;

                while (
                    open < source.Length &&
                    char.IsWhiteSpace(source[open]))
                {
                    open++;
                }

                if (open >= source.Length ||
                    source[open] != '(')
                {
                    position = nameEnd;
                    continue;
                }

                int close =
                    FindClosingParenthesis(
                        source,
                        open);

                if (close < 0)
                    break;

                /*
                 * Ignora comandos AddXXX que estejam dentro
                 * da implementação de um método auxiliar.
                 *
                 * Exemplo:
                 *
                 * private void AddBookBackground()
                 * {
                 *     AddImage(75, 6, BookBackground);
                 * }
                 *
                 * O AddImage acima pertence à implementação
                 * do método e não deve ser interpretado como
                 * um comando executado naquele ponto do Gump.
                 */

                int lineStart =
                    source.LastIndexOf(
                        '\n',
                        index);

                if (lineStart < 0)
                    lineStart = 0;
                else
                    lineStart++;

                string beforeCommand =
                    source.Substring(
                        lineStart,
                        index - lineStart).Trim();

                bool isMethodDeclaration =
                    beforeCommand.EndsWith(
                        "void",
                        StringComparison.OrdinalIgnoreCase) ||
                    beforeCommand.EndsWith(
                        "static void",
                        StringComparison.OrdinalIgnoreCase) ||
                    beforeCommand.EndsWith(
                        "private void",
                        StringComparison.OrdinalIgnoreCase) ||
                    beforeCommand.EndsWith(
                        "public void",
                        StringComparison.OrdinalIgnoreCase) ||
                    beforeCommand.EndsWith(
                        "protected void",
                        StringComparison.OrdinalIgnoreCase) ||
                    beforeCommand.EndsWith(
                        "internal void",
                        StringComparison.OrdinalIgnoreCase);

                if (isMethodDeclaration)
                {
                    int bodyStart = close + 1;

                    while (
                        bodyStart < source.Length &&
                        char.IsWhiteSpace(source[bodyStart]))
                    {
                        bodyStart++;
                    }

                    if (bodyStart < source.Length &&
                        source[bodyStart] == '{')
                    {
                        int depth = 0;
                        bool stringMode = false;
                        bool escaped = false;

                        for (
                            int i = bodyStart;
                            i < source.Length;
                            i++)
                        {
                            char c = source[i];

                            if (stringMode)
                            {
                                if (escaped)
                                {
                                    escaped = false;
                                    continue;
                                }

                                if (c == '\\')
                                {
                                    escaped = true;
                                    continue;
                                }

                                if (c == '"')
                                    stringMode = false;

                                continue;
                            }

                            if (c == '"')
                            {
                                stringMode = true;
                                continue;
                            }

                            if (c == '{')
                            {
                                depth++;
                            }
                            else if (c == '}')
                            {
                                depth--;

                                if (depth == 0)
                                {
                                    position = i + 1;
                                    break;
                                }
                            }
                        }

                        continue;
                    }

                    position = close + 1;
                    continue;
                }

                string arguments =
                    source.Substring(
                        open + 1,
                        close - open - 1);

                string code =
                    source.Substring(
                        index,
                        close - index + 1);

                result.Add(
                    new CommandMatch
                    {
                        Position = index,
                        Command = command,
                        Arguments = arguments,
                        SourceCode = code.Trim()
                    });

                position = close + 1;
            }

            return result;
        }
        private static int FindClosingParenthesis(
            string source,
            int open)
        {
            int depth = 0;
            bool stringMode = false;
            bool escaped = false;

            for (int i = open; i < source.Length; i++)
            {
                char c = source[i];

                if (stringMode)
                {
                    if (escaped)
                    {
                        escaped = false;
                        continue;
                    }

                    if (c == '\\')
                    {
                        escaped = true;
                        continue;
                    }

                    if (c == '"')
                        stringMode = false;

                    continue;
                }

                if (c == '"')
                {
                    stringMode = true;
                    continue;
                }

                if (c == '(')
                    depth++;

                if (c == ')')
                {
                    depth--;

                    if (depth == 0)
                        return i;
                }
            }

            return -1;
        }

        // ============================================================
        // ARGUMENTOS
        // ============================================================

        private static List<string> SplitArguments(
            string value)
        {
            var result = new List<string>();

            var current = new StringBuilder();

            bool stringMode = false;
            bool escaped = false;
            int depth = 0;

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];

                if (stringMode)
                {
                    current.Append(c);

                    if (escaped)
                    {
                        escaped = false;
                        continue;
                    }

                    if (c == '\\')
                    {
                        escaped = true;
                        continue;
                    }

                    if (c == '"')
                        stringMode = false;

                    continue;
                }

                if (c == '"')
                {
                    stringMode = true;
                    current.Append(c);
                    continue;
                }

                if (c == '(')
                {
                    depth++;
                    current.Append(c);
                    continue;
                }

                if (c == ')')
                {
                    depth--;
                    current.Append(c);
                    continue;
                }

                if (c == ',' && depth == 0)
                {
                    result.Add(current.ToString().Trim());
                    current.Clear();
                    continue;
                }

                current.Append(c);
            }

            if (current.Length > 0)
                result.Add(current.ToString().Trim());

            return result;
        }

        private static int ArgInt(
            List<string> args,
            int index)
        {
            if (index < 0 || index >= args.Count)
                return 0;

            return ParseInt(args[index]);
        }

        private static string ArgString(
            List<string> args,
            int index)
        {
            if (index < 0 || index >= args.Count)
                return string.Empty;

            string value = args[index].Trim();

            if (value.Length >= 2 &&
                value[0] == '"' &&
                value[value.Length - 1] == '"')
            {
                value =
                    value.Substring(
                        1,
                        value.Length - 2);
            }

            return value
                .Replace("\\\"", "\"")
                .Replace("\\n", "\n")
                .Replace("\\r", "\r");
        }

        private static void ParseConstants(
            string source)
        {
            int position = 0;

            while (position < source.Length)
            {
                int index =
                    source.IndexOf(
                        "const int",
                        position,
                        StringComparison.OrdinalIgnoreCase);

                if (index < 0)
                    break;

                int equal =
                    source.IndexOf(
                        '=',
                        index);

                int semicolon =
                    source.IndexOf(
                        ';',
                        equal);

                if (equal > 0 &&
                    semicolon > equal)
                {
                    string declaration =
                        source.Substring(
                            index,
                            semicolon - index);

                    string[] parts =
                        declaration.Split(
                            new[] { '=' },
                            2);

                    if (parts.Length == 2)
                    {
                        string left =
                            parts[0].Trim();

                        string right =
                            parts[1].Trim();

                        string[] words =
                            left.Split(
                                new[]
                                {
                                    ' ',
                                    '\t',
                                    '\r',
                                    '\n'
                                },
                                StringSplitOptions.RemoveEmptyEntries);

                        if (words.Length > 0)
                        {
                            string name =
                                words[words.Length - 1];

                            int number;

                            if (TryParseNumber(
                                right,
                                out number))
                            {
                                Constants[name] = number;
                            }
                        }
                    }
                }

                position =
                    semicolon > 0
                        ? semicolon + 1
                        : index + 9;
            }
        }

        private static int ParseInt(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0;

            value = value.Trim();

            int result;

            if (int.TryParse(
                value,
                out result))
            {
                return result;
            }

            if (value.StartsWith(
                "0x",
                StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(
                    value.Substring(2),
                    System.Globalization.NumberStyles.HexNumber,
                    null,
                    out result))
                {
                    return result;
                }
            }

            if (Constants.TryGetValue(
                value,
                out result))
            {
                return result;
            }

            return 0;
        }

        private static bool TryParseNumber(
            string value,
            out int result)
        {
            value = value.Trim();

            if (int.TryParse(
                value,
                out result))
            {
                return true;
            }

            if (value.StartsWith(
                "0x",
                StringComparison.OrdinalIgnoreCase))
            {
                return int.TryParse(
                    value.Substring(2),
                    System.Globalization.NumberStyles.HexNumber,
                    null,
                    out result);
            }

            result = 0;
            return false;
        }

        private static int GetLineNumber(
            string source,
            int position)
        {
            int line = 1;

            for (
                int i = 0;
                i < position && i < source.Length;
                i++)
            {
                if (source[i] == '\n')
                    line++;
            }

            return line;
        }
    }
}



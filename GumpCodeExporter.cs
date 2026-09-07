using System;
using System.Collections.Generic;
using System.Text;
using GumpEditor.Models;

namespace GumpEditor.Parsing
{
    /// <summary>
    /// Exporta o Gump novamente para cÃ³digo C#.
    ///
    /// A regra principal Ã©:
    /// - preservar todos os parÃ¢metros originais;
    /// - alterar somente os valores que o editor modificou;
    /// - nÃ£o destruir parÃ¢metros que o editor ainda nÃ£o possui
    /// como propriedade especÃ­fica.
    /// </summary>
    public static class GumpCodeExporter
    {
        public static string Export(GumpDocument document)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            string source = document.SourceCode ?? string.Empty;

            if (string.IsNullOrWhiteSpace(source))
                throw new InvalidOperationException("O documento nÃ£o possui cÃ³digo-fonte.");

            // Cada elemento Ã© associado Ã  sua ocorrÃªncia sequencial no cÃ³digo original.
            // Isso evita trocar a primeira ocorrÃªncia sempre que existirem comandos iguais.
            var replacements = new List<Tuple<int, int, string>>();
            var occurrenceBySource = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var element in document.Elements)
            {
                if (element == null || string.IsNullOrWhiteSpace(element.SourceCode))
                    continue;

                string key = element.SourceCode;
                int occurrence = 0;
                if (occurrenceBySource.ContainsKey(key))
                    occurrence = occurrenceBySource[key];
                occurrenceBySource[key] = occurrence + 1;

                int index = FindOccurrence(source, key, occurrence);
                if (index < 0)
                    continue;

                string generated = GenerateElement(element);
                if (string.IsNullOrWhiteSpace(generated))
                    continue;

                replacements.Add(Tuple.Create(index, key.Length, generated));
            }

            replacements.Sort(delegate(Tuple<int, int, string> a, Tuple<int, int, string> b)
            {
                return b.Item1.CompareTo(a.Item1);
            });

            foreach (var replacement in replacements)
            {
                source = source.Substring(0, replacement.Item1) +
                         PreserveOriginalFormatting(source.Substring(replacement.Item1, replacement.Item2), replacement.Item3) +
                         source.Substring(replacement.Item1 + replacement.Item2);
            }

            var newElements = new List<GumpElement>();
            foreach (var element in document.Elements)
            {
                if (element != null && string.IsNullOrWhiteSpace(element.SourceCode))
                    newElements.Add(element);
            }

            if (newElements.Count > 0)
                source = InsertNewElements(source, newElements);

            return source;
        }

        private static int FindOccurrence(string source, string value, int occurrence)
        {
            int index = -1;
            int start = 0;

            for (int i = 0; i <= occurrence; i++)
            {
                index = source.IndexOf(value, start, StringComparison.Ordinal);
                if (index < 0)
                    return -1;
                start = index + value.Length;
            }

            return index;
        }

        private static string PreserveOriginalFormatting(string originalCommand, string generatedCommand)
        {
            if (string.IsNullOrEmpty(originalCommand))
                return generatedCommand;

            int originalOpen = originalCommand.IndexOf('(');
            int originalClose = originalCommand.LastIndexOf(')');
            int generatedOpen = generatedCommand.IndexOf('(');
            int generatedClose = generatedCommand.LastIndexOf(')');

            if (originalOpen < 0 || originalClose <= originalOpen ||
                generatedOpen < 0 || generatedClose <= generatedOpen)
                return generatedCommand;

            string originalHead = originalCommand.Substring(0, originalOpen + 1);
            string originalArgs = originalCommand.Substring(originalOpen + 1, originalClose - originalOpen - 1);
            string originalTail = originalCommand.Substring(originalClose);
            string generatedArgs = generatedCommand.Substring(generatedOpen + 1, generatedClose - generatedOpen - 1);

            var oldArgs = SplitTopLevelArguments(originalArgs);
            var newArgs = SplitTopLevelArguments(generatedArgs);

            if (oldArgs.Count != newArgs.Count)
                return generatedCommand;

            var builder = new StringBuilder();
            builder.Append(originalHead);

            for (int i = 0; i < oldArgs.Count; i++)
            {
                string oldArg = oldArgs[i];
                string newArg = newArgs[i];

                int lead = 0;
                while (lead < oldArg.Length && char.IsWhiteSpace(oldArg[lead])) lead++;

                int trail = oldArg.Length;
                while (trail > lead && char.IsWhiteSpace(oldArg[trail - 1])) trail--;

                builder.Append(oldArg.Substring(0, lead));
                builder.Append(newArg.Trim());
                builder.Append(oldArg.Substring(trail));

                if (i < oldArgs.Count - 1)
                    builder.Append(',');
            }

            builder.Append(originalTail);
            return builder.ToString();
        }

        private static List<string> SplitTopLevelArguments(string text)
        {
            var result = new List<string>();
            int start = 0;
            int depth = 0;
            bool inString = false;
            bool inChar = false;
            bool escaped = false;
            bool verbatim = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                char n = i + 1 < text.Length ? text[i + 1] : '\0';

                if (inString)
                {
                    if (verbatim)
                    {
                        if (c == '"')
                        {
                            if (n == '"') i++;
                            else { inString = false; verbatim = false; }
                        }
                    }
                    else
                    {
                        if (escaped) escaped = false;
                        else if (c == '\\') escaped = true;
                        else if (c == '"') inString = false;
                    }
                    continue;
                }

                if (inChar)
                {
                    if (escaped) escaped = false;
                    else if (c == '\\') escaped = true;
                    else if (c == '\'') inChar = false;
                    continue;
                }

                if (c == '@' && n == '"') { inString = true; verbatim = true; i++; continue; }
                if (c == '"') { inString = true; continue; }
                if (c == '\'') { inChar = true; continue; }

                if (c == '(' || c == '[' || c == '{') depth++;
                else if (c == ')' || c == ']' || c == '}') depth--;
                else if (c == ',' && depth == 0)
                {
                    result.Add(text.Substring(start, i - start));
                    start = i + 1;
                }
            }

            if (text.Length > 0 || start == 0)
                result.Add(text.Substring(start));

            return result;
        }

        private static string GenerateElement(
            GumpElement element)
        {
            if (element == null)
                return null;

            /*
             * Se o elemento veio do cÃ³digo original,
             * usamos TODOS os parÃ¢metros encontrados
             * pelo parser.
             *
             * Isso impede que parÃ¢metros desconhecidos
             * sejam perdidos durante o Save.
             */
            if (element.Parameters.Count > 0 &&
                !string.IsNullOrWhiteSpace(
                    element.CommandName))
            {
                var args =
                    new List<string>(
                        element.Parameters);

                UpdateParameters(
                    element,
                    args);

                return
                    element.CommandName +
                    "(" +
                    string.Join(
                        ", ",
                        args) +
                    ");";
            }

            /*
             * Elementos novos ainda nÃ£o possuem
             * parÃ¢metros originais.
             *
             * Neste caso geramos um comando padrÃ£o.
             */
            return GenerateNewElement(element);
        }

        private static void UpdateParameters(
            GumpElement element,
            List<string> args)
        {
            switch (element.Type)
            {
                case GumpElementType.Page:
                    SetInt(
                        args,
                        0,
                        element.Page);
                    break;                case GumpElementType.Background:
                    SetInt(args, 0, element.X);
                    SetInt(args, 1, element.Y);
                    SetInt(args, 2, Math.Max(1, element.Width));
                    SetInt(args, 3, Math.Max(1, element.Height));
                    SetInt(args, 4, element.ArtId);
                    break;

                case GumpElementType.Image:
                    SetInt(args, 0, element.X);
                    SetInt(args, 1, element.Y);
                    SetInt(args, 2, element.ArtId);
                    SetInt(args, 3, element.Hue);
                    break;

                case GumpElementType.ImageTiled:
                    SetInt(args, 0, element.X);
                    SetInt(args, 1, element.Y);
                    SetInt(args, 2, Math.Max(1, element.Width));
                    SetInt(args, 3, Math.Max(1, element.Height));
                    SetInt(args, 4, element.ArtId);
                    break;

                case GumpElementType.Label:
                    SetInt(args, 0, element.X);
                    SetInt(args, 1, element.Y);
                    SetInt(args, 2, element.Hue);
                    SetString(args, 3, element.Text);
                    break;

                case GumpElementType.LabelCropped:
                    SetInt(args, 0, element.X);
                    SetInt(args, 1, element.Y);
                    SetInt(args, 2, Math.Max(1, element.Width));
                    SetInt(args, 3, Math.Max(1, element.Height));
                    SetInt(args, 4, element.Hue);
                    SetString(args, 5, element.Text);
                    break;

                case GumpElementType.Html:
                    SetInt(args, 0, element.X);
                    SetInt(args, 1, element.Y);
                    SetInt(args, 2, Math.Max(1, element.Width));
                    SetInt(args, 3, Math.Max(1, element.Height));
                    SetString(args, 4, element.Text);
                    break;

                case GumpElementType.HtmlLocalized:
                    SetInt(args, 0, element.X);
                    SetInt(args, 1, element.Y);
                    SetInt(args, 2, Math.Max(1, element.Width));
                    SetInt(args, 3, Math.Max(1, element.Height));
                    SetInt(args, 4, element.ArtId);
                    SetInt(args, 5, element.Hue);
                    break;

                case GumpElementType.Button:
                    /*
                     * AddButton:
                     *
                     * 0 = X
                     * 1 = Y
                     * 2 = normalID
                     * 3 = pressedID
                     * 4 = buttonID
                     * 5 = GumpButtonType
                     * 6 = param
                     *
                     * O pressedID original Ã© preservado.
                     * A aÃ§Ã£o e o destino sÃ£o editÃ¡veis no programa.
                     */
                    SetInt(args, 0, element.X);
                    SetInt(args, 1, element.Y);
                    SetInt(args, 2, element.ArtId);
                    SetInt(args, 4, element.ButtonId);

                    while (args.Count <= 6)
                        args.Add("0");

                    if (element.Parameters.Count > 3)
                        args[3] = element.Parameters[3];

                    if (element.Parameters.Count > 5)
                        args[5] = element.Parameters[5];

                    if (element.Parameters.Count > 6)
                        args[6] = element.Parameters[6];

                    break;

                    break;

                case GumpElementType.Checkbox:
                case GumpElementType.Radio:
                    /*
                     * Preservamos todos os parÃ¢metros
                     * que nÃ£o sÃ£o editados pelo modelo.
                     */
                    SetInt(args, 0, element.X);
                    SetInt(args, 1, element.Y);
                    SetInt(args, 2, element.ArtId);

                    if (args.Count > 5)
                        SetInt(args, 5, element.ButtonId);
                    else if (args.Count > 4)
                        SetInt(args, 4, element.ButtonId);

                    break;

                case GumpElementType.TextEntry:
                    SetInt(args, 0, element.X);
                    SetInt(args, 1, element.Y);
                    SetInt(args, 2, Math.Max(1, element.Width));
                    SetInt(args, 3, Math.Max(1, element.Height));
                    SetInt(args, 4, element.Hue);
                    SetInt(args, 5, element.ButtonId);
                    SetString(args, 6, element.Text);
                    break;

                case GumpElementType.AlphaRegion:
                    SetInt(args, 0, element.X);
                    SetInt(args, 1, element.Y);
                    SetInt(args, 2, Math.Max(1, element.Width));
                    SetInt(args, 3, Math.Max(1, element.Height));
                    break;

                case GumpElementType.Item:
                    SetInt(args, 0, element.X);
                    SetInt(args, 1, element.Y);
                    SetInt(args, 2, element.ArtId);
                    SetInt(args, 3, element.Hue);
                    break;

                case GumpElementType.ItemProperty:
                case GumpElementType.Tooltip:
                    /*
                     * Esses comandos normalmente possuem
                     * somente informaÃ§Ãµes sem posiÃ§Ã£o.
                     *
                     * Mantemos os argumentos originais.
                     */
                    break;

                case GumpElementType.ImageTiledButton:
                    SetInt(args, 0, element.X);
                    SetInt(args, 1, element.Y);
                    SetInt(args, 2, element.ArtId);

                    if (args.Count > 4)
                        SetInt(args, 4, element.ButtonId);

                    if (args.Count > 7)
                        SetInt(
                            args,
                            7,
                            Math.Max(1, element.Width));

                    if (args.Count > 8)
                        SetInt(
                            args,
                            8,
                            Math.Max(1, element.Height));

                    break;
            }
        }

        private static string GenerateNewElement(
            GumpElement element)
        {
            switch (element.Type)
            {
                case GumpElementType.Background:
                    return string.Format(
                        "AddBackground({0}, {1}, {2}, {3}, {4});",
                        element.X,
                        element.Y,
                        Math.Max(1, element.Width),
                        Math.Max(1, element.Height),
                        element.ArtId);

                case GumpElementType.Image:
                    return string.Format(
                        "AddImage({0}, {1}, {2}, {3});",
                        element.X,
                        element.Y,
                        element.ArtId,
                        element.Hue);

                case GumpElementType.ImageTiled:
                    return string.Format(
                        "AddImageTiled({0}, {1}, {2}, {3}, {4});",
                        element.X,
                        element.Y,
                        Math.Max(1, element.Width),
                        Math.Max(1, element.Height),
                        element.ArtId);

                case GumpElementType.Button:
                    {
                        string buttonType =
                            "GumpButtonType.Reply";

                        string buttonParam =
                            "0";

                        string pressedId =
                            element.ArtId.ToString();

                        if (element.Parameters != null)
                        {
                            if (element.Parameters.Count > 3 &&
                                !string.IsNullOrWhiteSpace(
                                    element.Parameters[3]))
                            {
                                pressedId =
                                    element.Parameters[3];
                            }

                            if (element.Parameters.Count > 5 &&
                                !string.IsNullOrWhiteSpace(
                                    element.Parameters[5]))
                            {
                                buttonType =
                                    element.Parameters[5];
                            }

                            if (element.Parameters.Count > 6 &&
                                !string.IsNullOrWhiteSpace(
                                    element.Parameters[6]))
                            {
                                buttonParam =
                                    element.Parameters[6];
                            }
                        }

                        return string.Format(
                            "AddButton({0}, {1}, {2}, {3}, {4}, {5}, {6});",
                            element.X,
                            element.Y,
                            element.ArtId,
                            pressedId,
                            element.ButtonId,
                            buttonType,
                            buttonParam);
                    }

                case GumpElementType.Page:
                    return string.Format(
                        "AddPage({0});",
                        element.Page);
                case GumpElementType.Label:
                    return string.Format(
                        "AddLabel({0}, {1}, {2}, \"{3}\");",
                        element.X,
                        element.Y,
                        element.Hue,
                        EscapeString(element.Text));

                case GumpElementType.LabelCropped:
                    return string.Format(
                        "AddLabelCropped({0}, {1}, {2}, {3}, {4}, \"{5}\");",
                        element.X,
                        element.Y,
                        Math.Max(1, element.Width),
                        Math.Max(1, element.Height),
                        element.Hue,
                        EscapeString(element.Text));

                case GumpElementType.Html:
                    return string.Format(
                        "AddHtml({0}, {1}, {2}, {3}, \"{4}\", true, false);",
                        element.X,
                        element.Y,
                        Math.Max(1, element.Width),
                        Math.Max(1, element.Height),
                        EscapeString(element.Text));

                case GumpElementType.HtmlLocalized:
                    return string.Format(
                        "AddHtmlLocalized({0}, {1}, {2}, {3}, {4}, {5}, false, false);",
                        element.X,
                        element.Y,
                        Math.Max(1, element.Width),
                        Math.Max(1, element.Height),
                        element.ArtId,
                        element.Hue);

                case GumpElementType.TextEntry:
                    return string.Format(
                        "AddTextEntry({0}, {1}, {2}, {3}, {4}, {5}, \"{6}\");",
                        element.X,
                        element.Y,
                        Math.Max(1, element.Width),
                        Math.Max(1, element.Height),
                        element.Hue,
                        element.ButtonId,
                        EscapeString(element.Text));

                case GumpElementType.AlphaRegion:
                    return string.Format(
                        "AddAlphaRegion({0}, {1}, {2}, {3});",
                        element.X,
                        element.Y,
                        Math.Max(1, element.Width),
                        Math.Max(1, element.Height));

                case GumpElementType.Item:
                    return string.Format(
                        "AddItem({0}, {1}, {2}, {3});",
                        element.X,
                        element.Y,
                        element.ArtId,
                        element.Hue);

                default:
                    return null;
            }
        }

        private static void SetInt(
            List<string> args,
            int index,
            int value)
        {
            if (index < 0)
                return;

            while (args.Count <= index)
                args.Add("0");

            args[index] =
                value.ToString(
                    System.Globalization.CultureInfo.InvariantCulture);
        }

        private static void SetString(
            List<string> args,
            int index,
            string value)
        {
            if (index < 0)
                return;

            while (args.Count <= index)
                args.Add("\"\"");

            args[index] =
                "\"" +
                EscapeString(value) +
                "\"";
        }

        private static string InsertNewElements(
            string source,
            List<GumpElement> elements)
        {
            int buildStart =
                source.IndexOf(
                    "void Build(",
                    StringComparison.Ordinal);

            if (buildStart < 0)
            {
                buildStart =
                    source.IndexOf(
                        " Build(",
                        StringComparison.Ordinal);
            }

            if (buildStart < 0)
                return source;

            int openBrace =
                source.IndexOf(
                    '{',
                    buildStart);

            if (openBrace < 0)
                return source;

            int depth = 0;
            int closeBrace = -1;

            for (int i = openBrace;
                 i < source.Length;
                 i++)
            {
                if (source[i] == '{')
                    depth++;

                if (source[i] == '}')
                {
                    depth--;

                    if (depth == 0)
                    {
                        closeBrace = i;
                        break;
                    }
                }
            }

            if (closeBrace < 0)
                return source;

            var builder =
                new StringBuilder();

            builder.AppendLine();
            builder.AppendLine(
                "        // Elementos adicionados pelo GumpEditor");

            foreach (var element in elements)
            {
                string code =
                    GenerateElement(element);

                if (!string.IsNullOrWhiteSpace(code))
                {
                    builder.AppendLine(
                        "        " + code);
                }
            }

            return
                source.Substring(0, closeBrace) +
                builder.ToString() +
                "    " +
                source.Substring(closeBrace);
        }

        private static string EscapeString(
            string value)
        {
            if (value == null)
                return string.Empty;

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }
    }
}

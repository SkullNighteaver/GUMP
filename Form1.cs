using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace GumpEditor
{
public partial class Form1 : Form
{
// ============================================================
// MODELO DE ELEMENTO DO GUMP
// ============================================================

    private class GumpElement
    {
        public string Method;
        public int X;
        public int Y;

        public int? Width;
        public int? Height;

        public int LineIndex;

        public string OriginalLine;

        public override string ToString()
        {
            string tamanho = "";

            if (Width.HasValue && Height.HasValue)
            {
                tamanho = " [" + Width + "x" + Height + "]";
            }

            return Method +
                   "  X:" + X +
                   "  Y:" + Y +
                   tamanho;
        }
    }

    // ============================================================
    // CAMPOS
    // ============================================================

    private readonly List<GumpElement> _elements =
        new List<GumpElement>();

    private string _currentFile;
    private string[] _sourceLines;

    private Panel _previewPanel;
    private ListBox _elementList;

    private NumericUpDown _numericX;
    private NumericUpDown _numericY;

    private Label _selectedLabel;

    private Button _openButton;
    private Button _saveButton;
    private Button _applyButton;

    // ============================================================
    // CONSTRUTOR
    // ============================================================

    public Form1()
    {
        InitializeComponent();

        CreateInterface();
    }

    // ============================================================
    // INTERFACE
    // ============================================================

    private void CreateInterface()
    {
        Text = "Gump Editor";
        Width = 1200;
        Height = 750;
        StartPosition = FormStartPosition.CenterScreen;

        // --------------------------------------------------------
        // BOTÃO ABRIR
        // --------------------------------------------------------

        _openButton = new Button
        {
            Text = "Abrir Gump",
            Left = 10,
            Top = 10,
            Width = 110,
            Height = 32
        };

        _openButton.Click += OpenButton_Click;

        Controls.Add(_openButton);

        // --------------------------------------------------------
        // BOTÃO SALVAR
        // --------------------------------------------------------

        _saveButton = new Button
        {
            Text = "Salvar Gump",
            Left = 130,
            Top = 10,
            Width = 110,
            Height = 32,
            Enabled = false
        };

        _saveButton.Click += SaveButton_Click;

        Controls.Add(_saveButton);

        // --------------------------------------------------------
        // PAINEL DE PRÉ-VISUALIZAÇÃO
        // --------------------------------------------------------

        _previewPanel = new Panel
        {
            Left = 10,
            Top = 55,
            Width = 820,
            Height = 640,
            BackColor = Color.FromArgb(35, 35, 35),
            BorderStyle = BorderStyle.FixedSingle,
            AutoScroll = true
        };

        _previewPanel.Paint += PreviewPanel_Paint;

        Controls.Add(_previewPanel);

        // --------------------------------------------------------
        // LISTA DE ELEMENTOS
        // --------------------------------------------------------

        _elementList = new ListBox
        {
            Left = 845,
            Top = 55,
            Width = 330,
            Height = 400
        };

        _elementList.SelectedIndexChanged +=
            ElementList_SelectedIndexChanged;

        Controls.Add(_elementList);

        // --------------------------------------------------------
        // ELEMENTO SELECIONADO
        // --------------------------------------------------------

        _selectedLabel = new Label
        {
            Left = 845,
            Top = 470,
            Width = 330,
            Height = 30,
            Text = "Nenhum elemento selecionado.",
            Font = new Font(
                Font.FontFamily,
                10,
                FontStyle.Bold
            )
        };

        Controls.Add(_selectedLabel);

        // --------------------------------------------------------
        // X
        // --------------------------------------------------------

        Label labelX = new Label
        {
            Left = 845,
            Top = 515,
            Width = 30,
            Height = 25,
            Text = "X:"
        };

        Controls.Add(labelX);

        _numericX = new NumericUpDown
        {
            Left = 880,
            Top = 510,
            Width = 100,
            Minimum = -5000,
            Maximum = 5000
        };

        _numericX.ValueChanged += Position_ValueChanged;

        Controls.Add(_numericX);

        // --------------------------------------------------------
        // Y
        // --------------------------------------------------------

        Label labelY = new Label
        {
            Left = 995,
            Top = 515,
            Width = 30,
            Height = 25,
            Text = "Y:"
        };

        Controls.Add(labelY);

        _numericY = new NumericUpDown
        {
            Left = 1025,
            Top = 510,
            Width = 100
        };

        _numericY.Minimum = -5000;
        _numericY.Maximum = 5000;

        _numericY.ValueChanged += Position_ValueChanged;

        Controls.Add(_numericY);

        // --------------------------------------------------------
        // APLICAR
        // --------------------------------------------------------

        _applyButton = new Button
        {
            Text = "Aplicar posição",
            Left = 845,
            Top = 555,
            Width = 280,
            Height = 35,
            Enabled = false
        };

        _applyButton.Click += ApplyButton_Click;

        Controls.Add(_applyButton);

        // --------------------------------------------------------
        // INFORMAÇÃO
        // --------------------------------------------------------

        Label info = new Label
        {
            Left = 845,
            Top = 610,
            Width = 320,
            Height = 70,
            Text =
                "Dica:\r\n" +
                "Selecione um elemento na lista e altere X/Y.\r\n" +
                "A prévia será atualizada automaticamente.",
            AutoSize = false
        };

        Controls.Add(info);
    }

    // ============================================================
    // ABRIR ARQUIVO
    // ============================================================

    private void OpenButton_Click(object sender, EventArgs e)
    {
        using (OpenFileDialog dialog = new OpenFileDialog())
        {
            dialog.Title = "Abrir Gump C#";
            dialog.Filter =
                "Arquivos C# (*.cs)|*.cs|" +
                "Todos os arquivos (*.*)|*.*";

            dialog.Multiselect = false;

            if (dialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            try
            {
                LoadGump(dialog.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Erro ao abrir o arquivo:\r\n\r\n" +
                    ex.Message,
                    "Erro",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }
    }

    // ============================================================
    // CARREGAR GUMP
    // ============================================================

    private void LoadGump(string file)
    {
        _currentFile = file;

        _sourceLines = File.ReadAllLines(file);

        _elements.Clear();
        _elementList.Items.Clear();

        ParseGump();

        foreach (GumpElement element in _elements)
        {
            _elementList.Items.Add(element);
        }

        _saveButton.Enabled = _elements.Count > 0;

        _previewPanel.Invalidate();

        Text =
            "Gump Editor - " +
            Path.GetFileName(file);

        if (_elements.Count == 0)
        {
            MessageBox.Show(
                "Nenhum elemento de Gump reconhecido foi encontrado.",
                "Gump Editor",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }
    }

    // ============================================================
    // ANALISAR CÓDIGO
    // ============================================================

    private void ParseGump()
    {
        if (_sourceLines == null)
        {
            return;
        }

        string[] supportedMethods =
        {
            "AddBackground",
            "AddButton",
            "AddImage",
            "AddImageTiled",
            "AddLabel",
            "AddHtml",
            "AddItem",
            "AddTextEntry",
            "AddCheck",
            "AddRadio"
        };

        for (int lineIndex = 0;
             lineIndex < _sourceLines.Length;
             lineIndex++)
        {
            string line = _sourceLines[lineIndex];

            foreach (string method in supportedMethods)
            {
                if (!line.Contains(method + "("))
                {
                    continue;
                }

                Match match = Regex.Match(
                    line,
                    method +
                    @"\s*\(\s*" +
                    @"(-?\d+)\s*,\s*" +
                    @"(-?\d+)"
                );

                if (!match.Success)
                {
                    continue;
                }

                int x;
                int y;

                if (!int.TryParse(
                    match.Groups[1].Value,
                    out x))
                {
                    continue;
                }

                if (!int.TryParse(
                    match.Groups[2].Value,
                    out y))
                {
                    continue;
                }

                GumpElement element = new GumpElement();

                element.Method = method;
                element.X = x;
                element.Y = y;
                element.LineIndex = lineIndex;
                element.OriginalLine = line;

                // ------------------------------------------------
                // TAMANHO
                // ------------------------------------------------

                if (method == "AddBackground" ||
                    method == "AddImageTiled")
                {
                    int width;
                    int height;

                    ParseSize(
                        line,
                        out width,
                        out height
                    );

                    element.Width = width;
                    element.Height = height;
                }

                _elements.Add(element);

                break;
            }
        }
    }

    // ============================================================
    // LER TAMANHO
    // ============================================================

    private void ParseSize(
        string line,
        out int width,
        out int height)
    {
        width = 40;
        height = 40;

        Match match = Regex.Match(
            line,
            @"\(\s*-?\d+\s*,\s*-?\d+\s*,\s*(-?\d+)\s*,\s*(-?\d+)"
        );

        if (!match.Success)
        {
            return;
        }

        int.TryParse(
            match.Groups[1].Value,
            out width
        );

        int.TryParse(
            match.Groups[2].Value,
            out height
        );
    }

    // ============================================================
    // SELEÇÃO DE ELEMENTO
    // ============================================================

    private void ElementList_SelectedIndexChanged(
        object sender,
        EventArgs e)
    {
        if (_elementList.SelectedIndex < 0)
        {
            _applyButton.Enabled = false;
            return;
        }

        GumpElement element =
            _elements[_elementList.SelectedIndex];

        _selectedLabel.Text =
            "Selecionado: " +
            element.Method;

        _numericX.Value = element.X;
        _numericY.Value = element.Y;

        _applyButton.Enabled = true;

        _previewPanel.Invalidate();
    }

    // ============================================================
    // ALTERAÇÃO DOS CAMPOS
    // ============================================================

    private void Position_ValueChanged(
        object sender,
        EventArgs e)
    {
        if (_elementList.SelectedIndex < 0)
        {
            return;
        }

        GumpElement element =
            _elements[_elementList.SelectedIndex];

        element.X = (int)_numericX.Value;
        element.Y = (int)_numericY.Value;

        int index = _elementList.SelectedIndex;

        _elementList.Items[index] = element;
        _elementList.SelectedIndex = index;

        _previewPanel.Invalidate();
    }

    // ============================================================
    // APLICAR
    // ============================================================

    private void ApplyButton_Click(
        object sender,
        EventArgs e)
    {
        if (_elementList.SelectedIndex < 0)
        {
            return;
        }

        GumpElement element =
            _elements[_elementList.SelectedIndex];

        element.X = (int)_numericX.Value;
        element.Y = (int)_numericY.Value;

        int index = _elementList.SelectedIndex;

        _elementList.Items[index] = element;
        _elementList.SelectedIndex = index;

        _previewPanel.Invalidate();
    }

    // ============================================================
    // PRÉ-VISUALIZAÇÃO
    // ============================================================

    private void PreviewPanel_Paint(
        object sender,
        PaintEventArgs e)
    {
        Graphics g = e.Graphics;

        g.Clear(Color.FromArgb(35, 35, 35));

        if (_elements.Count == 0)
        {
            using (SolidBrush emptyBrush =
                new SolidBrush(Color.Gray))
            {
                g.DrawString(
                    "Abra um arquivo .cs de Gump",
                    Font,
                    emptyBrush,
                    20,
                    20
                );
            }

            return;
        }

        // --------------------------------------------------------
        // GRADE
        // --------------------------------------------------------

        using (Pen gridPen =
            new Pen(Color.FromArgb(55, 55, 55)))
        {
            for (int x = 0; x < 800; x += 50)
            {
                g.DrawLine(
                    gridPen,
                    x,
                    0,
                    x,
                    800
                );
            }

            for (int y = 0; y < 800; y += 50)
            {
                g.DrawLine(
                    gridPen,
                    0,
                    y,
                    800,
                    y
                );
            }
        }

        // --------------------------------------------------------
        // ELEMENTOS
        // --------------------------------------------------------

        for (int i = 0; i < _elements.Count; i++)
        {
            GumpElement element = _elements[i];

            int width =
                element.Width.HasValue
                    ? element.Width.Value
                    : GetDefaultWidth(element.Method);

            int height =
                element.Height.HasValue
                    ? element.Height.Value
                    : GetDefaultHeight(element.Method);

            Rectangle rect = new Rectangle(
                element.X,
                element.Y,
                width,
                height
            );

            bool selected =
                i == _elementList.SelectedIndex;

            using (SolidBrush fillBrush =
                new SolidBrush(
                    selected
                        ? Color.FromArgb(80, 100, 180)
                        : Color.FromArgb(70, 70, 70)))
            {
                g.FillRectangle(
                    fillBrush,
                    rect
                );
            }

            using (Pen borderPen =
                new Pen(
                    selected
                        ? Color.White
                        : Color.Gray,
                    selected ? 2 : 1))
            {
                g.DrawRectangle(
                    borderPen,
                    rect
                );
            }

            using (SolidBrush textBrush =
                new SolidBrush(Color.White))
            {
                g.DrawString(
                    element.Method,
                    Font,
                    textBrush,
                    element.X + 3,
                    element.Y + 3
                );
            }
        }
    }

    // ============================================================
    // TAMANHOS PADRÃO
    // ============================================================

    private int GetDefaultWidth(string method)
    {
        switch (method)
        {
            case "AddButton":
                return 40;

            case "AddImage":
                return 40;

            case "AddLabel":
                return 100;

            case "AddHtml":
                return 150;

            case "AddItem":
                return 40;

            default:
                return 40;
        }
    }

    private int GetDefaultHeight(string method)
    {
        switch (method)
        {
            case "AddButton":
                return 40;

            case "AddImage":
                return 40;

            case "AddLabel":
                return 20;

            case "AddHtml":
                return 60;

            case "AddItem":
                return 40;

            default:
                return 40;
        }
    }

    // ============================================================
    // SALVAR
    // ============================================================

    private void SaveButton_Click(
        object sender,
        EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_currentFile) ||
            _sourceLines == null)
        {
            return;
        }

        using (SaveFileDialog dialog =
            new SaveFileDialog())
        {
            dialog.Title = "Salvar Gump";
            dialog.Filter =
                "Arquivos C# (*.cs)|*.cs";
            dialog.FileName =
                Path.GetFileName(_currentFile);

            if (dialog.ShowDialog() !=
                DialogResult.OK)
            {
                return;
            }

            try
            {
                string[] outputLines =
                    (string[])_sourceLines.Clone();

                foreach (GumpElement element in _elements)
                {
                    if (element.LineIndex < 0 ||
                        element.LineIndex >=
                        outputLines.Length)
                    {
                        continue;
                    }

                    string line =
                        outputLines[element.LineIndex];

                    string pattern =
                        "(" +
                        Regex.Escape(element.Method) +
                        @"\s*\(\s*)" +
                        @"-?\d+" +
                        @"(\s*,\s*)" +
                        @"-?\d+";

                    string replacement =
                        "$1" +
                        element.X +
                        "$2" +
                        element.Y;

                    Regex regex =
                        new Regex(pattern);

                    outputLines[element.LineIndex] =
                        regex.Replace(
                            line,
                            replacement,
                            1
                        );
                }

                File.WriteAllLines(
                    dialog.FileName,
                    outputLines
                );

                MessageBox.Show(
                    "Gump salvo com sucesso.",
                    "Gump Editor",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Erro ao salvar:\r\n\r\n" +
                    ex.Message,
                    "Erro",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }
    }

        private void Form1_Load(object sender, EventArgs e)
        {

        }
    }


}

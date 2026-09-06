using System;
using System.Drawing;
using System.Windows.Forms;
using GumpEditor.Models;

namespace GumpEditor.Forms
{
    public sealed class AddElementDialog : Form
    {
        private ComboBox _typeCombo;

        private NumericUpDown _x;
        private NumericUpDown _y;
        private NumericUpDown _width;
        private NumericUpDown _height;
        private NumericUpDown _art;
        private NumericUpDown _button;
        private NumericUpDown _hue;

        private TextBox _text;

        public GumpElement Element { get; private set; }

        public AddElementDialog()
        {
            Text = "Adicionar elemento";
            StartPosition = FormStartPosition.CenterParent;
            Width = 400;
            Height = 500;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            Build();
        }

        private void Build()
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                ColumnCount = 2,
                RowCount = 9
            };

            panel.ColumnStyles.Add(
                new ColumnStyle(SizeType.Absolute, 120)
            );

            panel.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 100)
            );

            _typeCombo = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            _typeCombo.Items.Add(GumpElementType.Background);
            _typeCombo.Items.Add(GumpElementType.Image);
            _typeCombo.Items.Add(GumpElementType.ImageTiled);
            _typeCombo.Items.Add(GumpElementType.Button);
            _typeCombo.Items.Add(GumpElementType.Label);
            _typeCombo.Items.Add(GumpElementType.Html);

            _typeCombo.SelectedIndex = 0;

            _x = Number();
            _y = Number();
            _width = Number(52);
            _height = Number(52);
            _art = Number();
            _button = Number(1);
            _hue = Number();

            _text = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                Height = 80
            };

            AddRow(panel, 0, "Tipo", _typeCombo);
            AddRow(panel, 1, "X", _x);
            AddRow(panel, 2, "Y", _y);
            AddRow(panel, 3, "Largura", _width);
            AddRow(panel, 4, "Altura", _height);
            AddRow(panel, 5, "Art ID", _art);
            AddRow(panel, 6, "Button ID", _button);
            AddRow(panel, 7, "Hue", _hue);
            AddRow(panel, 8, "Texto", _text);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 45,
                FlowDirection = FlowDirection.RightToLeft
            };

            var cancel = new Button
            {
                Text = "Cancelar",
                DialogResult = DialogResult.Cancel,
                Width = 90
            };

            var ok = new Button
            {
                Text = "Adicionar",
                Width = 90
            };

            ok.Click += OkClick;

            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);

            Controls.Add(panel);
            Controls.Add(buttons);

            AcceptButton = ok;
            CancelButton = cancel;
        }

        private static NumericUpDown Number(int value = 0)
        {
            return new NumericUpDown
            {
                Minimum = -10000,
                Maximum = 10000,
                Value = value,
                Dock = DockStyle.Fill
            };
        }

        private static void AddRow(
            TableLayoutPanel panel,
            int row,
            string caption,
            Control control)
        {
            panel.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 38)
            );

            panel.Controls.Add(
                new Label
                {
                    Text = caption,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft
                },
                0,
                row
            );

            panel.Controls.Add(control, 1, row);
        }

        private void OkClick(object sender, EventArgs e)
        {
            var type =
                (GumpElementType)_typeCombo.SelectedItem;

            Element = new GumpElement
            {
                Type = type,
                X = (int)_x.Value,
                Y = (int)_y.Value,
                Width = (int)_width.Value,
                Height = (int)_height.Value,
                ArtId = (int)_art.Value,
                ButtonId = (int)_button.Value,
                Hue = (int)_hue.Value,
                Text = _text.Text ?? string.Empty,
                Page = 0,
                SourceLine = 0,
                SourceCode = string.Empty
            };

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}

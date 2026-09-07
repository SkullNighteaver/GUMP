using Server.Gumps;
using Server.Network;

namespace Server.Gumps
{
    public class GumpTesteCompleto : Gump
    {
        private const int Width = 620;
        private const int Height = 520;

        public GumpTesteCompleto()
            : base(50, 50)
        {
            Build();
        }

        private void Build()
        {
            AddPage(0);

            AddBackground(0, 0, Width, Height, 9270);

            AddImage(15, 15, 2269);

            AddLabel(
                90,
                25,
                1153,
                "GUMP EDITOR - TESTE COMPLETO");

            AddLabel(
                90,
                50,
                53,
                "Todos os principais elementos do Gump");

            AddImage(30, 100, 2269);
            AddImage(100, 100, 2269);
            AddImage(170, 100, 2269);
            AddImage(240, 100, 2269, 1153);

            AddImageTiled(
                320,
                90,
                200,
                70,
                9270);

            AddLabel(
                40,
                190,
                1153,
                "Texto normal");

            AddLabel(
                40,
                215,
                53,
                "Outro texto");

            AddLabel(
                40,
                240,
                68,
                "Texto colorido");

            AddLabelCropped(
                40,
                270,
                240,
                30,
                1153,
                "Texto cortado caso ultrapasse o limite");

            AddHtml(
                300,
                180,
                270,
                100,
                "<BASEFONT COLOR='#FFFFFF'>" +
                "<BIG>Informações</BIG><BR><BR>" +
                "Este é um texto HTML dentro do Gump.<BR>" +
                "Ele possui várias linhas.<BR>" +
                "O editor deve conseguir visualizar esta área." +
                "</BASEFONT>",
                true,
                true);

            AddLabel(
                40,
                320,
                1153,
                "Botões:");

            AddButton(
                40,
                350,
                2269,
                2269,
                1,
                GumpButtonType.Reply,
                0);

            AddLabel(
                95,
                360,
                1153,
                "Botão 1");

            AddButton(
                40,
                400,
                2269,
                2269,
                2,
                GumpButtonType.Reply,
                0);

            AddLabel(
                95,
                410,
                1153,
                "Botão 2");

            AddButton(
                200,
                350,
                2269,
                2269,
                3,
                GumpButtonType.Reply,
                0);

            AddLabel(
                255,
                360,
                1153,
                "Botão 3");

            AddLabel(
                350,
                310,
                1153,
                "Opções:");

            AddCheckbox(
                350,
                350,
                210,
                211,
                false,
                10);

            AddLabel(
                385,
                352,
                1153,
                "Ativar habilidade");

            AddRadio(
                350,
                390,
                208,
                209,
                true,
                20);

            AddLabel(
                385,
                392,
                1153,
                "Opção A");

            AddRadio(
                350,
                425,
                208,
                209,
                false,
                21);

            AddLabel(
                385,
                427,
                1153,
                "Opção B");

            AddLabel(
                40,
                455,
                1153,
                "Nome:");

            AddTextEntry(
                100,
                450,
                180,
                30,
                0,
                100,
                "");

            AddPage(1);

            AddBackground(
                0,
                0,
                Width,
                Height,
                9270);

            AddLabel(
                40,
                30,
                1153,
                "PÁGINA 2");

            AddLabel(
                40,
                65,
                53,
                "Esta página serve para testar AddPage.");

            AddImage(
                50,
                120,
                2269);

            AddImage(
                120,
                120,
                2269);

            AddImage(
                190,
                120,
                2269,
                1153);

            AddHtml(
                300,
                100,
                250,
                150,
                "<BASEFONT COLOR='#FFFFFF'>" +
                "<BIG>Página 2</BIG><BR><BR>" +
                "Esta página contém imagens, " +
                "textos e HTML." +
                "</BASEFONT>",
                true,
                true);

            AddButton(
                40,
                350,
                2269,
                2269,
                100,
                GumpButtonType.Page,
                0);

            AddLabel(
                95,
                360,
                1153,
                "Voltar");

            AddButton(
                300,
                300,
                2269,
                2269,
                101,
                GumpButtonType.Reply,
                0);

            AddLabel(
                355,
                310,
                1153,
                "Testar");
        }
    }
}

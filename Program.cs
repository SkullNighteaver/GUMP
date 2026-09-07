using System;
using GumpEditor;
using System.IO;
using System.Windows.Forms;

namespace GumpEditor
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            string log = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "startup.log"
            );

            try
            {
                File.WriteAllText(log, "1 - Main iniciado\r\n");

                Application.EnableVisualStyles();
                File.AppendAllText(log, "2 - EnableVisualStyles OK\r\n");

                Application.SetCompatibleTextRenderingDefault(false);
                File.AppendAllText(log, "3 - TextRendering OK\r\n");

                MainForm form = new MainForm();

                File.AppendAllText(
                    log,
                    "4 - MainForm criado: " +
                    (form != null ? "SIM" : "NAO") +
                    "\r\n"
                );

                if (form == null)
                {
                    MessageBox.Show(
                        "MainForm retornou NULL.",
                        "GumpEditor",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );

                    return;
                }

                File.AppendAllText(log, "5 - Application.Run iniciado\r\n");

                Application.Run(form);

                File.AppendAllText(log, "6 - Application.Run terminou\r\n");
            }
            catch (Exception ex)
            {
                File.AppendAllText(
                    log,
                    "ERRO:\r\n" +
                    ex.ToString() +
                    "\r\n"
                );

                MessageBox.Show(
                    ex.ToString(),
                    "Erro ao iniciar GumpEditor",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }
    }
}

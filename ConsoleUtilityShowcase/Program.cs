using System.Text;
using System;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

using ConEmu.WinForms;
using System.Globalization;
using System.Reflection;

namespace FckingTest
{
    internal static class Program
    {
        private static Form CreateMainForm()
        {
            var form = new Form() { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(10), Text = "Console Utility in a Terminal" };

            FlowLayoutPanel stack;
            form.Controls.Add(stack = new FlowLayoutPanel() { Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.TopDown });

            stack.Controls.Add(new Label() { AutoSize = true, Dock = DockStyle.Top, Text = "This sample illustrates running a console utility\npresenting the user a real terminal window to its console\nas a control embedded in the form.\n\nThe program also gets the output of the utility,\nthough presenting its progress to the user is the main goal.\n\n" });

            Button btnPing;
            stack.Controls.Add(btnPing = new Button() { Text = "Run ping", Dock = DockStyle.Left });
            btnPing.Click += delegate { CreatePingForm().ShowDialog(form); };
            return form;
        }

        private static Form CreatePingForm()
        {
            var form = new Form() { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(10), Text = "Ping Command" };

            FlowLayoutPanel stack;
            form.Controls.Add(stack = new FlowLayoutPanel() { Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.TopDown });

            stack.Controls.Add(new Label() { AutoSize = true, Dock = DockStyle.Top, Text = "Running the ping command.", Padding = new Padding(5) });
            Label labelWaitOrResult;
            stack.Controls.Add(labelWaitOrResult = new Label() { AutoSize = true, Dock = DockStyle.Top, Text = "Please wait…", BackColor = Color.Yellow, Padding = new Padding(5) });

            ConEmuControl conemu;
            var sbText = new StringBuilder();
            stack.Controls.Add(conemu = new ConEmuControl() { MinimumSize = new Size(800, 600), Dock = DockStyle.Top });

            ConEmuStartInfo startInfo = (new ConEmuStartInfo()
            {
                AnsiStreamChunkReceivedEventSink = (sender, args) => sbText.Append(args.GetMbcsText()),
                ConsoleProcessCommandLine = "ping 8.8.8.8",
                LogLevel = ConEmuStartInfo.LogLevels.Basic
            });

            var joinableTaskContext = new Microsoft.VisualStudio.Threading.JoinableTaskContext();
            var _joinableTaskCollection = joinableTaskContext.CreateCollection();
            var JoinableTaskFactory = joinableTaskContext.CreateFactory(_joinableTaskCollection);

            ConEmuSession session = null;
            session = conemu.Start(startInfo,
                JoinableTaskFactory,
                "Default",
                null /** "Arial" */,
                null /** "24" */);


            session.ConsoleProcessExited += delegate
            {
                Match match = Regex.Match(sbText.ToString(), @"\(.*\b(?<pc>\d+)%.*?\)", RegexOptions.Multiline);
                if (!match.Success)
                {
                    labelWaitOrResult.Text = "Ping execution completed, failed to parse the result.";
                    labelWaitOrResult.BackColor = Color.PaleVioletRed;
                }
                else
                {
                    labelWaitOrResult.Text = $"Ping execution completed, lost {match.Groups["pc"].Value} per cent of packets.";
                    labelWaitOrResult.BackColor = Color.Lime;
                }
            };
            session.ConsoleEmulatorClosed += delegate { form.Close(); };

            return form;
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(CreateMainForm());
        }
    }
}
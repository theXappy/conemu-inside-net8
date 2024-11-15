using ConEmu.WinForms;
using System;
using System.Text;
using System.Windows;
using System.Windows.Forms.Integration;
using System.Windows.Forms;

namespace HostingWfInWPF
{
    /// <summary>
    /// Interaction logic for ConEnumWpfHost.xaml
    /// </summary>
    public partial class ConEnumWpfHost : System.Windows.Controls.UserControl
    {
        public event EventHandler ConsoleEmulatorClosed;

        public string ConsoleProcessCommandLine { get; set; }
        public string ConEmuStyle { get; set; } = "<Babun>";

        public ConEnumWpfHost()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Create the interop host control.
            var host =
                new WindowsFormsHost();


            ConEmuControl conemu;
            var sbText = new StringBuilder();
            conemu = new ConEmuControl() { MinimumSize = new System.Drawing.Size(10, 10), Dock = DockStyle.Top };

            ConEmuStartInfo startInfo = (new ConEmuStartInfo()
            {
                AnsiStreamChunkReceivedEventSink = (sender, args) => sbText.Append(args.GetMbcsText()),
                ConsoleProcessCommandLine = ConsoleProcessCommandLine, //@"C:\git\rnet-kit\rnet-repl\RemoteNetRepl\CSharpRepl\bin\Debug\net7.0-windows\rnet-repl.exe",
                LogLevel = ConEmuStartInfo.LogLevels.Basic
            });

            var joinableTaskContext = new Microsoft.VisualStudio.Threading.JoinableTaskContext();
            var _joinableTaskCollection = joinableTaskContext.CreateCollection();
            var JoinableTaskFactory = joinableTaskContext.CreateFactory(_joinableTaskCollection);

            ConEmuSession session = null;
            session = conemu.Start(startInfo,
                JoinableTaskFactory,
                ConEmuStyle,
                null /** "Arial" */,
                null /** "24" */);

            //
            // SS: Disabled 
            //
            //session.ConsoleProcessExited += delegate
            //{
            //    Match match = Regex.Match(sbText.ToString(), @"\(.*\b(?<pc>\d+)%.*?\)", RegexOptions.Multiline);
            //    if (!match.Success)
            //    {
            //        labelWaitOrResult.Text = "Ping execution completed, failed to parse the result.";
            //        labelWaitOrResult.BackColor = Color.PaleVioletRed;
            //    }
            //    else
            //    {
            //        labelWaitOrResult.Text = $"Ping execution completed, lost {match.Groups["pc"].Value} per cent of packets.";
            //        labelWaitOrResult.BackColor = Color.Lime;
            //    }
            //};

            //
            // SS: Disabled 
            //
            session.ConsoleEmulatorClosed += (o, args) =>
            {
                // this.Close(); 
                ConsoleEmulatorClosed?.Invoke(this, args);
            };


            // Assign the MaskedTextBox control as the host control's child.
            host.Child = conemu;

            // Add the interop host control to the Grid
            // control's collection of child controls.
            grid1.Children.Add(host);
        }
    }
}

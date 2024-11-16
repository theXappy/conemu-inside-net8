using ConEmu.WinForms;
using System;
using System.Text;
using System.Windows;
using System.Windows.Forms.Integration;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Runtime.InteropServices;
using System.Threading;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace HostingWfInWPF
{
    /// <summary>
    /// Interaction logic for ConEnumWpfHost.xaml
    /// </summary>
    public partial class ConEnumWpfHost : System.Windows.Controls.UserControl
    {
        public event EventHandler ConsoleEmulatorClosed;
        public string ConEmuStyle { get; set; } = "<Babun>";
        public bool IsStarted { get; set; }

        private ConEmuControl conemu;
        private ConEmuSession _session;

        public ConEnumWpfHost()
        {
            InitializeComponent();
        }

        public Task StartAsync(string exe, string[] args)
        {
            string consoleProcessCommandLine = exe;
            foreach (string arg in args)
            {
                consoleProcessCommandLine += $" \"{arg}\"";
            }

            return StartAsync(consoleProcessCommandLine);
        }

        public Task WriteInputTextAsync(string text)
        {
            AutoResetEvent inputEchoedResetEvent = new AutoResetEvent(false);

            AutoResetEvent inputIdleResetEvent = new AutoResetEvent(false);
            _session.AnsiStreamChunkReceived += LookForIdle;

            var task = _session.WriteInputTextAsync(text);
            if (text.EndsWith('\n') || text.EndsWith('\r'))
            {
                task = task.ContinueWith(_ =>
                {
                    Stopwatch sw = Stopwatch.StartNew();
                    inputEchoedResetEvent.WaitOne(TimeSpan.FromMilliseconds(1500));
                    sw.Stop();
                    Debug.WriteLine($"STOP WATCH FINISHED AFTER: {sw.ElapsedMilliseconds}");
                    Dispatcher.Invoke(SendEnterKeyToControl);
                });
            }

            Task waitForIdleTask = Task.Run(() =>
            {
                inputIdleResetEvent.WaitOne();
                _session.AnsiStreamChunkReceived -= LookForIdle;
            });
            return waitForIdleTask;


            void LookForIdle(object o, AnsiStreamChunkEventArgs args)
            {
                string str = Encoding.ASCII.GetString(args.Chunk);
                // Get rid of ANSI coloring
                str = Regex.Replace(str, "[\\u001b\\u009b][[()#;?]*(?:[0-9]{1,4}(?:;[0-9]{0,4})*)?[0-9A-ORZcf-nqry=><]", "");
                // Heuristic.
                // Might fail for this input: "expression1;\r\nexpression2\r\n"
                // if we got the echo for "expression1;" in a complete chunk
                if (str.TrimEnd().EndsWith(';')) 
                {
                    inputEchoedResetEvent.Set();
                }
                if (str.EndsWith("> "))
                {
                    inputIdleResetEvent.Set();
                }
            }
        }


        // Importing the SendMessage function from user32.dll
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        // Windows Message Constants
        private const uint WM_KEYDOWN = 0x0100;
        private const uint WM_KEYUP = 0x0101;

        // Virtual Key Code for Enter
        private const int VK_RETURN = 0x0D;

        private void SendEnterKeyToControl()
        {
            unsafe
            {
                IntPtr windowHandle = new IntPtr(conemu.TryGetConEmuHwnd());

                if (windowHandle != IntPtr.Zero)
                {
                    // Sending ENTER key down
                    SendMessage(windowHandle, WM_KEYDOWN, (IntPtr)VK_RETURN, IntPtr.Zero);

                    // Sending ENTER key up
                    SendMessage(windowHandle, WM_KEYUP, (IntPtr)VK_RETURN, IntPtr.Zero);
                }
            }
        }

        public Task StartAsync(string consoleProcessCommandLine)
        {
            IsStarted = true;

            // Create the interop host control.
            WindowsFormsHost host =
                new WindowsFormsHost();

            var sbText = new StringBuilder();
            conemu = new ConEmuControl()
            {
                MinimumSize = new System.Drawing.Size(10, 10),
                Dock = DockStyle.Top,
                IsStatusbarVisible = false,
            };

            ConEmuStartInfo startInfo = (new ConEmuStartInfo()
            {
                AnsiStreamChunkReceivedEventSink = (sender, args) => sbText.Append(args.GetMbcsText()),
                ConsoleProcessCommandLine = consoleProcessCommandLine,
                LogLevel = ConEmuStartInfo.LogLevels.Basic
            });

            var joinableTaskContext = new Microsoft.VisualStudio.Threading.JoinableTaskContext();
            var _joinableTaskCollection = joinableTaskContext.CreateCollection();
            var JoinableTaskFactory = joinableTaskContext.CreateFactory(_joinableTaskCollection);

            _session = null;
            _session = conemu.Start(startInfo,
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
            AutoResetEvent inputIdleResetEvent = new AutoResetEvent(false);
            _session.AnsiStreamChunkReceived += LookForIdle;

            //
            // SS: Disabled 
            //
            _session.ConsoleEmulatorClosed += (o, args) =>
            {
                // this.Close(); 
                ConsoleEmulatorClosed?.Invoke(this, args);
            };


            // Assign the MaskedTextBox control as the host control's child.
            host.Child = conemu;

            // Add the interop host control to the Grid
            // control's collection of child controls.
            grid1.Children.Add(host);

            // For 
            Task waitForIdleTask = Task.Run(() =>
            {
                inputIdleResetEvent.WaitOne();
                _session.AnsiStreamChunkReceived -= LookForIdle;
            });
            return waitForIdleTask;


            void LookForIdle(object o, AnsiStreamChunkEventArgs args)
            {
                if (args.Chunk.Length > 2 &&
                    args.Chunk[^2] == (byte)'>' &&
                    args.Chunk[^1] == (byte)' ')
                {
                    inputIdleResetEvent.Set();
                }
            }
        }
    }
}

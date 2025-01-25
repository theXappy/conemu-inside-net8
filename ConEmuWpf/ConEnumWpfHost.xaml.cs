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
            IsVisiableResetEvent = new ManualResetEvent(false);
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

        public async Task WriteInputTextAsync(string text)
        {
            // Disarm existing text/expressions in the input buffer of ConEmu
            await WriteInputTextInnerAsync(";");
            await WriteInputTextInnerAsync(text);
        }

        private Task WriteInputTextInnerAsync(string text)
        {
            AutoResetEvent inputEchoedResetEvent = new AutoResetEvent(false);

            AutoResetEvent inputIdleResetEvent = new AutoResetEvent(false);
            _session.AnsiStreamChunkReceived += LookForIdle;

            var task = _session.WriteInputTextAsync(text);
            task = task.ContinueWith(_ =>
            {
                inputEchoedResetEvent.WaitOne(TimeSpan.FromMilliseconds(1500));
                Dispatcher.Invoke(SendEnterKeyToControl);
            });

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

        ManualResetEvent IsVisiableResetEvent = null;
        AutoResetEvent inputIdleResetEvent = new AutoResetEvent(false);
        public async Task StartAsync(string consoleProcessCommandLine)
        {
            // Create the interop host control.
            WindowsFormsHost host = new WindowsFormsHost();

            // TODO: If our control is not a child of the main window, this code is wrong.
            var mainWindow = System.Windows.Application.Current.MainWindow;
            if (mainWindow == null)
                throw new Exception("Can't reason when MainWindow is null.");
            // wait for our control be to VISISBLE and the window to NOT BE MINIMZED.
            if (!IsVisible || mainWindow.WindowState == WindowState.Minimized)
            {
                await Task.Run(WaitUntilVisible);
            }

            StartConemuProc(consoleProcessCommandLine);


            // Assign the MaskedTextBox control as the host control's child.
            host.Child = conemu;

            // Add the interop host control to the Grid
            // control's collection of child controls.
            grid1.Children.Add(host);

            // For 
            await Task.Run(() =>
            {
                inputIdleResetEvent.WaitOne();
                _session.AnsiStreamChunkReceived -= LookForIdle;
            });

            IsStarted = true;
            return;
        }

        private void WaitUntilVisible()
        {
            IsVisiableResetEvent.WaitOne();
            // Check if the app's main window is minimized
            while (IsWindowMinimized())
            {
                Debug.WriteLine("[xxx] Waiting for the main window to be restored.");
                Thread.Sleep(100);
            }
            Debug.WriteLine("[xxx] Main window is restored.");

            bool IsWindowMinimized()
            {
                bool isMainWindowMinimized = false;
                Dispatcher.Invoke(() => { isMainWindowMinimized = (System.Windows.Application.Current.MainWindow.WindowState == WindowState.Minimized); });
                return isMainWindowMinimized;
            }
        }

        private void StartConemuProc(string consoleProcessCommandLine)
        {
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

            _session.AnsiStreamChunkReceived += LookForIdle;

            _session.ConsoleEmulatorClosed += (o, args) =>
            {
                ConsoleEmulatorClosed?.Invoke(this, args);
            };
        }

        void LookForIdle(object o, AnsiStreamChunkEventArgs args)
        {
            if (args.Chunk.Length > 2 &&
                args.Chunk[^2] == (byte)'>' &&
                args.Chunk[^1] == (byte)' ')
            {
                inputIdleResetEvent.Set();
            }
        }

        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (this.IsVisible)
                IsVisiableResetEvent.Set();
            else
                IsVisiableResetEvent.Reset();
        }
    }
}

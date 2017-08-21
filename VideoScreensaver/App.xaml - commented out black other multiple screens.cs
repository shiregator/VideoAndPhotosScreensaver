using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;
using Application = System.Windows.Application;
using Cursors = System.Windows.Input.Cursors;

namespace VideoScreensaver
{

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT {
            public int left, top, right, bottom;
        }

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsWindow(IntPtr hWnd);


        private void OnStartup(object sender, StartupEventArgs e) {
            if (e.Args.Length > 0) {
                switch (e.Args[0].Substring(0, 2).ToLower()) {
                    case "/c":
                        // User clicked the "configure" button.
                        ConfigureScreensaver();
                        Shutdown(0);
                        return;
                    case "/p":
                        // Previewing inside of a Win32 window specified by args[1].
                        ShowInParent(new IntPtr(Convert.ToInt32(e.Args[1])));
                        return;
                }
            }
            
			/*
            foreach (var screen in Screen.AllScreens)
            {
                if (!screen.Primary)  // on other screens we show black screen
                {
                    var blackWindow = new Window();
                    blackWindow.WindowStyle = WindowStyle.None;
                    blackWindow.ResizeMode = ResizeMode.NoResize;
                    blackWindow.ShowInTaskbar = false;
                    blackWindow.Left = screen.WorkingArea.Left;
                    blackWindow.Top = screen.WorkingArea.Top;
                    blackWindow.Width = screen.WorkingArea.Width;
                    blackWindow.Height = screen.WorkingArea.Height;
                    blackWindow.Topmost = true;
                    blackWindow.Background = new SolidColorBrush(Colors.Black);
// Commented out to workaround keys not working   FIXED
                    blackWindow.Show();
                    blackWindow.WindowState = WindowState.Maximized;
                }
            }
			*/
            var prscreen = Screen.PrimaryScreen;
            var mainWindow = new MainWindow(false); // on Primary screen we show our screensaver
            mainWindow.WindowStyle = WindowStyle.None;
            mainWindow.ResizeMode = ResizeMode.NoResize;
            mainWindow.ShowInTaskbar = false;
            mainWindow.Left = prscreen.WorkingArea.Left;
            mainWindow.Top = prscreen.WorkingArea.Top;
            mainWindow.Width = prscreen.WorkingArea.Width;
            mainWindow.Height = prscreen.WorkingArea.Height;
            mainWindow.Topmost = true;
            mainWindow.WindowState = WindowState.Maximized;
            mainWindow.Show();
        }

        private async void  ShowInParent(IntPtr parentHwnd) {
            MainWindow previewContent = new MainWindow(true);
            WindowInteropHelper windowHelper = new WindowInteropHelper(previewContent);
            windowHelper.Owner = parentHwnd;
            previewContent.WindowState = WindowState.Normal;
            RECT parentRect;
            GetClientRect(parentHwnd, out parentRect);
            previewContent.Left = 0;
            previewContent.Top = 0;
            previewContent.Width = 0;
            previewContent.Height = 0;
            previewContent.ShowInTaskbar = false;
            previewContent.ShowActivated = false;  // Doesn't work, so we'll use SetForegroundWindow() to restore focus.
            previewContent.Cursor = Cursors.Arrow;
            previewContent.ForceCursor = false;

            IntPtr currentFocus = GetForegroundWindow();
            previewContent.Show();

            SetParent(windowHelper.Handle, parentHwnd);
            SetWindowLong(windowHelper.Handle, -16, new IntPtr(0x10000000 | 0x40000000 | 0x02000000));

            previewContent.Width = parentRect.right - parentRect.left;
            previewContent.Height = parentRect.bottom - parentRect.top;
            SetForegroundWindow(currentFocus);

            // check if preview window is still exists
            await Task.Factory.StartNew(() =>
            {
                while (true)
                {
                    if (!IsWindow(parentHwnd)) return;
                    Task.Delay(1000).Wait();
                }
            });
            // shutdown after preview window closed
            Shutdown();
        }

        private void ConfigureScreensaver() {
            new SettingsWindow().ShowDialog();
            /*
            List<String> videoUri = PreferenceManager.ReadVideoSettings();
            Microsoft.Win32.OpenFileDialog openDialog = new Microsoft.Win32.OpenFileDialog();
            openDialog.Multiselect = true;
            openDialog.FileName = (videoUri.Count > 0) ? videoUri[0] : "";
            openDialog.Title = "Select video/image files to display...";
            if (openDialog.ShowDialog() == true) {
                List<String> videos = new List<String>();
                videos.AddRange(openDialog.FileNames);
                PreferenceManager.WriteVideoSettings(videos);
            }*/
        }
    }
}

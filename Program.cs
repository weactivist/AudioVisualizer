using System;
using System.Windows.Forms;

namespace AudioVisualizer
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var overlay = new RadarOverlayForm();
            var audio = new AudioDirection();
            audio.OnDirection += overlay.UpdateDirection;

            overlay.Show();
            audio.Start();

            Application.ApplicationExit += (s, e) =>
            {
                audio.Stop();
                audio.Dispose();
            };

            Application.Run();
        }
    }
}

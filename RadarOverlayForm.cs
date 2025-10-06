using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

class RadarOverlayForm : Form
{
    private float direction = 0f; // -1..+1
    private System.Windows.Forms.Timer? renderTimer;
    private NotifyIcon? trayIcon;
    private Color ellipseColor = Color.Magenta;
    private int ellipseRadius = 50;

    public RadarOverlayForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        TopMost = true;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Width = Screen.PrimaryScreen!.Bounds.Width;
        Height = Screen.PrimaryScreen!.Bounds.Height;

        // Make window layered and click-through
        int exStyle = (int)GetWindowLong(Handle, GWL_EXSTYLE);
        exStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT;
        SetWindowLong(Handle, GWL_EXSTYLE, (IntPtr)exStyle);

        renderTimer = new System.Windows.Forms.Timer();
        renderTimer.Interval = 33; // ~30 FPS
        renderTimer.Tick += (s, e) => RenderOverlay();
        renderTimer.Start();

        // Create tray icon
        trayIcon = new NotifyIcon();
        trayIcon.Icon = new Icon(Path.Combine(AppContext.BaseDirectory, "graphics/icon.ico"));
        trayIcon.Text = "Audio Visualizer";
        trayIcon.Visible = true;

        // Add context menu
        trayIcon.ContextMenuStrip = new ContextMenuStrip();
        trayIcon.ContextMenuStrip.Items.Add("Choose Color", null, OnChooseColor);
        trayIcon.ContextMenuStrip.Items.Add("Change Size", null, OnChangeSize);
        trayIcon.ContextMenuStrip.Items.Add("Exit", null, (s, e) => Close());
    }

    public void UpdateDirection(float dir)
    {
        direction = Math.Max(-1f, Math.Min(1f, dir));
    }

    private void RenderOverlay()
    {
        int w = Width;
        int h = Height;
        float maxOffsetX = w / 2;
        float maxOffsetY = h / 2;
        float maxOffset = maxOffsetX + maxOffsetY;
        float ratioY = maxOffsetY / maxOffsetX;
        float ratioX = maxOffsetX / maxOffsetY;
        int centerX = Math.Clamp((int)(maxOffsetX + direction * maxOffset), 0, w);
        int centerY = 0;

        if (centerX == 0)
        {
            centerY = Math.Clamp((int)((direction * -1 - ratioY) * maxOffset), 0, (int)maxOffsetY);
        }
        else if (centerX == w)
        {
            centerY = Math.Clamp((int)((direction - ratioY) * maxOffset), 0, (int)maxOffsetY);
        }

        using (Bitmap bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);

            Rectangle ellipseRect = new Rectangle(
                centerX - ellipseRadius,
                centerY - ellipseRadius,
                ellipseRadius * 2,
                ellipseRadius * 2
            );

            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddEllipse(ellipseRect);

                using (PathGradientBrush brush = new PathGradientBrush(path))
                {
                    brush.CenterColor = Color.FromArgb(255, ellipseColor);
                    brush.SurroundColors = new Color[] { Color.FromArgb(0, ellipseColor) };
                    brush.CenterPoint = new Point(ellipseRect.X + ellipseRadius, ellipseRect.Y + ellipseRadius);

                    g.CompositingMode = CompositingMode.SourceOver;
                    g.FillPath(brush, path); // fill the path, not a separate ellipse
                }
            }

            ApplyBitmap(bmp);
        }
    }


    private void ApplyBitmap(Bitmap bmp)
    {
        IntPtr screenDC = GetDC(IntPtr.Zero);
        IntPtr memDC = CreateCompatibleDC(screenDC);
        IntPtr hBitmap = bmp.GetHbitmap(Color.FromArgb(0));
        IntPtr oldBitmap = SelectObject(memDC, hBitmap);

        SIZE size = new SIZE { cx = bmp.Width, cy = bmp.Height };
        POINT topPos = new POINT { x = Left, y = Top };
        POINT ptSrc = new POINT { x = 0, y = 0 };
        BLENDFUNCTION blend = new BLENDFUNCTION { BlendOp = 0x00, BlendFlags = 0, SourceConstantAlpha = 255, AlphaFormat = 0x01 };

        UpdateLayeredWindow(Handle, screenDC, ref topPos, ref size, memDC, ref ptSrc, 0, ref blend, 2);

        SelectObject(memDC, oldBitmap);
        DeleteObject(hBitmap);
        DeleteDC(memDC);
        ReleaseDC(IntPtr.Zero, screenDC);
    }

    private void OnChooseColor(object? sender, EventArgs e)
    {
        using (ColorDialog dlg = new ColorDialog())
        {
            dlg.Color = ellipseColor; // show current color
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                ellipseColor = dlg.Color;
                // Force redraw immediately
                RenderOverlay();
            }
        }
    }

    private void OnChangeSize(object? sender, EventArgs e)
    {
        Form sizeForm = new Form();
        sizeForm.FormBorderStyle = FormBorderStyle.FixedToolWindow;
        sizeForm.StartPosition = FormStartPosition.CenterScreen;
        sizeForm.Width = 300;
        sizeForm.Height = 80;
        sizeForm.Text = "Adjust Size";

        TrackBar trackBar = new TrackBar();
        trackBar.Minimum = 10;
        trackBar.Maximum = 200;
        trackBar.Value = ellipseRadius;
        trackBar.Dock = DockStyle.Fill;
        trackBar.TickFrequency = 10;

        trackBar.Scroll += (s2, e2) =>
        {
            ellipseRadius = trackBar.Value;
            RenderOverlay(); // update overlay in real-time
        };

        sizeForm.Controls.Add(trackBar);
        sizeForm.Show();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        renderTimer?.Stop();
        renderTimer?.Dispose();
        renderTimer = null;

        // Dispose the tray icon to let the process exit
        if (trayIcon != null)
        {
            trayIcon.Visible = false;
            trayIcon.Dispose();
            trayIcon = null;
        }

        base.OnFormClosing(e);
        Application.Exit();
    }

    // Win32 structs and methods
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int x, y; }
    [StructLayout(LayoutKind.Sequential)] struct SIZE { public int cx, cy; }
    [StructLayout(LayoutKind.Sequential, Pack = 1)] struct BLENDFUNCTION
    {
        public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat;
    }

    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hDC);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hDC);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr hObject);
    [DllImport("user32.dll", SetLastError = true)]
    static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize,
        IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);
    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong);


    private const int WS_EX_LAYERED = 0x80000;
    private const int WS_EX_TRANSPARENT = 0x20;
    private const int GWL_EXSTYLE = -20;
}

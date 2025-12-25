/*
 * Lone EFT DMA Radar
 * Brought to you by Lone (Lone DMA)
 * 
MIT License

Copyright (c) 2025 Lone DMA

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
 *
*/

using ImGuiNET;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using System.Diagnostics;

namespace LoneEftDmaRadar.UI.Misc
{
    /// <summary>
    /// A loading window using Silk.NET and ImGui that displays loading progress and status.
    /// </summary>
    internal sealed partial class LoadingWindow : IDisposable
    {
        private readonly IWindow _window;
        private GL _gl;
        private ImGuiController _imgui;

        private float _progress;
        private string _statusText = "Initializing...";
        private string _currentStep = "";
        private bool _isRunning;
        private bool _disposed;

        // Animation state
        private float _pulsePhase;
        private readonly Stopwatch _animationTimer = Stopwatch.StartNew();

        /// <summary>
        /// Current progress value (0-100).
        /// </summary>
        public float Progress
        {
            get => _progress;
            set => _progress = Math.Clamp(value, 0f, 100f);
        }

        /// <summary>
        /// Current status text to display (main heading).
        /// </summary>
        public string StatusText
        {
            get => _statusText;
            set => _statusText = value ?? string.Empty;
        }

        /// <summary>
        /// Current step description (sub-text below progress bar).
        /// </summary>
        public string CurrentStep
        {
            get => _currentStep;
            set => _currentStep = value ?? string.Empty;
        }

        public LoadingWindow()
        {
            var options = WindowOptions.Default;
            options.Size = new Vector2D<int>(550, 220);
            options.Title = "EFT DMA Radar - Loading...";
            options.WindowBorder = WindowBorder.Hidden;
            options.WindowState = WindowState.Normal;
            options.VSync = false;
            options.TopMost = true;
            options.ShouldSwapAutomatically = false;

            _window = Window.Create(options);
            _window.Load += OnLoad;
            _window.Closing += OnClosing;
        }

        /// <summary>
        /// Shows the loading window and initializes it.
        /// </summary>
        public void Show()
        {
            _window.Initialize();
            _isRunning = true;

            // Center the window on screen
            var monitor = _window.Monitor;
            if (monitor is not null)
            {
                var screenSize = monitor.VideoMode.Resolution ?? new Vector2D<int>(1920, 1080);
                var windowSize = _window.Size;
                _window.Position = new Vector2D<int>(
                    (screenSize.X - windowSize.X) / 2,
                    (screenSize.Y - windowSize.Y) / 2);
            }
        }

        /// <summary>
        /// Process window events and render a frame. Call this periodically to keep the window responsive.
        /// </summary>
        public void DoEvents()
        {
            if (!_isRunning || _disposed || _window.IsClosing)
                return;

            _window.DoEvents();

            if (_window.IsClosing)
                return;

            RenderFrame();
        }

        private void RenderFrame()
        {
            if (_imgui is null || _gl is null)
                return;

            // Dark background
            _gl.ClearColor(0.10f, 0.10f, 0.12f, 1.0f);
            _gl.Clear(ClearBufferMask.ColorBufferBit);

            _imgui.Update(1f / 60f);

            // Update animation
            _pulsePhase = (float)(_animationTimer.Elapsed.TotalSeconds * 2.0) % (2f * MathF.PI);

            DrawLoadingUI();

            _imgui.Render();

            _window.SwapBuffers();
        }

        /// <summary>
        /// Update progress, status text, and current step.
        /// </summary>
        public void UpdateProgress(float percent, string status, string step = "")
        {
            Progress = percent;
            StatusText = status;
            CurrentStep = step;
        }

        /// <summary>
        /// Close and dispose the window.
        /// </summary>
        public void Close()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _isRunning = false;

            // Dispose ImGui controller first
            if (_imgui is not null)
            {
                _imgui.Dispose();
                _imgui = null;
            }

            // Destroy the ImGui context we created
            var ctx = ImGui.GetCurrentContext();
            if (ctx != IntPtr.Zero)
            {
                ImGui.DestroyContext(ctx);
            }

            // Close and dispose the window
            if (!_window.IsClosing)
            {
                _window.Close();
            }

            _window.Reset();
            _window.Dispose();
        }

        private void OnLoad()
        {
            _gl = GL.GetApi(_window);

            // Apply dark mode and window icon (Windows only)
            if (_window.Native?.Win32 is { } win32)
            {
                EnableDarkMode(win32.Hwnd);
            }

            ImGui.CreateContext();
            _imgui = new ImGuiController(
                _gl,
                _window,
                _window.Size.X,
                _window.Size.Y
            );

            ConfigureStyle();
        }

        private void ConfigureStyle()
        {
            var style = ImGui.GetStyle();
            style.WindowRounding = 0f;
            style.FrameRounding = 6.0f;
            style.WindowPadding = new Vector2(20, 20);
            style.ItemSpacing = new Vector2(8, 8);

            var colors = style.Colors;
            // Dark theme colors
            colors[(int)ImGuiCol.WindowBg] = new Vector4(0.10f, 0.10f, 0.12f, 1.0f);
            colors[(int)ImGuiCol.Text] = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
            colors[(int)ImGuiCol.TextDisabled] = new Vector4(0.5f, 0.5f, 0.5f, 1.0f);
            colors[(int)ImGuiCol.FrameBg] = new Vector4(0.18f, 0.18f, 0.22f, 1.0f);
            colors[(int)ImGuiCol.FrameBgHovered] = new Vector4(0.22f, 0.22f, 0.28f, 1.0f);
            // Progress bar colors - blue accent
            colors[(int)ImGuiCol.PlotHistogram] = new Vector4(0.30f, 0.65f, 1.0f, 1.0f);
        }

        private void DrawLoadingUI()
        {
            var io = ImGui.GetIO();
            var windowSize = io.DisplaySize;

            ImGui.SetNextWindowPos(Vector2.Zero);
            ImGui.SetNextWindowSize(windowSize);

            var flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove |
                        ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings;

            if (ImGui.Begin("LoadingContent", flags))
            {
                float contentWidth = windowSize.X - 60f;
                float startX = 30f;

                // Title with subtle animation
                float titleY = 25f;
                ImGui.SetCursorPos(new Vector2(0, titleY));
                
                string title = "EFT DMA Radar";
                var titleSize = ImGui.CalcTextSize(title);
                ImGui.SetCursorPosX((windowSize.X - titleSize.X) / 2f);
                
                // Pulsing title color
                float pulse = 0.85f + 0.15f * MathF.Sin(_pulsePhase);
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(pulse, pulse, 1.0f, 1.0f));
                ImGui.TextUnformatted(title);
                ImGui.PopStyleColor();

                // Status text - centered below title
                ImGui.SetCursorPosY(titleY + 35f);
                var statusSize = ImGui.CalcTextSize(_statusText);
                ImGui.SetCursorPosX((windowSize.X - statusSize.X) / 2f);
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.7f, 0.7f, 0.7f, 1.0f));
                ImGui.TextUnformatted(_statusText);
                ImGui.PopStyleColor();

                // Progress bar container
                float progressY = titleY + 75f;
                float progressBarHeight = 24f;
                ImGui.SetCursorPos(new Vector2(startX, progressY));

                // Draw progress bar background
                var drawList = ImGui.GetWindowDrawList();
                var progressBarPos = ImGui.GetCursorScreenPos();
                var progressBarEnd = new Vector2(progressBarPos.X + contentWidth, progressBarPos.Y + progressBarHeight);
                
                // Background with rounded corners
                uint bgColor = ImGui.GetColorU32(new Vector4(0.18f, 0.18f, 0.22f, 1.0f));
                drawList.AddRectFilled(progressBarPos, progressBarEnd, bgColor, 6f);
                
                // Progress fill with gradient effect
                float fillWidth = contentWidth * (_progress / 100f);
                if (fillWidth > 0)
                {
                    var fillEnd = new Vector2(progressBarPos.X + fillWidth, progressBarPos.Y + progressBarHeight);
                    
                    // Base progress color (blue)
                    uint progressColor = ImGui.GetColorU32(new Vector4(0.30f, 0.65f, 1.0f, 1.0f));
                    // Lighter highlight at top
                    uint progressHighlight = ImGui.GetColorU32(new Vector4(0.45f, 0.75f, 1.0f, 1.0f));
                    
                    drawList.AddRectFilledMultiColor(
                        progressBarPos, fillEnd,
                        progressHighlight, progressHighlight,
                        progressColor, progressColor);
                    
                    // Add subtle glow effect at the end of progress
                    if (fillWidth > 10)
                    {
                        float glowPulse = 0.3f + 0.2f * MathF.Sin(_pulsePhase * 2f);
                        uint glowColor = ImGui.GetColorU32(new Vector4(0.5f, 0.8f, 1.0f, glowPulse));
                        var glowPos = new Vector2(progressBarPos.X + fillWidth - 5, progressBarPos.Y);
                        var glowEnd = new Vector2(progressBarPos.X + fillWidth, progressBarPos.Y + progressBarHeight);
                        drawList.AddRectFilled(glowPos, glowEnd, glowColor, 3f);
                    }
                }
                
                // Border
                uint borderColor = ImGui.GetColorU32(new Vector4(0.25f, 0.25f, 0.30f, 1.0f));
                drawList.AddRect(progressBarPos, progressBarEnd, borderColor, 6f);

                // Percentage text centered on progress bar
                var percentText = $"{_progress:F0}%";
                var percentSize = ImGui.CalcTextSize(percentText);
                float textX = progressBarPos.X + (contentWidth - percentSize.X) / 2f;
                float textY = progressBarPos.Y + (progressBarHeight - percentSize.Y) / 2f;
                drawList.AddText(new Vector2(textX, textY), ImGui.GetColorU32(ImGuiCol.Text), percentText);

                // Move cursor past progress bar
                ImGui.SetCursorPosY(progressY + progressBarHeight + 15f);

                // Current step text - smaller, muted, centered
                if (!string.IsNullOrEmpty(_currentStep))
                {
                    var stepSize = ImGui.CalcTextSize(_currentStep);
                    ImGui.SetCursorPosX((windowSize.X - stepSize.X) / 2f);
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 0.5f, 0.55f, 1.0f));
                    ImGui.TextUnformatted(_currentStep);
                    ImGui.PopStyleColor();
                }

                // Animated loading dots at the bottom
                float dotsY = windowSize.Y - 30f;
                ImGui.SetCursorPosY(dotsY);
                
                string dots = new string('.', (int)((_animationTimer.Elapsed.TotalSeconds * 2) % 4));
                var dotsSize = ImGui.CalcTextSize("Loading" + dots);
                ImGui.SetCursorPosX((windowSize.X - dotsSize.X) / 2f);
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.4f, 0.4f, 0.45f, 1.0f));
                ImGui.TextUnformatted("Loading" + dots);
                ImGui.PopStyleColor();

                ImGui.End();
            }
        }

        private void OnClosing()
        {
            _isRunning = false;
        }

        #region Win32 Interop

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        [LibraryImport("dwmapi.dll")]
        private static partial int DwmSetWindowAttribute(nint hwnd, int attr, ref int attrValue, int attrSize);

        private static void EnableDarkMode(nint hwnd)
        {
            int useImmersiveDarkMode = 1;
            _ = DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useImmersiveDarkMode, sizeof(int));
        }

        #endregion
    }
}

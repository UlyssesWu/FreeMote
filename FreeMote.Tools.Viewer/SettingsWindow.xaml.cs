using System;
using System.Globalization;
using System.Windows;

namespace FreeMote.Tools.Viewer
{
    public partial class SettingsWindow : Window
    {
        private const int MinScreenshotSize = 100;
        private const int MaxScreenshotSize = 10240;
        private const double DefaultPlaybackSpeed = 1.0;
        private const int DefaultScreenshotWidth = 1920;
        private const int DefaultScreenshotHeight = 1080;

        public event Action<double> PlaybackSpeedPreviewChanged;

        public double PlaybackSpeed { get; private set; }
        public int ScreenshotWidth { get; private set; }
        public int ScreenshotHeight { get; private set; }

        public SettingsWindow(double playbackSpeed, int screenshotWidth, int screenshotHeight)
        {
            InitializeComponent();

            PlaybackSpeed = Clamp(playbackSpeed, 0.05, 3.0);
            ScreenshotWidth = Clamp(screenshotWidth, MinScreenshotSize, MaxScreenshotSize);
            ScreenshotHeight = Clamp(screenshotHeight, MinScreenshotSize, MaxScreenshotSize);

            SpeedSlider.Value = PlaybackSpeed;
            WidthBox.Text = ScreenshotWidth.ToString(CultureInfo.InvariantCulture);
            HeightBox.Text = ScreenshotHeight.ToString(CultureInfo.InvariantCulture);
            UpdateSpeedText();
        }

        private void SpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateSpeedText();
            PlaybackSpeedPreviewChanged?.Invoke(Math.Round(SpeedSlider.Value, 2));
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (!TryReadSize(WidthBox.Text, out var width) || !TryReadSize(HeightBox.Text, out var height))
            {
                MessageBox.Show(this, "Screenshot width and height must be integers between 100 and 10240.", "Invalid Settings",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            PlaybackSpeed = Math.Round(SpeedSlider.Value, 2);
            ScreenshotWidth = width;
            ScreenshotHeight = height;
            DialogResult = true;
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            SpeedSlider.Value = DefaultPlaybackSpeed;
            WidthBox.Text = DefaultScreenshotWidth.ToString(CultureInfo.InvariantCulture);
            HeightBox.Text = DefaultScreenshotHeight.ToString(CultureInfo.InvariantCulture);
            UpdateSpeedText();
            PlaybackSpeedPreviewChanged?.Invoke(DefaultPlaybackSpeed);
        }

        private void UpdateSpeedText()
        {
            if (SpeedValue != null)
            {
                SpeedValue.Text = $"{SpeedSlider.Value:F2}x";
            }
        }

        private static bool TryReadSize(string text, out int value)
        {
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
                   && value >= MinScreenshotSize
                   && value <= MaxScreenshotSize;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            if (value > max)
            {
                return max;
            }

            return value;
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min)
            {
                return min;
            }

            if (value > max)
            {
                return max;
            }

            return value;
        }
    }
}

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
        private const string DefaultCenterPointMode = "default";

        public event Action<double> PlaybackSpeedPreviewChanged;

        public double PlaybackSpeed { get; private set; }
        public int ScreenshotWidth { get; private set; }
        public int ScreenshotHeight { get; private set; }
        public bool KeepScreenshotScale100 { get; private set; }
        public string CenterPointMode { get; private set; }

        public SettingsWindow(double playbackSpeed, int screenshotWidth, int screenshotHeight, bool keepScreenshotScale100, string centerPointMode)
        {
            InitializeComponent();

            PlaybackSpeed = Clamp(playbackSpeed, 0.05, 3.0);
            ScreenshotWidth = Clamp(screenshotWidth, MinScreenshotSize, MaxScreenshotSize);
            ScreenshotHeight = Clamp(screenshotHeight, MinScreenshotSize, MaxScreenshotSize);
            KeepScreenshotScale100 = keepScreenshotScale100;
            CenterPointMode = NormalizeCenterPointMode(centerPointMode);

            SpeedSlider.Value = PlaybackSpeed;
            WidthBox.Text = ScreenshotWidth.ToString(CultureInfo.InvariantCulture);
            HeightBox.Text = ScreenshotHeight.ToString(CultureInfo.InvariantCulture);
            KeepScaleBox.IsChecked = KeepScreenshotScale100;
            SetCenterPointMode(CenterPointMode);
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
            KeepScreenshotScale100 = KeepScaleBox.IsChecked == true;
            CenterPointMode = GetCenterPointMode();
            DialogResult = true;
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            SpeedSlider.Value = DefaultPlaybackSpeed;
            WidthBox.Text = DefaultScreenshotWidth.ToString(CultureInfo.InvariantCulture);
            HeightBox.Text = DefaultScreenshotHeight.ToString(CultureInfo.InvariantCulture);
            KeepScaleBox.IsChecked = false;
            SetCenterPointMode(DefaultCenterPointMode);
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

        private string GetCenterPointMode()
        {
            if (CenterBustButton.IsChecked == true)
            {
                return "bust";
            }

            if (CenterEyeButton.IsChecked == true)
            {
                return "eye";
            }

            if (CenterMouthButton.IsChecked == true)
            {
                return "mouth";
            }

            if (CenterZeroButton.IsChecked == true)
            {
                return "0";
            }

            return DefaultCenterPointMode;
        }

        private void SetCenterPointMode(string mode)
        {
            mode = NormalizeCenterPointMode(mode);
            CenterDefaultButton.IsChecked = mode == DefaultCenterPointMode;
            CenterBustButton.IsChecked = mode == "bust";
            CenterEyeButton.IsChecked = mode == "eye";
            CenterMouthButton.IsChecked = mode == "mouth";
            CenterZeroButton.IsChecked = mode == "0";
        }

        private static string NormalizeCenterPointMode(string mode)
        {
            switch ((mode ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "bust":
                case "eye":
                case "mouth":
                case "0":
                    return mode.Trim().ToLowerInvariant();
                default:
                    return DefaultCenterPointMode;
            }
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

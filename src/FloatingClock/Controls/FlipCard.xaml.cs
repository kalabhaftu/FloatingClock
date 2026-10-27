using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace FloatingClock.Controls;

public partial class FlipCard : UserControl
{
    private string _currentValue = "00";
    private bool _isAnimating;

    public FlipCard()
    {
        InitializeComponent();
    }

    /// <summary>Update the displayed value, triggering a flip animation when it changes.</summary>
    public void SetValue(string newValue)
    {
        if (newValue == _currentValue) return;

        var oldValue = _currentValue;
        _currentValue = newValue;

        if (_isAnimating)
        {
            // Already mid-flip – just snap to new value
            BaseText.Text = newValue;
            return;
        }

        FlipToNewValue(oldValue, newValue);
    }

    // ───────── animation ─────────

    private void FlipToNewValue(string oldValue, string newValue)
    {
        _isAnimating = true;

        // Set layer texts
        BaseText.Text       = newValue;   // visible behind everything
        TopFlapText.Text    = oldValue;   // top half – old
        OldBottomText.Text  = oldValue;   // bottom half – old (static)
        BottomFlapText.Text = newValue;   // bottom half – new (unfolds)

        // Reset transforms
        TopFlapScale.ScaleY    = 1;
        BottomFlapScale.ScaleY = 0;

        // Update clip regions to current size
        UpdateClipRegions();

        // Show overlay layers
        TopFlap.Visibility        = Visibility.Visible;
        OldBottomPanel.Visibility = Visibility.Visible;
        BottomFlap.Visibility     = Visibility.Visible;

        // Phase 1 – top flap folds down (200 ms, ease-in)
        var topAnim = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };

        // Phase 2 – bottom flap unfolds (200 ms, ease-out, starts at 150 ms)
        var bottomAnim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200))
        {
            BeginTime      = TimeSpan.FromMilliseconds(150),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        bottomAnim.Completed += (_, _) =>
        {
            TopFlap.Visibility        = Visibility.Collapsed;
            OldBottomPanel.Visibility = Visibility.Collapsed;
            BottomFlap.Visibility     = Visibility.Collapsed;
            TopFlapScale.ScaleY       = 1;
            BottomFlapScale.ScaleY    = 1;
            _isAnimating              = false;
        };

        TopFlapScale.BeginAnimation(ScaleTransform.ScaleYProperty, topAnim);
        BottomFlapScale.BeginAnimation(ScaleTransform.ScaleYProperty, bottomAnim);
    }

    // ───────── clipping ─────────

    private void UpdateClipRegions()
    {
        double w = CardGrid.ActualWidth;
        double h = CardGrid.ActualHeight;
        if (w <= 0 || h <= 0) return;

        double half = h / 2;

        TopFlap.Clip        = new RectangleGeometry(new Rect(0, 0,    w, half));
        OldBottomPanel.Clip = new RectangleGeometry(new Rect(0, half, w, half));
        BottomFlap.Clip     = new RectangleGeometry(new Rect(0, half, w, half));
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => UpdateClipRegions();
}

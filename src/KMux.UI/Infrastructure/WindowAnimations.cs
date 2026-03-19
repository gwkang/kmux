using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace KMux.UI.Infrastructure;

internal static class WindowAnimations
{
    /// <summary>Plays a 150ms fade-in + scale-up entry animation on a modal window's root content.</summary>
    internal static void PlayEntry(FrameworkElement content, ScaleTransform scale)
    {
        var dur  = new Duration(TimeSpan.FromMilliseconds(150));
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var sb   = new Storyboard();

        var fadeIn = new DoubleAnimation(0, 1, dur) { EasingFunction = ease };
        Storyboard.SetTarget(fadeIn, content);
        Storyboard.SetTargetProperty(fadeIn, new PropertyPath(UIElement.OpacityProperty));

        var scaleX = new DoubleAnimation(0.96, 1.0, dur) { EasingFunction = ease };
        Storyboard.SetTarget(scaleX, scale);
        Storyboard.SetTargetProperty(scaleX, new PropertyPath(ScaleTransform.ScaleXProperty));

        var scaleY = new DoubleAnimation(0.96, 1.0, dur) { EasingFunction = ease };
        Storyboard.SetTarget(scaleY, scale);
        Storyboard.SetTargetProperty(scaleY, new PropertyPath(ScaleTransform.ScaleYProperty));

        sb.Children.Add(fadeIn);
        sb.Children.Add(scaleX);
        sb.Children.Add(scaleY);
        sb.Begin();
    }
}

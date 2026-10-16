using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using System;
using System.Numerics;

namespace PasswordVault.App.Helpers;

/// <summary>
/// Composition-Animationen (laufen auf dem Compositor-Thread, also flüssig
/// auch wenn der UI-Thread beschäftigt ist) plus Attached Properties für XAML.
/// </summary>
public static class Motion
{
    private static readonly Windows.UI.ViewManagement.UISettings SystemUi = new();

    public static bool Enabled =>
        (App.Settings?.Current.AnimationsEnabled ?? true) && SystemUi.AnimationsEnabled;

    private static CompositionEasingFunction EaseOut(Compositor c)
        => c.CreateCubicBezierEasingFunction(new Vector2(0.16f, 1f), new Vector2(0.3f, 1f));

    private static Visual Prepare(UIElement element)
    {
        ElementCompositionPreview.SetIsTranslationEnabled(element, true);
        var visual = ElementCompositionPreview.GetElementVisual(element);
        var size = element.ActualSize;
        visual.CenterPoint = new Vector3(size.X / 2, size.Y / 2, 0);
        return visual;
    }

    /// <summary>Einblenden mit leichtem Hochgleiten.</summary>
    public static void FadeInUp(UIElement element, double delayMs = 0, float distance = 18, double durationMs = 520)
    {
        if (!Enabled) return;
        var visual = Prepare(element);
        var c = visual.Compositor;

        var fade = c.CreateScalarKeyFrameAnimation();
        fade.InsertKeyFrame(0f, 0f);
        fade.InsertKeyFrame(1f, 1f, EaseOut(c));
        fade.Duration = TimeSpan.FromMilliseconds(durationMs);
        fade.DelayTime = TimeSpan.FromMilliseconds(delayMs);
        fade.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

        var slide = c.CreateVector3KeyFrameAnimation();
        slide.InsertKeyFrame(0f, new Vector3(0, distance, 0));
        slide.InsertKeyFrame(1f, Vector3.Zero, EaseOut(c));
        slide.Duration = fade.Duration;
        slide.DelayTime = fade.DelayTime;
        slide.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

        visual.StartAnimation("Opacity", fade);
        visual.StartAnimation("Translation", slide);
    }

    /// <summary>Einblenden von der Seite.</summary>
    public static void FadeInX(UIElement element, double delayMs = 0, float distance = 24, double durationMs = 480)
    {
        if (!Enabled) return;
        var visual = Prepare(element);
        var c = visual.Compositor;

        var fade = c.CreateScalarKeyFrameAnimation();
        fade.InsertKeyFrame(0f, 0f);
        fade.InsertKeyFrame(1f, 1f, EaseOut(c));
        fade.Duration = TimeSpan.FromMilliseconds(durationMs);
        fade.DelayTime = TimeSpan.FromMilliseconds(delayMs);
        fade.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

        var slide = c.CreateVector3KeyFrameAnimation();
        slide.InsertKeyFrame(0f, new Vector3(distance, 0, 0));
        slide.InsertKeyFrame(1f, Vector3.Zero, EaseOut(c));
        slide.Duration = fade.Duration;
        slide.DelayTime = fade.DelayTime;
        slide.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

        visual.StartAnimation("Opacity", fade);
        visual.StartAnimation("Translation", slide);
    }

    /// <summary>Aufploppen mit Feder-Effekt.</summary>
    public static void PopIn(UIElement element, double delayMs = 0, float fromScale = 0.6f)
    {
        if (!Enabled) return;
        var visual = Prepare(element);
        var c = visual.Compositor;

        var scale = c.CreateSpringVector3Animation();
        scale.InitialValue = new Vector3(fromScale, fromScale, 1);
        scale.FinalValue = Vector3.One;
        scale.DampingRatio = 0.55f;
        scale.Period = TimeSpan.FromMilliseconds(55);
        scale.DelayTime = TimeSpan.FromMilliseconds(delayMs);
        scale.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

        var fade = c.CreateScalarKeyFrameAnimation();
        fade.InsertKeyFrame(0f, 0f);
        fade.InsertKeyFrame(1f, 1f);
        fade.Duration = TimeSpan.FromMilliseconds(220);
        fade.DelayTime = scale.DelayTime;
        fade.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

        visual.StartAnimation("Scale", scale);
        visual.StartAnimation("Opacity", fade);
    }

    /// <summary>Kurzer "Herzschlag" (z.B. nach Kopieren oder Favorisieren).</summary>
    public static void Pulse(UIElement element, float peak = 1.25f)
    {
        if (!Enabled) return;
        var visual = Prepare(element);
        var c = visual.Compositor;
        var anim = c.CreateVector3KeyFrameAnimation();
        anim.InsertKeyFrame(0f, Vector3.One);
        anim.InsertKeyFrame(0.35f, new Vector3(peak, peak, 1), EaseOut(c));
        anim.InsertKeyFrame(1f, Vector3.One, EaseOut(c));
        anim.Duration = TimeSpan.FromMilliseconds(420);
        visual.StartAnimation("Scale", anim);
    }

    /// <summary>Kopfschütteln für Fehler.</summary>
    public static void Shake(UIElement element)
    {
        if (!Enabled) return;
        var visual = Prepare(element);
        var c = visual.Compositor;
        var anim = c.CreateVector3KeyFrameAnimation();
        float[] offsets = { 0, -14, 12, -10, 8, -5, 3, 0 };
        for (int i = 0; i < offsets.Length; i++)
            anim.InsertKeyFrame(i / (float)(offsets.Length - 1), new Vector3(offsets[i], 0, 0));
        anim.Duration = TimeSpan.FromMilliseconds(480);
        visual.StartAnimation("Translation", anim);
    }

    /// <summary>Eine volle Umdrehung (z.B. "Neu generieren"-Icon).</summary>
    public static void Spin(UIElement element, double durationMs = 600)
    {
        if (!Enabled) return;
        var visual = Prepare(element);
        var c = visual.Compositor;
        var anim = c.CreateScalarKeyFrameAnimation();
        anim.InsertKeyFrame(0f, 0f);
        anim.InsertKeyFrame(1f, 360f, EaseOut(c));
        anim.Duration = TimeSpan.FromMilliseconds(durationMs);
        visual.StartAnimation("RotationAngleInDegrees", anim);
    }

    /// <summary>Animiert die horizontale Skalierung (z.B. Stärke-Balken) von links aus.</summary>
    public static void ScaleXTo(UIElement element, float to, double durationMs = 600)
    {
        var visual = ElementCompositionPreview.GetElementVisual(element);
        visual.CenterPoint = Vector3.Zero;
        if (!Enabled)
        {
            visual.Scale = new Vector3(to, 1, 1);
            return;
        }
        var c = visual.Compositor;
        var anim = c.CreateVector3KeyFrameAnimation();
        anim.InsertKeyFrame(1f, new Vector3(Math.Max(to, 0.0001f), 1, 1), EaseOut(c));
        anim.Duration = TimeSpan.FromMilliseconds(durationMs);
        visual.StartAnimation("Scale", anim);
    }

    /// <summary>Linearer Countdown-Balken (Zwischenablage).</summary>
    public static void Countdown(UIElement element, TimeSpan duration)
    {
        var visual = ElementCompositionPreview.GetElementVisual(element);
        visual.CenterPoint = Vector3.Zero;
        var c = visual.Compositor;
        var anim = c.CreateVector3KeyFrameAnimation();
        anim.InsertKeyFrame(0f, Vector3.One);
        anim.InsertKeyFrame(1f, new Vector3(0.0001f, 1, 1), c.CreateLinearEasingFunction());
        anim.Duration = duration;
        visual.StartAnimation("Scale", anim);
    }

    /// <summary>Endlos schwebende Bewegung (Hintergrund-Blobs, Illustrationen).</summary>
    public static void Float(UIElement element, Vector3 amplitude, double periodMs, double delayMs = 0)
    {
        if (!Enabled) return;
        ElementCompositionPreview.SetIsTranslationEnabled(element, true);
        var visual = ElementCompositionPreview.GetElementVisual(element);
        var c = visual.Compositor;
        var ease = c.CreateCubicBezierEasingFunction(new Vector2(0.45f, 0f), new Vector2(0.55f, 1f));
        var anim = c.CreateVector3KeyFrameAnimation();
        anim.InsertKeyFrame(0f, Vector3.Zero);
        anim.InsertKeyFrame(0.5f, amplitude, ease);
        anim.InsertKeyFrame(1f, Vector3.Zero, ease);
        anim.Duration = TimeSpan.FromMilliseconds(periodMs);
        anim.DelayTime = TimeSpan.FromMilliseconds(delayMs);
        anim.IterationBehavior = AnimationIterationBehavior.Forever;
        visual.StartAnimation("Translation", anim);
    }

    /// <summary>Endlos pulsierender Ring (Skalierung + Ausblenden).</summary>
    public static void Ripple(UIElement element, double periodMs, double delayMs = 0, float toScale = 1.7f)
    {
        if (!Enabled)
        {
            element.Opacity = 0;
            return;
        }
        var visual = Prepare(element);
        var c = visual.Compositor;

        var scale = c.CreateVector3KeyFrameAnimation();
        scale.InsertKeyFrame(0f, Vector3.One);
        scale.InsertKeyFrame(1f, new Vector3(toScale, toScale, 1), EaseOut(c));
        scale.Duration = TimeSpan.FromMilliseconds(periodMs);
        scale.DelayTime = TimeSpan.FromMilliseconds(delayMs);
        scale.IterationBehavior = AnimationIterationBehavior.Forever;

        var fade = c.CreateScalarKeyFrameAnimation();
        fade.InsertKeyFrame(0f, 0.7f);
        fade.InsertKeyFrame(1f, 0f, EaseOut(c));
        fade.Duration = scale.Duration;
        fade.DelayTime = scale.DelayTime;
        fade.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
        fade.IterationBehavior = AnimationIterationBehavior.Forever;

        visual.StartAnimation("Scale", scale);
        visual.StartAnimation("Opacity", fade);
    }

    /// <summary>Kinder eines Panels nacheinander einblenden.</summary>
    public static void Stagger(Panel panel, double stepMs = 45, double initialDelayMs = 0)
    {
        double delay = initialDelayMs;
        foreach (var child in panel.Children)
        {
            if (child.Visibility != Visibility.Visible) continue;
            FadeInUp(child, delay);
            delay += stepMs;
        }
    }

    // ===== Attached Property: HoverLift (Karte hebt sich bei Hover leicht an) =====

    public static readonly DependencyProperty HoverLiftProperty =
        DependencyProperty.RegisterAttached("HoverLift", typeof(bool), typeof(Motion),
            new PropertyMetadata(false, OnHoverLiftChanged));

    public static bool GetHoverLift(DependencyObject obj) => (bool)obj.GetValue(HoverLiftProperty);
    public static void SetHoverLift(DependencyObject obj, bool value) => obj.SetValue(HoverLiftProperty, value);

    private static void OnHoverLiftChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element || e.NewValue is not true) return;
        element.PointerEntered += (_, _) => Lift(element, -3, 1.015f);
        element.PointerExited += (_, _) => Lift(element, 0, 1f);
        element.PointerCanceled += (_, _) => Lift(element, 0, 1f);
    }

    private static void Lift(UIElement element, float y, float scale)
    {
        if (!Enabled) return;
        var visual = Prepare(element);
        var c = visual.Compositor;

        var t = c.CreateSpringVector3Animation();
        t.FinalValue = new Vector3(0, y, 0);
        t.DampingRatio = 0.7f;
        t.Period = TimeSpan.FromMilliseconds(40);
        visual.StartAnimation("Translation", t);

        var s = c.CreateSpringVector3Animation();
        s.FinalValue = new Vector3(scale, scale, 1);
        s.DampingRatio = 0.7f;
        s.Period = TimeSpan.FromMilliseconds(40);
        visual.StartAnimation("Scale", s);
    }

    // ===== Attached Property: HoverReveal (Ziel-Element erscheint bei Hover) =====

    public static readonly DependencyProperty HoverRevealTargetProperty =
        DependencyProperty.RegisterAttached("HoverRevealTarget", typeof(UIElement), typeof(Motion),
            new PropertyMetadata(null, OnHoverRevealChanged));

    public static UIElement? GetHoverRevealTarget(DependencyObject obj) => (UIElement?)obj.GetValue(HoverRevealTargetProperty);
    public static void SetHoverRevealTarget(DependencyObject obj, UIElement? value) => obj.SetValue(HoverRevealTargetProperty, value);

    private static void OnHoverRevealChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement host || e.NewValue is not UIElement target) return;
        target.Opacity = 0.0;
        target.IsHitTestVisible = false;
        host.PointerEntered += (_, _) => Reveal(target, true);
        host.PointerExited += (_, _) => Reveal(target, false);
        host.PointerCanceled += (_, _) => Reveal(target, false);
        host.GotFocus += (_, _) => Reveal(target, true);
    }

    private static void Reveal(UIElement target, bool show)
    {
        // Unsichtbare Buttons dürfen keine Klicks abfangen
        target.IsHitTestVisible = show;
        if (!Enabled)
        {
            target.Opacity = show ? 1 : 0;
            return;
        }
        target.Opacity = 1;
        var visual = Prepare(target);
        var c = visual.Compositor;
        var fade = c.CreateScalarKeyFrameAnimation();
        fade.InsertKeyFrame(1f, show ? 1f : 0f, EaseOut(c));
        fade.Duration = TimeSpan.FromMilliseconds(200);
        visual.StartAnimation("Opacity", fade);

        var slide = c.CreateVector3KeyFrameAnimation();
        slide.InsertKeyFrame(1f, new Vector3(show ? 0 : 10, 0, 0), EaseOut(c));
        slide.Duration = fade.Duration;
        visual.StartAnimation("Translation", slide);
    }

    // ===== Attached Property: Entrance (beim Laden einblenden) =====

    public static readonly DependencyProperty EntranceDelayProperty =
        DependencyProperty.RegisterAttached("EntranceDelay", typeof(int), typeof(Motion),
            new PropertyMetadata(-1, OnEntranceChanged));

    public static int GetEntranceDelay(DependencyObject obj) => (int)obj.GetValue(EntranceDelayProperty);
    public static void SetEntranceDelay(DependencyObject obj, int value) => obj.SetValue(EntranceDelayProperty, value);

    private static void OnEntranceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element || e.NewValue is not int delay || delay < 0) return;
        element.Loaded += (_, _) => FadeInUp(element, delay);
    }
}

// SPDX-FileCopyrightText: 2026 Diogo Losacco Toporcov
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Chess.Desktop.Views.Components;

public partial class EvaluationBarView
{
    public static readonly DependencyProperty WhiteFractionProperty =
        DependencyProperty.Register(
            nameof(WhiteFraction),
            typeof(double),
            typeof(EvaluationBarView),
            new PropertyMetadata(0.5, OnWhiteFractionChanged));

    public static readonly DependencyProperty EvaluationTextProperty =
        DependencyProperty.Register(
            nameof(EvaluationText),
            typeof(string),
            typeof(EvaluationBarView),
            new PropertyMetadata("—"));

    public static readonly DependencyProperty IsFlippedProperty =
        DependencyProperty.Register(
            nameof(IsFlipped),
            typeof(bool),
            typeof(EvaluationBarView),
            new PropertyMetadata(false, OnIsFlippedChanged));

    public static readonly DependencyProperty IsPendingProperty =
        DependencyProperty.Register(
            nameof(IsPending),
            typeof(bool),
            typeof(EvaluationBarView),
            new PropertyMetadata(false));

    public static readonly DependencyProperty IsAvailableProperty =
        DependencyProperty.Register(
            nameof(IsAvailable),
            typeof(bool),
            typeof(EvaluationBarView),
            new PropertyMetadata(false));

    public double WhiteFraction
    {
        get => (double)GetValue(WhiteFractionProperty);
        set => SetValue(WhiteFractionProperty, value);
    }

    public string EvaluationText
    {
        get => (string)GetValue(EvaluationTextProperty);
        set => SetValue(EvaluationTextProperty, value);
    }

    public bool IsFlipped
    {
        get => (bool)GetValue(IsFlippedProperty);
        set => SetValue(IsFlippedProperty, value);
    }

    public bool IsPending
    {
        get => (bool)GetValue(IsPendingProperty);
        set => SetValue(IsPendingProperty, value);
    }

    public bool IsAvailable
    {
        get => (bool)GetValue(IsAvailableProperty);
        set => SetValue(IsAvailableProperty, value);
    }

    public EvaluationBarView()
    {
        InitializeComponent();
    }

    private static void OnWhiteFractionChanged(
        DependencyObject sender,
        DependencyPropertyChangedEventArgs args)
    {
        var view = (EvaluationBarView)sender;
        if (view.WhiteScale is null)
        {
            return;
        }

        var animation = new DoubleAnimation
        {
            From = view.WhiteScale.ScaleY,
            To = Math.Clamp((double)args.NewValue, 0, 1),
            Duration = TimeSpan.FromMilliseconds(225),
            EasingFunction = new QuadraticEase
            {
                EasingMode = EasingMode.EaseOut
            }
        };
        view.WhiteScale.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            animation);
    }

    private static void OnIsFlippedChanged(
        DependencyObject sender,
        DependencyPropertyChangedEventArgs args)
    {
        var view = (EvaluationBarView)sender;
        if (view.WhiteSegment is not null)
        {
            view.WhiteSegment.RenderTransformOrigin = (bool)args.NewValue
                ? new Point(0.5, 0)
                : new Point(0.5, 1);
        }
    }
}

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PasswordVault.App.Helpers;
using PasswordVault.Core.Models;
using PasswordVault.Core.Services;
using System;
using System.Collections.ObjectModel;
using System.Security.Cryptography;

namespace PasswordVault.App.Views;

public sealed partial class GeneratorPage : Page
{
    // Sitzungsverlauf – lebt nur bis zum Sperren
    private static readonly ObservableCollection<string> History = new();

    private readonly PasswordGenerator _generator = new();
    private readonly DispatcherQueueTimer _scrambleTimer;
    private string _password = "";
    private int _scrambleStep;
    private bool _initializing = true;

    static GeneratorPage()
    {
        App.Store.Locked += (_, _) => App.MainWindowInstance.DispatcherQueue.TryEnqueue(History.Clear);
    }

    public GeneratorPage()
    {
        InitializeComponent();
        HistoryList.ItemsSource = History;

        var s = App.Settings.Current;
        LengthSlider.Value = s.DefaultPasswordLength;
        UpperToggle.IsOn = s.UseUppercase;
        LowerToggle.IsOn = s.UseLowercase;
        DigitToggle.IsOn = s.UseDigits;
        SymbolToggle.IsOn = s.UseSymbols;
        AmbiguousToggle.IsOn = s.ExcludeAmbiguous;
        LengthValue.Text = s.DefaultPasswordLength.ToString();

        _scrambleTimer = DispatcherQueue.CreateTimer();
        _scrambleTimer.Interval = TimeSpan.FromMilliseconds(28);
        _scrambleTimer.Tick += Scramble_Tick;

        _initializing = false;
        Loaded += (_, _) =>
        {
            Motion.Stagger(Root, 70);
            Motion.FadeInUp(OptionsCard, 200);
            Motion.FadeInUp(HistoryCard, 280);
            Generate();
        };
    }

    private void Generate()
    {
        bool upper = UpperToggle.IsOn, lower = LowerToggle.IsOn, digits = DigitToggle.IsOn, symbols = SymbolToggle.IsOn;
        if (!upper && !lower && !digits && !symbols)
        {
            _initializing = true;
            LowerToggle.IsOn = lower = true;
            _initializing = false;
        }

        _password = _generator.Generate((int)LengthSlider.Value, lower, upper, digits, symbols, AmbiguousToggle.IsOn);

        var strength = PasswordStrength.Evaluate(_password);
        StrengthLabel.Text = strength.Label;
        EntropyLabel.Text = $"≈ {strength.EntropyBits:0} Bit · Knackzeit: {PasswordStrength.EstimateCrackTime(strength.EntropyBits)}";
        StrengthFill.Background = Ui.StrengthBrush(strength.Level);
        Motion.ScaleXTo(StrengthFill, Math.Max(strength.Score, 3) / 100f, 700);

        // "Slot-Machine"-Effekt: Zeichen rasten nacheinander ein
        if (Motion.Enabled)
        {
            _scrambleStep = 0;
            _scrambleTimer.Start();
        }
        else
        {
            Ui.SetColoredPassword(PasswordText, _password);
        }
    }

    private void Scramble_Tick(DispatcherQueueTimer sender, object args)
    {
        const string noise = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%&*?";
        _scrambleStep += Math.Max(1, _password.Length / 12);
        if (_scrambleStep >= _password.Length)
        {
            _scrambleTimer.Stop();
            Ui.SetColoredPassword(PasswordText, _password);
            return;
        }

        var chars = _password.ToCharArray();
        for (int i = _scrambleStep; i < chars.Length; i++)
            chars[i] = noise[RandomNumberGenerator.GetInt32(noise.Length)];
        PasswordText.Inlines.Clear();
        PasswordText.Text = new string(chars);
    }

    private void Option_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializing || LengthValue == null) return;
        LengthValue.Text = ((int)LengthSlider.Value).ToString();
        Generate();
    }

    private void Regenerate_Click(object sender, RoutedEventArgs e)
    {
        Motion.Spin(RegenerateIcon);
        Generate();
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        ClipboardHelper.CopySecret(_password);
        Motion.Pulse(CopyButton, 1.06f);
        AddToHistory(_password);
    }

    private void AddToHistory(string password)
    {
        History.Remove(password);
        History.Insert(0, password);
        while (History.Count > 10) History.RemoveAt(History.Count - 1);
    }

    private void CopyHistory_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is string pw)
        {
            ClipboardHelper.CopySecret(pw);
            Motion.Pulse((UIElement)sender);
        }
    }

    private async void SaveAsEntry_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new EntryEditorDialog(new PasswordEntry
        {
            Password = _password,
            Category = Categories.Logins
        }, isNew: true);

        if (await App.MainWindowInstance.ShowDialogAsync(dialog) != ContentDialogResult.Primary
            || dialog.Result == null)
            return;

        App.Store.Add(dialog.Result);
        AddToHistory(_password);
        App.MainWindowInstance.ShowToast($"„{dialog.Result.Title}“ wurde gespeichert", "");
    }

    private void SaveDefaults_Click(object sender, RoutedEventArgs e)
    {
        var s = App.Settings.Current;
        s.DefaultPasswordLength = (int)LengthSlider.Value;
        s.UseUppercase = UpperToggle.IsOn;
        s.UseLowercase = LowerToggle.IsOn;
        s.UseDigits = DigitToggle.IsOn;
        s.UseSymbols = SymbolToggle.IsOn;
        s.ExcludeAmbiguous = AmbiguousToggle.IsOn;
        App.Settings.Save();
        App.MainWindowInstance.ShowToast("Generator-Einstellungen gespeichert", "");
    }
}

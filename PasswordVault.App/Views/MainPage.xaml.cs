using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using PasswordVault.Core.Models;
using PasswordVault.Core.Services;
using System;
using System.Linq;

namespace PasswordVault.App.Views;

/// <summary>
/// Parameter für die VaultPage: Filter und optional ein vorzuwählender Eintrag.
/// </summary>
public sealed record VaultNavigation(string Filter, string? SelectId = null, bool StartNew = false);

public sealed partial class MainPage : Page
{
    private string _currentTag = "all";

    public MainPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += (_, e) =>
        {
            // Unter 1000 px nur Symbole, darüber die volle Seitenleiste
            var mode = e.NewSize.Width < 1000
                ? NavigationViewPaneDisplayMode.LeftCompact
                : NavigationViewPaneDisplayMode.Left;
            if (NavView.PaneDisplayMode != mode) NavView.PaneDisplayMode = mode;
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        App.Store.Changed += Store_Changed;
        RefreshCounts();
        if (ContentFrame.Content == null)
            ContentFrame.Navigate(typeof(VaultPage), new VaultNavigation("all"),
                new SuppressNavigationTransitionInfo());
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        App.Store.Changed -= Store_Changed;
    }

    private void Store_Changed(object? sender, VaultChangedEventArgs e)
        => DispatcherQueue.TryEnqueue(RefreshCounts);

    private void RefreshCounts()
    {
        if (!App.Store.IsUnlocked) return;
        var entries = App.Store.GetEntries();
        CountAll.Text = entries.Count.ToString();
        CountFavorites.Text = entries.Count(e => e.IsFavorite).ToString();
        CountLogins.Text = entries.Count(e => e.Category == Categories.Logins).ToString();
        CountCards.Text = entries.Count(e => e.Category == Categories.Cards).ToString();
        CountNotes.Text = entries.Count(e => e.Category == Categories.Notes).ToString();
        CountGeneral.Text = entries.Count(e => e.Category == Categories.General).ToString();

        var report = SecurityAnalyzer.Analyze(entries);
        var issues = report.Weak.Union(report.Reused).Count();
        SecurityBadge.Value = issues;
        SecurityBadge.Visibility = issues > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    // ================= Navigation =================

    private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if ((args.InvokedItemContainer as NavigationViewItem)?.Tag as string == "lock")
            App.Store.Lock();
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer?.Tag is not string tag || tag == "lock") return;
        NavigateTo(tag, args.RecommendedNavigationTransitionInfo);
    }

    private void NavigateTo(string tag, NavigationTransitionInfo? transition = null, VaultNavigation? vaultArgs = null)
    {
        var previous = _currentTag;
        _currentTag = tag;
        transition ??= new EntranceNavigationTransitionInfo();

        switch (tag)
        {
            case "security":
                ContentFrame.Navigate(typeof(SecurityPage), null, transition);
                break;
            case "generator":
                ContentFrame.Navigate(typeof(GeneratorPage), null, transition);
                break;
            case "settings":
                ContentFrame.Navigate(typeof(SettingsPage), null, transition);
                break;
            default:
                // Innerhalb der Liste nur filtern (mit Listen-Animation), nicht neu navigieren
                if (ContentFrame.Content is VaultPage vault && vaultArgs == null)
                {
                    vault.SetFilter(tag);
                }
                else
                {
                    ContentFrame.Navigate(typeof(VaultPage), vaultArgs ?? new VaultNavigation(tag),
                        IsVaultTag(previous) ? new SuppressNavigationTransitionInfo() : transition);
                }
                break;
        }
        ContentFrame.BackStack.Clear();
    }

    private static bool IsVaultTag(string tag)
        => tag is "all" or "favorites" or "logins" or "cards" or "notes" or "general";

    private void SelectNavItem(string tag)
    {
        var item = NavView.MenuItems.Concat(NavView.FooterMenuItems)
            .OfType<NavigationViewItem>()
            .FirstOrDefault(i => i.Tag as string == tag);
        if (item != null && !ReferenceEquals(NavView.SelectedItem, item))
            NavView.SelectedItem = item; // löst SelectionChanged aus
    }

    /// <summary>Öffnet die Liste und wählt einen Eintrag aus (z.B. vom Sicherheitscheck).</summary>
    public void ShowEntry(string id)
    {
        _currentTag = "all";
        var item = NavView.MenuItems.OfType<NavigationViewItem>().First(i => i.Tag as string == "all");
        NavView.SelectionChanged -= NavView_SelectionChanged;
        NavView.SelectedItem = item;
        NavView.SelectionChanged += NavView_SelectionChanged;
        NavigateTo("all", new DrillInNavigationTransitionInfo(), new VaultNavigation("all", id));
    }

    public void StartNewEntry()
    {
        if (ContentFrame.Content is VaultPage vault)
        {
            vault.StartNewEntry();
            return;
        }
        NavView.SelectionChanged -= NavView_SelectionChanged;
        NavView.SelectedItem = NavView.MenuItems.OfType<NavigationViewItem>().First(i => i.Tag as string == "all");
        NavView.SelectionChanged += NavView_SelectionChanged;
        NavigateTo("all", null, new VaultNavigation("all", null, StartNew: true));
    }

    // ================= Tastenkürzel =================

    private void NewEntry_Accelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        StartNewEntry();
    }

    private void Search_Accelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        if (ContentFrame.Content is not VaultPage)
            SelectNavItem("all");
        DispatcherQueue.TryEnqueue(() => (ContentFrame.Content as VaultPage)?.FocusSearch());
    }

    private void Lock_Accelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        App.Store.Lock();
    }

    private void Generator_Accelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        SelectNavItem("generator");
    }
}

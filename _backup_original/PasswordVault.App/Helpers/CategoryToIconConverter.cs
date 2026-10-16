using Microsoft.UI.Xaml.Data;
using System;

namespace PasswordVault.App.Helpers;

public class CategoryToIconConverter : IValueConverter
{
    public object Convert(object value,
        Type targetType, object parameter,
        string language)
    {
        return value?.ToString() switch
        {
            "Logins" => "\uE77B",      // Contact
            "Kreditkarten" => "\uE8C7", // Shop
            "Notizen" => "\uE70B",      // Page
            _ => "\uE72E"               // Lock
        };
    }

    public object ConvertBack(object value,
        Type targetType, object parameter,
        string language)
    {
        throw new NotImplementedException();
    }
}

namespace PasswordVault.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("Inter-Regular.ttf", Ui.Font);
                fonts.AddFont("Inter-SemiBold.ttf", Ui.FontSemiBold);
                fonts.AddFont("Inter-Bold.ttf", Ui.FontBold);
            });
        return builder.Build();
    }
}

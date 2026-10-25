namespace GPT9000_WinForms_TestSystem;

internal static class AppBranding
{
    public static void ApplyIcon(Form form)
    {
        using var stream = typeof(AppBranding).Assembly.GetManifestResourceStream("Branding.App.ico")!;
        var icon = new Icon(stream, new Size(256, 256));
        form.Icon = icon;
        form.Disposed += (_, _) => icon.Dispose();
    }
}

namespace GPT9000_WinForms_TestSystem;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        System.Globalization.CultureInfo.DefaultThreadCurrentUICulture =
            System.Globalization.CultureInfo.GetCultureInfo("vi-VN");
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

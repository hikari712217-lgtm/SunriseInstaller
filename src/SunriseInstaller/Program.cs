namespace Sunrise.Installer;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (!AppOptions.TryParse(args, out AppOptions options, out string? error))
        {
            MessageBox.Show(error, "Sunrise Installer", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        Application.Run(new MainForm(options));
    }
}

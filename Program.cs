namespace Aye;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var instance = new Mutex(initiallyOwned: true, "Aye.SingleInstance.50505a61c51b498f8486a7d5d181165c", out var isFirstInstance);
        if (!isFirstInstance)
            return;

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayAppContext());
    }
}

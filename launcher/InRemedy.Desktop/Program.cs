namespace InRemedy.Desktop;

static class Program
{
    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, "InRemedy.Desktop.Singleton", out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                "In-Remedy is already running.",
                "In-Remedy",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new Form1());
    }
}

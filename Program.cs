using System;
using Velopack;

namespace JiraTimeTracker;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Velopack MUST be the very first thing called — handles install/uninstall hooks
        VelopackApp.Build().Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}

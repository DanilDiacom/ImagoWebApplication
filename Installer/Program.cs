using Squirrel;
using System;
using System.Threading.Tasks;

class Program {
    static async Task Main(string[] args) {
        Console.WriteLine("Сборка установщика...");
        await CreateInstaller();
    }

    static async Task CreateInstaller() {
        using (var mgr = new UpdateManager("C:\\Users\\User\\Desktop\\SquirrelReleases")) {
            await mgr.Releasify("bin\\Release\\net6.0-windows\\win-x64\\publish\\ImagoAdmin.exe");
        }
    }
}

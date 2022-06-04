using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

namespace MegaSync
{
    public class Program
    {
        public static Task<int> Main(string[] args) => CommandLineApplication.ExecuteAsync<Program>(args);
        

        [Argument(0)]
        public string Email { get; set; }

        [Option("-o|--output", CommandOptionType.SingleValue)]
        public string OutputDirectory { get; } = Constants.DefaultOutputDirectory;

        [Option("-u|--unzip", CommandOptionType.NoValue)]
        public bool Unzip { get; }

        private async Task OnExecuteAsync()
        {
            while (string.IsNullOrWhiteSpace(Email))
            {
                Email = Prompt.GetString("Email: ");
            }

            var password = Prompt.GetPassword("Password:");

            var megaService = new MegaService();

            await megaService.SyncUserData(Email, password, OutputDirectory, Unzip);
        }
    }
}

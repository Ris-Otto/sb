using System.CommandLine;
using System.Threading.Tasks;

namespace sb;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand();
        
        #region Options
        #endregion

        #region Commands
        #endregion
        

        #region Handlers
        #endregion
        
        
        return await rootCommand.InvokeAsync(args);
    }
}
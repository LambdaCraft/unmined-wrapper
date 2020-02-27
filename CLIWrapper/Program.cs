using System;
using System.IO;
using Serilog;

namespace CLIWrapper
{
    class Program
    {
        public static ILogger Logger = Log.ForContext("SourceContext", (object) "Web", false);

        static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateLogger();


            if (args.Length < 3)
            {
                Logger.Error("Usage: CLIWrapper <world path> <output dir> <world name> <force re-gen>");
                return;
            }

            var worldPath = args[0];
            var outPath = args[1];
            var worldName = args[2];

            var force = false;
            
            if (4 == args.Length)
            {
                bool.TryParse(args[3], out force);
            }
            

            var ow = new WebGenerator(worldPath, Path.Combine(outPath, worldName, "overworld"), worldName + " overworld", force);
            var nether = new WebGenerator(Path.Combine(worldPath, "DIM-1"), Path.Combine(outPath, worldName, "the_nether"), worldName + " the_nether", force, true);
            var end = new WebGenerator(Path.Combine(worldPath, "DIM1"), Path.Combine(outPath, worldName, "the_end"), worldName + " the_end", force, false, "End");

            Logger.Information("Processing overworld");
            ow.DoProcess();

            Logger.Information("Processing the nether");
            nether.DoProcess();

            Logger.Information("Processing the end");
            end.DoProcess();
        }
    }
}
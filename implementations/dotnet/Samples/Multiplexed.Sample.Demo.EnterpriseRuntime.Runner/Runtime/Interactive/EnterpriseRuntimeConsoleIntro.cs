namespace Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Runtime.Interactive
{
    /// <summary>
    /// Prints the enterprise runtime demo introduction.
    /// </summary>
    public static class EnterpriseRuntimeConsoleIntro
    {
        /// <summary>
        /// Prints the interactive console demo introduction.
        /// </summary>
        public static void Print()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Enterprise Runtime Demo");
            Console.WriteLine("=======================");
            Console.ResetColor();
            Console.WriteLine();

            Console.WriteLine("This demo shows a deterministic AI runtime executing production-style workflows");
            Console.WriteLine("with distributed workers, durable execution state, retry recovery, retention,");
            Console.WriteLine("distributed throttling, replay validation, realtime logs, and interactive");
            Console.WriteLine("execution control.");
            Console.WriteLine();

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("Available scenarios:");
            Console.ResetColor();
            Console.WriteLine();

            PrintScenario(
                "json",
                "Runs the standard JSON pipeline demo from a pipeline file. Best for a quick sanity check of the controller, distributed workers, retry recovery, and replay validation.");

            PrintScenario(
                "chaos-100",
                "Runs an in-memory distributed chaos pipeline with 100 steps. Good for demonstrating multi-worker coordination, retry recovery, live progress, and execution control.");

            PrintScenario(
                "chaos-500",
                "Runs an aggressive in-memory distributed chaos pipeline with 500 steps. This scenario puts stronger pressure on distributed workers, retry recovery, hot-state retention, compaction, eviction, snapshot persistence, and replay restoration.");

            PrintScenario(
                "throttling-100",
                "Runs a distributed throttling pipeline with 100 steps. Demonstrates provider-level distributed concurrency control, realtime throttling visibility, lease-based admission control, and bounded provider capacity under worker pressure.");

            Console.WriteLine();

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("Controls during execution:");
            Console.WriteLine("  Space    Pause / Resume execution");
            Console.WriteLine("  Shift+C  Cancel with confirmation");
            Console.ResetColor();
            Console.WriteLine();

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("Press any key to continue to scenario selection...");
            Console.ResetColor();

            Console.ReadKey(
                intercept: true);

            Console.Clear();
        }

        /// <summary>
        /// Prints one scenario description.
        /// </summary>
        /// <param name="name">
        /// The scenario name.
        /// </param>
        /// <param name="description">
        /// The scenario description.
        /// </param>
        private static void PrintScenario(
            string name,
            string description)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write($"  {name}");
            Console.ResetColor();

            Console.WriteLine();
            Console.WriteLine($"    {description}");
            Console.WriteLine();
        }
    }
}
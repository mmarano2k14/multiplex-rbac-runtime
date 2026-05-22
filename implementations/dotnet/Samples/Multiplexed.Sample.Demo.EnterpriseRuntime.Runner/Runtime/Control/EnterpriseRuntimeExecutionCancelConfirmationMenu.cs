namespace Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Runtime.Execution.Control
{
    /// <summary>
    /// Displays a confirmation menu before cancelling an execution.
    /// </summary>
    public static class EnterpriseRuntimeExecutionCancelConfirmationMenu
    {
        private const string Yes = "Yes";
        private const string No = "No";

        /// <summary>
        /// Asks the user to confirm execution cancellation.
        /// </summary>
        /// <returns>
        /// True when cancellation is confirmed; otherwise, false.
        /// </returns>
        public static bool Confirm()
        {
            var items = new[]
            {
                Yes,
                No
            };

            var selectedIndex = 1;

            while (true)
            {
                Draw(
                    items,
                    selectedIndex);

                var key = Console.ReadKey(
                    intercept: true);

                if (key.Key == ConsoleKey.LeftArrow ||
                    key.Key == ConsoleKey.UpArrow)
                {
                    selectedIndex = selectedIndex == 0
                        ? items.Length - 1
                        : selectedIndex - 1;

                    continue;
                }

                if (key.Key == ConsoleKey.RightArrow ||
                    key.Key == ConsoleKey.DownArrow)
                {
                    selectedIndex = selectedIndex == items.Length - 1
                        ? 0
                        : selectedIndex + 1;

                    continue;
                }

                if (key.Key == ConsoleKey.Enter)
                {
                    var confirmed = string.Equals(
                        items[selectedIndex],
                        Yes,
                        StringComparison.OrdinalIgnoreCase);

                    Console.Clear();

                    return confirmed;
                }

                if (key.Key == ConsoleKey.Y)
                {
                    Console.Clear();

                    return true;
                }

                if (key.Key == ConsoleKey.N ||
                    key.Key == ConsoleKey.Escape)
                {
                    Console.Clear();

                    return false;
                }
            }
        }

        /// <summary>
        /// Draws the cancel confirmation menu.
        /// </summary>
        /// <param name="items">
        /// The menu items.
        /// </param>
        /// <param name="selectedIndex">
        /// The selected item index.
        /// </param>
        private static void Draw(
            IReadOnlyList<string> items,
            int selectedIndex)
        {
            Console.Clear();

            Console.WriteLine("Enterprise Runtime Demo");
            Console.WriteLine("=======================");
            Console.WriteLine();

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Cancel execution?");
            Console.ResetColor();

            Console.WriteLine();

            for (var index = 0; index < items.Count; index++)
            {
                var selected = index == selectedIndex;

                if (selected)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write("> ");
                }
                else
                {
                    Console.Write("  ");
                }

                Console.WriteLine(
                    items[index]);

                if (selected)
                {
                    Console.ResetColor();
                }
            }

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("Use Up / Down to choose, Enter to confirm, Y/N as shortcut.");
            Console.ResetColor();
        }
    }
}
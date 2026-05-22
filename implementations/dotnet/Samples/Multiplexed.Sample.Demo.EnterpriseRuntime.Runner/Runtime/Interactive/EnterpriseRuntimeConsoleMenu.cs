namespace Multiplexed.Sample.Demo.EnterpriseRuntime.Runner.Runtime.Interactive
{
    /// <summary>
    /// Provides a simple interactive console selection menu.
    /// </summary>
    public static class EnterpriseRuntimeConsoleMenu
    {
        /// <summary>
        /// Selects one item from a list using the keyboard.
        /// </summary>
        /// <param name="title">
        /// The menu title.
        /// </param>
        /// <param name="items">
        /// The selectable items.
        /// </param>
        /// <returns>
        /// The selected item.
        /// </returns>
        public static string Select(
            string title,
            IReadOnlyList<string> items)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(
                title);

            ArgumentNullException.ThrowIfNull(
                items);

            if (items.Count == 0)
            {
                throw new InvalidOperationException(
                    "Cannot display an interactive menu without items.");
            }

            var selectedIndex = 0;

            while (true)
            {
                Draw(
                    title,
                    items,
                    selectedIndex);

                var key = System.Console.ReadKey(
                    intercept: true);

                if (key.Key == ConsoleKey.UpArrow)
                {
                    selectedIndex = selectedIndex == 0
                        ? items.Count - 1
                        : selectedIndex - 1;

                    continue;
                }

                if (key.Key == ConsoleKey.DownArrow)
                {
                    selectedIndex = selectedIndex == items.Count - 1
                        ? 0
                        : selectedIndex + 1;

                    continue;
                }

                if (key.Key == ConsoleKey.Enter)
                {
                    var selectedItem = items[selectedIndex];

                    System.Console.Clear();

                    return selectedItem;
                }
            }
        }

        /// <summary>
        /// Draws the interactive menu.
        /// </summary>
        /// <param name="title">
        /// The menu title.
        /// </param>
        /// <param name="items">
        /// The selectable items.
        /// </param>
        /// <param name="selectedIndex">
        /// The selected item index.
        /// </param>
        private static void Draw(
            string title,
            IReadOnlyList<string> items,
            int selectedIndex)
        {
            System.Console.Clear();

            System.Console.WriteLine("Enterprise Runtime Demo");
            System.Console.WriteLine("=======================");
            System.Console.WriteLine();

            System.Console.WriteLine(
                title);

            System.Console.WriteLine();

            for (var index = 0; index < items.Count; index++)
            {
                var selected = index == selectedIndex;

                if (selected)
                {
                    System.Console.ForegroundColor = ConsoleColor.Green;
                    System.Console.Write("> ");
                }
                else
                {
                    System.Console.Write("  ");
                }

                System.Console.WriteLine(
                    items[index]);

                if (selected)
                {
                    System.Console.ResetColor();
                }
            }

            System.Console.WriteLine();
            System.Console.ForegroundColor = ConsoleColor.DarkGray;
            System.Console.WriteLine("Use Up / Down to choose, Enter to confirm.");
            System.Console.ResetColor();
        }
    }
}
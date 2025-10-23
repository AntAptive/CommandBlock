namespace CommandBlock
{
    internal class Utils
    {
        /// <summary>
        /// Prints a blank message to the console on a new line with no timestamp or tagging.
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <param name="color">Foreground color of message</param>
        internal static void PrintRaw(object message, ConsoleColor color = ConsoleColor.Gray)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ResetColor();
        }

        /// <summary>
        /// Prints a message to the console with a timestamp and INFO tag.
        /// </summary>
        /// <param name="message"></param>
        internal static void Print(object message)
        {
            Console.ResetColor();
            Console.Write($"[{DateTime.Now.ToString("MM/dd/yyyy hh:mm:ss tt")}]");
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.Write($" INFO: ");
            Console.ResetColor();
            Console.Write($"{message}\n");
        }

        /// <summary>
        /// Prints a message to the console with a timestamp and ERROR tag.
        /// </summary>
        /// <param name="message"></param>
        internal static void PrintError(object message)
        {

            Console.Write($"[{DateTime.Now.ToString("MM/dd/yyyy hh:mm:ss tt")}]");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write(" ERROR: ");
            Console.ResetColor();
            Console.Write(message + "\n");
        }

        /// <summary>
        /// Prints a message to the console with a timestamp and ERROR tag.
        /// </summary>
        /// <param name="message"></param>
        internal static void PrintWarn(object message)
        {

            Console.Write($"[{DateTime.Now.ToString("MM/dd/yyyy hh:mm:ss tt")}]");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write(" WARN: ");
            Console.ResetColor();
            Console.Write(message + "\n");
        }
    }
}

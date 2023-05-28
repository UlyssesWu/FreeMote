using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace FreeMote
{
    internal delegate void LogEventHandler(string message);

    public static class Logger
    {
        //Log event
        internal static event LogEventHandler OnLog;
        internal static event LogEventHandler OnLogWarn;
        internal static event LogEventHandler OnLogError;
        internal static event LogEventHandler OnLogHint;

        //public static void Test()
        //{
        //    Log("Info...");
        //    LogWarn("Warning...");
        //    LogError("Error...");
        //    LogHint("Hint...");
        //}

        internal static void Log(string message)
        {
#if DEBUG
            if (OnLog == null)
            {
                Debug.WriteLine(message);
            }
#endif
            OnLog?.Invoke(message);
        }

        internal static void Log(object obj) => Log(obj?.ToString());

        internal static void LogWarn(string message)
        {
#if DEBUG
            if (OnLogWarn == null)
            {
                Debug.WriteLine(message);
            }
#endif
            if (OnLogWarn == null)
            {
                Console.WriteLine(message);
            }
            else
            {
                OnLogWarn?.Invoke(message);
            }
        }
        internal static void LogWarn(object obj) => LogWarn(obj?.ToString());

        internal static void LogError(string message)
        {
#if DEBUG
            if (OnLogError == null)
            {
                Debug.WriteLine(message);
            }
#endif
            if (OnLogError == null)
            {
                Console.WriteLine(message);
            }
            else
            {
                OnLogError?.Invoke(message);
            }
        }
        internal static void LogError(object obj) => LogError(obj?.ToString());

        internal static void LogHint(string message)
        {
#if DEBUG
            if (OnLogHint == null)
            {
                Debug.WriteLine(message);
            }
#endif
            OnLogHint?.Invoke(message);
        }
        internal static void LogHint(object obj) => LogHint(obj?.ToString());

        public static void InitConsole(bool colorful = true)
        {
            OnLog += Console.WriteLine;

            if (colorful)
            {
                OnLogWarn += message => ColorConsole.WriteLine(message, ConsoleColor.Yellow);
                OnLogError += message => ColorConsole.WriteLine(message, ConsoleColor.Red);
                OnLogHint += message => ColorConsole.WriteLine(message, ConsoleColor.Cyan);
            }
            else
            {
                //OnLogWarn += message => Console.WriteLine($"[WARN] {message}");
                //OnLogError += message => Console.WriteLine($"[ERROR] {message}");
                OnLogWarn += Console.WriteLine;
                OnLogError += Console.WriteLine;
                OnLogHint += Console.WriteLine;
            }
        }
    }

    //REF: https://gist.github.com/RickStrahl/52c9ee43bd2723bcdf7bf4d24b029768
    /// <summary>
    /// Console Color Helper class that provides coloring to individual commands
    /// </summary>
    public static class ColorConsole
    {
        /// <summary>
        /// WriteLine with color
        /// </summary>
        /// <param name="text"></param>
        /// <param name="color"></param>
        public static void WriteLine(string text, ConsoleColor? color = null)
        {
            if (color.HasValue)
            {
                var oldColor = System.Console.ForegroundColor;
                if (color == oldColor)
                    Console.WriteLine(text);
                else
                {
                    Console.ForegroundColor = color.Value;
                    Console.WriteLine(text);
                    Console.ForegroundColor = oldColor;
                }
            }
            else
                Console.WriteLine(text);
        }

        /// <summary>
        /// Writes out a line with a specific color as a string
        /// </summary>
        /// <param name="text">Text to write</param>
        /// <param name="color">A console color. Must match ConsoleColors collection names (case insensitive)</param>
        public static void WriteLine(string text, string color)
        {
            if (string.IsNullOrEmpty(color))
            {
                WriteLine(text);
                return;
            }

            if (!Enum.TryParse(color, true, out ConsoleColor col))
            {
                WriteLine(text);
            }
            else
            {
                WriteLine(text, col);
            }
        }

        /// <summary>
        /// Write with color
        /// </summary>
        /// <param name="text"></param>
        /// <param name="color"></param>
        public static void Write(string text, ConsoleColor? color = null)
        {
            if (color.HasValue)
            {
                var oldColor = System.Console.ForegroundColor;
                if (color == oldColor)
                    Console.Write(text);
                else
                {
                    Console.ForegroundColor = color.Value;
                    Console.Write(text);
                    Console.ForegroundColor = oldColor;
                }
            }
            else
                Console.Write(text);
        }

        /// <summary>
        /// Writes out a line with color specified as a string
        /// </summary>
        /// <param name="text">Text to write</param>
        /// <param name="color">A console color. Must match ConsoleColors collection names (case insensitive)</param>
        public static void Write(string text, string color)
        {
            if (string.IsNullOrEmpty(color))
            {
                Write(text);
                return;
            }

            if (!ConsoleColor.TryParse(color, true, out ConsoleColor col))
            {
                Write(text);
            }
            else
            {
                Write(text, col);
            }
        }

        #region Wrappers and Templates


        /// <summary>
        /// Writes a line of header text wrapped in a in a pair of lines of dashes:
        /// -----------
        /// Header Text
        /// -----------
        /// and allows you to specify a color for the header. The dashes are colored
        /// </summary>
        /// <param name="headerText">Header text to display</param>
        /// <param name="wrapperChar">wrapper character (-)</param>
        /// <param name="headerColor">Color for header text (yellow)</param>
        /// <param name="dashColor">Color for dashes (gray)</param>
        public static void WriteWrappedHeader(string headerText,
                                                char wrapperChar = '-',
                                                ConsoleColor headerColor = ConsoleColor.Yellow,
                                                ConsoleColor dashColor = ConsoleColor.DarkGray)
        {
            if (string.IsNullOrEmpty(headerText))
                return;

            string line = new string(wrapperChar, headerText.Length);

            WriteLine(line, dashColor);
            WriteLine(headerText, headerColor);
            WriteLine(line, dashColor);
        }

        private static Lazy<Regex> colorBlockRegEx = new Lazy<Regex>(
            () => new Regex("\\[(?<color>.*?)\\](?<text>[^[]*)\\[/\\k<color>\\]", RegexOptions.IgnoreCase),
            isThreadSafe: true);

        /// <summary>
        /// Allows a string to be written with embedded color values using:
        /// This is [red]Red[/red] text and this is [cyan]Blue[/blue] text
        /// </summary>
        /// <param name="text">Text to display</param>
        /// <param name="baseTextColor">Base text color</param>
        public static void WriteEmbeddedColorLine(string text, ConsoleColor? baseTextColor = null)
        {
            if (baseTextColor == null)
                baseTextColor = Console.ForegroundColor;

            if (string.IsNullOrEmpty(text))
            {
                WriteLine(string.Empty);
                return;
            }

            int at = text.IndexOf("[");
            int at2 = text.IndexOf("]");
            if (at == -1 || at2 <= at)
            {
                WriteLine(text, baseTextColor);
                return;
            }

            while (true)
            {
                var match = colorBlockRegEx.Value.Match(text);
                if (match.Length < 1)
                {
                    Write(text, baseTextColor);
                    break;
                }

                // write up to expression
                Write(text.Substring(0, match.Index), baseTextColor);

                // strip out the expression
                string highlightText = match.Groups["text"].Value;
                string colorVal = match.Groups["color"].Value;

                Write(highlightText, colorVal);

                // remainder of string
                text = text.Substring(match.Index + match.Value.Length);
            }

            Console.WriteLine();
        }

        #endregion

        #region Success, Error, Info, Warning Wrappers

        /// <summary>
        /// Write a Success Line - green
        /// </summary>
        /// <param name="text">Text to write out</param>
        public static void WriteSuccess(string text)
        {
            WriteLine(text, ConsoleColor.Green);
        }

        /// <summary>
        /// Write a Error Line - Red
        /// </summary>
        /// <param name="text">Text to write out</param>
        public static void WriteError(string text)
        {
            WriteLine(text, ConsoleColor.Red);
        }

        /// <summary>
        /// Write a Warning Line - Yellow
        /// </summary>
        /// <param name="text">Text to Write out</param>
        public static void WriteWarning(string text)
        {
            WriteLine(text, ConsoleColor.DarkYellow);
        }


        /// <summary>
        /// Write a Info Line - dark cyan
        /// </summary>
        /// <param name="text">Text to write out</param>
        public static void WriteInfo(string text)
        {
            WriteLine(text, ConsoleColor.DarkCyan);
        }

        #endregion
    }
}

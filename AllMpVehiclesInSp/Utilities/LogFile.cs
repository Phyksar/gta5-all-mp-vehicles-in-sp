using GTA;
using System;
using System.IO;

namespace Utilities
{
    public class LogFile : IDisposable
    {
        private const int InvalidStringIndex = -1;

        public bool EnableDebugLogging;

        private FileStream WriteStream;
        private StreamWriter StreamWriter;

        public LogFile(string filename)
        {
            try {
                WriteStream = new FileStream(filename, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            } catch (IOException) {
                StreamWriter = null;
                return;
            }
            StreamWriter = new StreamWriter(WriteStream);
        }

        public void Dispose()
        {
            StreamWriter?.Dispose();
            WriteStream?.Dispose();
            StreamWriter = null;
            WriteStream = null;
        }

        public void Message(string message)
        {
            WriteLine(string.Empty, message);
        }

        public void ErrorMessage(string message)
        {
            WriteLine("error", message);
        }

        public void DebugMessage(string message)
        {
            if (EnableDebugLogging) {
                WriteLine("debug", message);
            }
        }

        private void WriteLine(string severity, string message)
        {
            if (StreamWriter == null) {
                return;
            }

            var dateTime = DateTime.Now;
            var gameTime = FormatGameTime(Game.GameTime);
            var buffer = $"{dateTime:yyyy-MM-dd HH:mm:ss.fff} ({gameTime}) | ";
            if (!string.IsNullOrEmpty(severity)) {
                buffer += $"[{severity.ToUpper()}] ";
            }
            int newLineIndex;
            var messageIndex = 0;
            while ((newLineIndex = message.IndexOf('\n', messageIndex)) != InvalidStringIndex) {
                if (messageIndex < newLineIndex) {
                    buffer += message.Substring(messageIndex, newLineIndex - messageIndex);
                }
                buffer += "\n                                        | ";
                messageIndex = newLineIndex + 1;
            }
            if (messageIndex < message.Length) {
                buffer += message.Substring(messageIndex);
            }
            StreamWriter.WriteLine(buffer);
            StreamWriter.Flush();
        }

        public static string FormatGameTime(int gameTime)
        {
            const int MinutesPerHour = 60;
            const int SecondsPerMinute = 60;
            const int MillisecondsPerSecond = 1000;
            const int MillisecondsPerMinute = MillisecondsPerSecond * SecondsPerMinute;
            const int MillisecondsPerHour = MillisecondsPerMinute * MinutesPerHour;

            var gameHours = gameTime / MillisecondsPerHour;
            var gameMinutes = (gameTime / MillisecondsPerMinute) % MinutesPerHour;
            var gameSeconds = (gameTime / MillisecondsPerSecond) % SecondsPerMinute;
            var gameMilliseconds = gameTime % MillisecondsPerSecond;
            return $"{gameHours:d3}:{gameMinutes:d2}:{gameSeconds:d2}.{gameMilliseconds:d3}";
        }
    }
}

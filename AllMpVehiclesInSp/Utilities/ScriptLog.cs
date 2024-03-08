using GTA;
using System;
using System.IO;

namespace Utilities
{
    public class ScriptLog : IDisposable
    {
        private const int InvalidStringIndex = -1;

        private FileStream WriteStream;
        private StreamWriter StreamWriter;

        public bool EnableDebugLogging { get; set; }

        public ScriptLog(string filename, bool enableDebugLogging = false)
        {
            EnableDebugLogging = enableDebugLogging;
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

            const int MinutesPerHour = 60;
            const int SecondsPerMinute = 60;
            const int MillisecondsPerSecond = 1000;
            const int MillisecondsPerMinute = MillisecondsPerSecond * SecondsPerMinute;
            const int MillisecondsPerHour = MillisecondsPerMinute * MinutesPerHour;

            var dateTime = DateTime.Now;
            var gameTime = Game.GameTime;
            var gameHours = gameTime / MillisecondsPerHour;
            var gameMinutes = (gameTime / MillisecondsPerMinute) % MinutesPerHour;
            var gameSeconds = (gameTime / MillisecondsPerSecond) % SecondsPerMinute;
            var gameMilliseconds = gameTime % MillisecondsPerSecond;
            var buffer = $"{dateTime:yyyy-MM-dd HH:mm:ss.fff} "
                + $"({gameHours:d3}:{gameMinutes:d2}:{gameSeconds:d2}.{gameMilliseconds:d3}) | ";
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
            StreamWriter.WriteLineAsync(buffer);
            StreamWriter.FlushAsync();
        }
    }
}

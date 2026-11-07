using System;
using System.IO;
using System.Text;
using Evolution.Rooms;

namespace Evolution.Persistence
{
    // Reads and writes the save file of a participant (state.json inside the participant folder).
    //
    // Atomic write: the new content goes to state.json.tmp first and only then replaces state.json.
    // If the game closes in the middle, the old state.json is still intact. The previous version is
    // kept as state.json.bak, which is read if state.json is ever unreadable.
    public static class StateStore
    {
        public const string FileName = "state.json";

        public static string PathIn(string folder) => Path.Combine(folder, FileName);

        public static void Save(string folder, EvolutionState state)
        {
            state.UpdatedAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
            WriteAtomic(PathIn(folder), StateSerializer.ToJson(state));
        }

        // Returns null when the participant has no save yet.
        // warning != null when state.json was unreadable and the backup was used (or both failed).
        public static EvolutionState Load(string folder, RoomAttributeRanges ranges, out string warning)
        {
            warning = null;
            var path = PathIn(folder);
            var backup = path + ".bak";
            if (!File.Exists(path) && !File.Exists(backup)) return null;

            try
            {
                return StateSerializer.FromJson(File.ReadAllText(path, Encoding.UTF8), ranges);
            }
            catch (Exception e) when (e is IOException || e is FormatException || e is UnauthorizedAccessException)
            {
                warning = $"{path} could not be read ({e.Message}).";
            }

            try
            {
                var state = StateSerializer.FromJson(File.ReadAllText(backup, Encoding.UTF8), ranges);
                warning += " Loaded the backup (the last run before it may be played again).";
                return state;
            }
            catch (Exception e) when (e is IOException || e is FormatException || e is UnauthorizedAccessException)
            {
                // Both are broken: refuse to silently start over, the caller decides.
                throw new IOException($"{warning} The backup could not be read either ({e.Message}).", e);
            }
        }

        // Renames the participant folder (keeps the data, but the next Load starts from zero).
        public static string Archive(string folder)
        {
            if (!Directory.Exists(folder)) return null;
            var archived = $"{folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)}_old_{DateTime.Now:yyyyMMdd_HHmmss}";
            Directory.Move(folder, archived);
            return archived;
        }

        public static void WriteAtomic(string path, string content)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            var temp = path + ".tmp";
            var bytes = new UTF8Encoding(false).GetBytes(content);
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true); // really on disk before the rename
            }

            if (File.Exists(path))
            {
                File.Replace(temp, path, path + ".bak");
            }
            else
            {
                File.Move(temp, path);
            }
        }

        // Keeps only letters, digits, '-' and '_', so any participant ID is a valid folder name.
        public static string SafeFolderName(string id)
        {
            var text = new StringBuilder();
            foreach (var c in id ?? "")
            {
                if (char.IsLetterOrDigit(c) || c == '-' || c == '_') text.Append(c);
            }
            return text.Length > 0 ? text.ToString() : "dev";
        }
    }
}

using System;
using System.IO;
using System.Linq;

namespace ReadOnlyDetector
{
    public class WillowSaveGame
    {

        #region Fields

        // This is the file path to our save game
        public string FilePath { get; set;  }

        public string saveGame { get; }

        public string saveGameFileName { get; }

        public DateTime lastWriteTime { get; set; }

        public bool saveGameInReadOnly { get; set; }

        #endregion

        #region Constructors

        public WillowSaveGame(bool bl2)
        {

            DirectoryInfo directory = getSaveGameDirectory(bl2);

            FileInfo saveInfo = directory
                .GetFiles("*.sav", SearchOption.TopDirectoryOnly)
                .OrderByDescending(f => f.LastWriteTime)
                .First();

            saveGame = saveInfo.FullName;

            saveGameFileName = saveInfo.Name;

            saveGameInReadOnly = saveInfo.IsReadOnly;
        }

        public WillowSaveGame(string saveGameName, bool bl2)
        {
            DirectoryInfo directoryInfo = getSaveGameDirectory(bl2);

            FileInfo[] saveGameFiles = directoryInfo.GetFiles("*.sav", SearchOption.TopDirectoryOnly);

            foreach (FileInfo f in saveGameFiles)
            {
                if (f.Name != saveGameName) continue;

                saveGame = f.FullName;

                saveGameFileName = f.Name;

                saveGameInReadOnly = f.IsReadOnly;
                break;
            }

        }

        #endregion

        #region Methods

        // This toggles read-only depending on the state of the save file
        public void toggleReadOnly()
        {
            FileInfo saveInfo = new FileInfo(saveGame);

            saveInfo.IsReadOnly = !saveInfo.IsReadOnly;
            saveGameInReadOnly = saveInfo.IsReadOnly;
        }

        // This is the last write time of our file, stored as generic file write time stored in windows.
        public DateTime LastWriteTime()
        {
            lastWriteTime = new FileInfo(saveGame).LastWriteTime;
            return new FileInfo(saveGame).LastWriteTime;
        }

        private DirectoryInfo getSaveGameDirectory(bool bl2)
        {
            FilePath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"\My Games\" + (bl2 ? "Borderlands 2" : "Borderlands The Pre-Sequel") + @"\WillowGame\SaveData";

            #region Directory Handling

            if (!Directory.Exists(FilePath)) return null;

            var directory = new DirectoryInfo(FilePath);
            var mostRecentSaveDirectory = directory.GetDirectories().OrderByDescending(f => f.LastWriteTime).First();

            FilePath = FilePath + @"\" + mostRecentSaveDirectory.Name;

            if (!Directory.Exists(FilePath)) return null;

            return directory = new DirectoryInfo(FilePath);

            #endregion

        }

        #endregion
    }
}

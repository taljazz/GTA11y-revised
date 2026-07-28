using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace GrandTheftAccessibility
{
    /// <summary>
    /// Remembers a touchdown heading per landing destination.
    ///
    /// Runways carry their own heading, but helipads have no natural one and it
    /// cannot be derived from anything the mod knows - a rooftop pad's usable
    /// orientation depends on where the door, the aerials and the neighbouring
    /// towers are. Rather than invent numbers, the pilot flies to a pad, points
    /// the aircraft the way they want to end up, and saves it. After that the
    /// autopilot turns onto that heading before every descent there.
    ///
    /// Kept out of SettingsManager deliberately: the settings menu is built from
    /// every key that manager holds, so storing headings there would bury the
    /// real settings under a list of destination names.
    /// </summary>
    public class HelipadHeadingStore
    {
        #region Fields

        private readonly string _savePath;
        private Dictionary<string, float> _headings = new Dictionary<string, float>();

        #endregion

        #region Construction

        public HelipadHeadingStore()
        {
            try
            {
                string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string modSettingsPath = Path.Combine(documentsPath, Constants.SETTINGS_FOLDER_PATH.TrimStart('/'));

                if (!Directory.Exists(modSettingsPath))
                    Directory.CreateDirectory(modSettingsPath);

                _savePath = Path.Combine(modSettingsPath, Constants.HELIPAD_HEADINGS_FILE_NAME);
                Load();
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "HelipadHeadingStore constructor");
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// The saved touchdown heading for a destination, or -1 when none is set.
        /// </summary>
        public float GetHeading(string destinationName)
        {
            if (string.IsNullOrEmpty(destinationName) || _headings == null)
                return -1f;

            float heading;
            return _headings.TryGetValue(destinationName, out heading) ? heading : -1f;
        }

        /// <summary>Whether a heading has been saved for this destination.</summary>
        public bool HasHeading(string destinationName)
        {
            return GetHeading(destinationName) >= 0f;
        }

        /// <summary>
        /// Save a touchdown heading (0-359) for a destination and write to disk.
        /// </summary>
        public void SetHeading(string destinationName, float heading)
        {
            if (string.IsNullOrEmpty(destinationName) || _headings == null)
                return;

            // Normalize into 0-359 so a negative or wrapped value cannot be
            // mistaken for "unset" later
            float normalized = heading % 360f;
            if (normalized < 0f)
                normalized += 360f;

            _headings[destinationName] = normalized;
            Save();
        }

        /// <summary>Forget the heading for a destination.</summary>
        public bool ClearHeading(string destinationName)
        {
            if (string.IsNullOrEmpty(destinationName) || _headings == null)
                return false;

            if (!_headings.Remove(destinationName))
                return false;

            Save();
            return true;
        }

        #endregion

        #region Persistence

        private void Load()
        {
            if (string.IsNullOrEmpty(_savePath) || !File.Exists(_savePath))
                return;

            try
            {
                string json = File.ReadAllText(_savePath);
                var loaded = JsonConvert.DeserializeObject<Dictionary<string, float>>(json);
                if (loaded != null)
                    _headings = loaded;

                Logger.Debug($"HelipadHeadingStore: loaded {_headings.Count} saved headings");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "HelipadHeadingStore.Load");
            }
        }

        private void Save()
        {
            if (string.IsNullOrEmpty(_savePath))
                return;

            try
            {
                string json = JsonConvert.SerializeObject(_headings, Formatting.Indented);
                File.WriteAllText(_savePath, json);
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "HelipadHeadingStore.Save");
            }
        }

        #endregion
    }
}

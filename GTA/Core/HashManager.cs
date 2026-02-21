using System;
using System.Collections.Generic;

namespace GrandTheftAccessibility
{
    /// <summary>
    /// Centralized manager for entity hash lookups.
    /// Loads hashes from file once and provides shared access.
    /// Uses int keys to match NativeValue and avoid ToString() allocations.
    /// </summary>
    public static class HashManager
    {
        private static Dictionary<int, string> _hashes;
        private static bool _initialized;
        private static bool _loadFailed;
        private static readonly object _lock = new object();

        /// <summary>
        /// Gets the loaded hashes dictionary.
        /// Automatically initializes on first access.
        /// Returns empty dictionary if loading failed.
        /// </summary>
        public static Dictionary<int, string> Hashes
        {
            get
            {
                EnsureInitialized();
                return _hashes ?? (_hashes = new Dictionary<int, string>());
            }
        }

        /// <summary>
        /// Gets the number of loaded hashes.
        /// Returns 0 if not initialized or loading failed.
        /// </summary>
        public static int Count
        {
            get
            {
                EnsureInitialized();
                return _hashes?.Count ?? 0;
            }
        }

        /// <summary>
        /// Gets whether the hash file was successfully loaded.
        /// </summary>
        public static bool IsLoaded
        {
            get
            {
                EnsureInitialized();
                return _initialized && !_loadFailed && _hashes != null && _hashes.Count > 0;
            }
        }

        /// <summary>
        /// Try to get a name for the given hash value.
        /// </summary>
        /// <param name="hash">The entity model hash (int)</param>
        /// <param name="name">The entity name if found</param>
        /// <returns>True if the hash was found, false otherwise</returns>
        public static bool TryGetName(int hash, out string name)
        {
            name = null;

            try
            {
                EnsureInitialized();

                if (_hashes == null)
                    return false;

                return _hashes.TryGetValue(hash, out name);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if a hash exists in the dictionary.
        /// </summary>
        /// <param name="hash">The entity model hash</param>
        /// <returns>True if the hash exists</returns>
        public static bool ContainsHash(int hash)
        {
            try
            {
                EnsureInitialized();

                if (_hashes == null)
                    return false;

                return _hashes.ContainsKey(hash);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Ensures hashes are loaded. Thread-safe.
        /// </summary>
        private static void EnsureInitialized()
        {
            if (_initialized) return;

            lock (_lock)
            {
                if (_initialized) return;

                try
                {
                    _hashes = new Dictionary<int, string>();
                    LoadHashes();
                    _initialized = true;
                }
                catch (Exception ex)
                {
                    _loadFailed = true;
                    _initialized = true;  // Mark as initialized to prevent retry loops
                    Logger.Exception(ex, "HashManager initialization");
                }
            }
        }

        /// <summary>
        /// Load entity hashes from file.
        /// Uses int keys to avoid ToString() allocations during lookups.
        /// </summary>
        private static void LoadHashes()
        {
            try
            {
                string hashFilePath = Constants.HASH_FILE_PATH;

                if (string.IsNullOrEmpty(hashFilePath))
                {
                    Logger.Warning("HashManager: No hash file path configured");
                    _loadFailed = true;
                    return;
                }

                if (!System.IO.File.Exists(hashFilePath))
                {
                    Logger.Warning($"Hash file not found: {hashFilePath}");
                    _loadFailed = true;
                    return;
                }

                string[] lines = System.IO.File.ReadAllLines(hashFilePath);

                if (lines == null || lines.Length == 0)
                {
                    Logger.Warning("Hash file is empty");
                    _loadFailed = true;
                    return;
                }

                int loadedCount = 0;
                int errorCount = 0;

                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    try
                    {
                        string[] parts = line.Split('=');
                        if (parts.Length == 2 &&
                            !string.IsNullOrEmpty(parts[0]) &&
                            int.TryParse(parts[1], out int hashValue) &&
                            !_hashes.ContainsKey(hashValue))
                        {
                            _hashes.Add(hashValue, parts[0]);
                            loadedCount++;
                        }
                    }
                    catch
                    {
                        errorCount++;
                        if (errorCount > 100)
                        {
                            Logger.Warning("HashManager: Too many parsing errors, stopping");
                            break;
                        }
                    }
                }

                if (loadedCount > 0)
                {
                    Logger.Info($"HashManager: Loaded {loadedCount} entity hashes");
                    _loadFailed = false;
                }
                else
                {
                    Logger.Warning("HashManager: No valid hashes found in file");
                    _loadFailed = true;
                }
            }
            catch (Exception ex)
            {
                _loadFailed = true;
                Logger.Exception(ex, "HashManager.LoadHashes");
            }
        }
    }
}

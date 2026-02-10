using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace GrandTheftAccessibility
{
    /// <summary>
    /// Simple file-based logger for debugging the mod
    /// Thread-safe, non-blocking, and crash-resistant
    /// Uses a background thread to write logs asynchronously
    /// </summary>
    public static class Logger
    {
        private static readonly object _lock = new object();
        private static readonly Queue<string> _logQueue = new Queue<string>();
        private static string _logFilePath;
        private static bool _initialized;
        private static bool _enabled = true;
        private static Thread _writerThread;
        private static bool _shutdownRequested;
        private static readonly AutoResetEvent _logEvent = new AutoResetEvent(false);

        // Error throttling to prevent log flooding
        private static readonly Dictionary<string, long> _lastErrorTime = new Dictionary<string, long>();
        private static readonly Dictionary<string, int> _errorSuppressionCount = new Dictionary<string, int>();
        private const long ERROR_THROTTLE_TICKS = 50_000_000; // 5 seconds in ticks
        private const int MAX_ERROR_TRACKING_ENTRIES = 100; // Limit dictionary size to prevent memory leak
        private static long _lastErrorCleanupTick;

        /// <summary>
        /// Log levels for filtering
        /// </summary>
        public enum LogLevel
        {
            Debug,
            Info,
            Warning,
            Error
        }

        /// <summary>
        /// Minimum log level to write (set higher to reduce log verbosity)
        /// Default to Info to reduce file I/O - set to Debug only when debugging
        /// </summary>
        public static LogLevel MinLevel { get; set; } = LogLevel.Info;

        /// <summary>
        /// Check before building expensive debug strings to avoid allocation when debug is disabled.
        /// Usage: if (Logger.IsDebugEnabled) Logger.Debug($"expensive {thing}");
        /// </summary>
        public static bool IsDebugEnabled => _enabled && _initialized && MinLevel <= LogLevel.Debug;

        /// <summary>
        /// Enable or disable logging entirely
        /// </summary>
        public static bool Enabled
        {
            get => _enabled;
            set => _enabled = value;
        }

        /// <summary>
        /// Initialize the logger with the log file path
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;

            lock (_lock)
            {
                if (_initialized) return;

                try
                {
                    // Log to Documents\Rockstar Games\GTA V\ModSettings\gta11y.log
                    string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    if (string.IsNullOrEmpty(documentsPath))
                    {
                        _initialized = false;
                        _enabled = false;
                        return;
                    }

                    string settingsFolder = Path.Combine(documentsPath, "Rockstar Games", "GTA V", "ModSettings");

                    if (!Directory.Exists(settingsFolder))
                    {
                        Directory.CreateDirectory(settingsFolder);
                    }

                    _logFilePath = Path.Combine(settingsFolder, "gta11y.log");

                    // Always start fresh - delete old log to avoid confusion
                    try
                    {
                        if (File.Exists(_logFilePath))
                        {
                            File.Delete(_logFilePath);
                        }
                    }
                    catch
                    {
                        // If we can't delete, try to write anyway
                    }

                    // Start background writer thread
                    _shutdownRequested = false;
                    _writerThread = new Thread(BackgroundWriter)
                    {
                        IsBackground = true,
                        Name = "GTA11Y Logger"
                    };
                    _writerThread.Start();

                    _initialized = true;

                    // Write startup header
                    Info("=== GTA11Y Accessibility Mod Started ===");
                    Info($"Log file: {_logFilePath}");
                    Info($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    Info("=========================================");
                }
                catch
                {
                    // If we can't initialize logging, just disable it
                    _initialized = false;
                    _enabled = false;
                }
            }
        }

        /// <summary>
        /// Background thread that writes logs to file
        /// </summary>
        private static void BackgroundWriter()
        {
            while (!_shutdownRequested)
            {
                try
                {
                    // Wait for signal or timeout (flush periodically)
                    try
                    {
                        _logEvent?.WaitOne(1000);
                    }
                    catch (ObjectDisposedException)
                    {
                        // Event was disposed, exit cleanly
                        break;
                    }

                    // Get all pending messages
                    List<string> messages = null;
                    lock (_lock)
                    {
                        if (_logQueue != null && _logQueue.Count > 0)
                        {
                            messages = new List<string>(_logQueue);
                            _logQueue.Clear();
                        }
                    }

                    // Write to file if we have messages and valid path
                    if (messages != null && messages.Count > 0 && !string.IsNullOrEmpty(_logFilePath))
                    {
                        try
                        {
                            File.AppendAllLines(_logFilePath, messages);
                        }
                        catch (IOException)
                        {
                            // File might be locked, try again next iteration
                        }
                        catch (UnauthorizedAccessException)
                        {
                            // No write permission, disable logging
                            _enabled = false;
                            break;
                        }
                    }
                }
                catch
                {
                    // Never crash the background thread
                }
            }
        }

        /// <summary>
        /// Write a log message (non-blocking - queues for background write)
        /// </summary>
        private static void Write(LogLevel level, string message)
        {
            if (!_enabled || !_initialized) return;
            if (level < MinLevel) return;
            if (string.IsNullOrEmpty(message)) return;

            try
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                string levelStr;
                switch (level)
                {
                    case LogLevel.Debug:   levelStr = "DEBUG  "; break;
                    case LogLevel.Info:    levelStr = "INFO   "; break;
                    case LogLevel.Warning: levelStr = "WARNING"; break;
                    case LogLevel.Error:   levelStr = "ERROR  "; break;
                    default:               levelStr = "UNKNOWN"; break;
                }
                string logLine = $"[{timestamp}] [{levelStr}] {message}";

                lock (_lock)
                {
                    // Limit queue size to prevent memory issues
                    if (_logQueue != null && _logQueue.Count < 1000)
                    {
                        _logQueue.Enqueue(logLine);
                    }
                }

                // Signal the writer thread (safely)
                try
                {
                    _logEvent?.Set();
                }
                catch (ObjectDisposedException)
                {
                    // Event disposed, ignore
                }
            }
            catch
            {
                // Never crash due to logging
            }
        }

        /// <summary>
        /// Log a debug message (verbose, for development).
        /// Note: callers should guard with Logger.IsDebugEnabled before building expensive strings.
        /// </summary>
        public static void Debug(string message)
        {
            if (MinLevel > LogLevel.Debug) return;  // Fast exit before method call overhead
            Write(LogLevel.Debug, message);
        }

        /// <summary>
        /// Log an info message (general information)
        /// </summary>
        public static void Info(string message)
        {
            Write(LogLevel.Info, message);
        }

        /// <summary>
        /// Log a warning message (potential issues)
        /// </summary>
        public static void Warning(string message)
        {
            Write(LogLevel.Warning, message);
        }

        /// <summary>
        /// Log an error message (something went wrong)
        /// </summary>
        public static void Error(string message)
        {
            Write(LogLevel.Error, message);
        }

        /// <summary>
        /// Log an exception with full details (throttled to prevent log flooding)
        /// </summary>
        public static void Exception(Exception ex, string context = null)
        {
            if (!_enabled || !_initialized) return;

            try
            {
                // Create a key for this error type to enable throttling
                string errorKey = $"{context ?? ""}:{ex.GetType().Name}:{ex.Message}";
                long currentTick = DateTime.Now.Ticks;

                lock (_lock)
                {
                    // Check if this error was recently logged
                    if (_lastErrorTime.TryGetValue(errorKey, out long lastTime))
                    {
                        if (currentTick - lastTime < ERROR_THROTTLE_TICKS)
                        {
                            // Suppress this error, but track count
                            if (_errorSuppressionCount.ContainsKey(errorKey))
                                _errorSuppressionCount[errorKey]++;
                            else
                                _errorSuppressionCount[errorKey] = 1;
                            return;
                        }
                    }

                    // Periodic cleanup of old error entries to prevent memory leak
                    if (currentTick - _lastErrorCleanupTick > ERROR_THROTTLE_TICKS * 10 ||
                        _lastErrorTime.Count > MAX_ERROR_TRACKING_ENTRIES)
                    {
                        CleanupOldErrorEntries(currentTick);
                        _lastErrorCleanupTick = currentTick;
                    }

                    // Log the error
                    string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                    string contextStr = string.IsNullOrEmpty(context) ? "" : $" [{context}]";

                    // Include suppression count if errors were suppressed
                    string suppressedInfo = "";
                    if (_errorSuppressionCount.TryGetValue(errorKey, out int suppressed) && suppressed > 0)
                    {
                        suppressedInfo = $" (suppressed {suppressed} similar errors)";
                        _errorSuppressionCount[errorKey] = 0;
                    }

                    string logLine = $"[{timestamp}] [ERROR  ]{contextStr} Exception: {ex.GetType().Name} | Message: {ex.Message}{suppressedInfo}";

                    if (_logQueue.Count < 1000)
                    {
                        _logQueue.Enqueue(logLine);
                        if (ex.StackTrace != null)
                        {
                            _logQueue.Enqueue($"           Stack: {ex.StackTrace}");
                        }
                    }

                    // Update last error time
                    _lastErrorTime[errorKey] = currentTick;
                }
                _logEvent.Set();
            }
            catch
            {
                // Never crash due to logging
            }
        }

        /// <summary>
        /// Log a separator line for readability
        /// </summary>
        public static void Separator()
        {
            Write(LogLevel.Info, "----------------------------------------");
        }

        /// <summary>
        /// Clean up old error tracking entries to prevent memory leak
        /// Must be called while holding _lock
        /// </summary>
        private static void CleanupOldErrorEntries(long currentTick)
        {
            // Find and remove stale entries (older than 10x throttle time)
            long staleThreshold = currentTick - (ERROR_THROTTLE_TICKS * 10);
            List<string> keysToRemove = null;

            foreach (var kvp in _lastErrorTime)
            {
                if (kvp.Value < staleThreshold)
                {
                    if (keysToRemove == null)
                        keysToRemove = new List<string>();
                    keysToRemove.Add(kvp.Key);
                }
            }

            if (keysToRemove != null)
            {
                foreach (string key in keysToRemove)
                {
                    _lastErrorTime.Remove(key);
                    _errorSuppressionCount.Remove(key);
                }
            }
        }

        /// <summary>
        /// Log session information that a blind user cannot see visually
        /// Provides critical troubleshooting information for accessibility
        /// </summary>
        public static void LogSessionInfo(string modVersion, string shvdnVersion)
        {
            if (!_enabled || !_initialized) return;

            Info("=== SESSION INFORMATION ===");
            Info($"GTA11Y Version: {modVersion}");
            Info($"ScriptHookVDotNet Version: {shvdnVersion}");
            Info($"Operating System: {Environment.OSVersion}");
            Info($".NET Framework: {Environment.Version}");
            Info($"64-bit Process: {Environment.Is64BitProcess}");
            Info($"Processor Count: {Environment.ProcessorCount}");
            Info("===========================");
        }

        /// <summary>
        /// Log major state change for session tracking
        /// Helps blind users understand what's happening without visual feedback
        /// </summary>
        public static void LogStateChange(string feature, string state, string details = null)
        {
            if (!_enabled || !_initialized) return;

            string detailsStr = string.IsNullOrEmpty(details) ? "" : $" ({details})";
            Info($"STATE CHANGE: {feature} → {state}{detailsStr}");
        }

        /// <summary>
        /// Log resource load status (files, audio, etc.)
        /// Critical for diagnosing "silent failures" that blind users can't see
        /// </summary>
        public static void LogResourceLoad(string resourceType, string resourceName, bool success, string errorDetails = null)
        {
            if (!_enabled || !_initialized) return;

            if (success)
            {
                Info($"RESOURCE LOADED: {resourceType} - {resourceName}");
            }
            else
            {
                string error = string.IsNullOrEmpty(errorDetails) ? "Unknown error" : errorDetails;
                Error($"RESOURCE LOAD FAILED: {resourceType} - {resourceName} | Error: {error}");
            }
        }

        /// <summary>
        /// Log settings at session start for troubleshooting
        /// Allows blind users to verify their settings without visual confirmation
        /// </summary>
        public static void LogSettings(Dictionary<string, bool> settings, Dictionary<string, int> intSettings)
        {
            if (!_enabled || !_initialized || settings == null) return;

            Info("=== ACTIVE SETTINGS ===");

            // Log boolean settings
            if (settings != null)
            {
                foreach (var kvp in settings)
                {
                    if (kvp.Value)  // Only log enabled settings to reduce spam
                    {
                        Info($"  {kvp.Key}: ON");
                    }
                }
            }

            // Log int settings
            if (intSettings != null)
            {
                foreach (var kvp in intSettings)
                {
                    Info($"  {kvp.Key}: {kvp.Value}");
                }
            }

            Info("=======================");
        }

        /// <summary>
        /// Flush pending logs and shutdown the logger
        /// </summary>
        public static void Shutdown()
        {
            if (!_initialized) return;

            Info("=== GTA11Y Session Ended ===");
            Info($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Info("============================");

            lock (_lock)
            {
                if (!_initialized) return;

                _shutdownRequested = true;

                // Wake up the writer thread safely
                try
                {
                    _logEvent?.Set();
                }
                catch (ObjectDisposedException)
                {
                    // Already disposed
                }

                // Give it a moment to flush
                try
                {
                    if (_writerThread != null && _writerThread.IsAlive)
                    {
                        _writerThread.Join(2000);  // Wait up to 2 seconds
                    }
                }
                catch
                {
                    // Ignore thread join errors
                }

                // Dispose the AutoResetEvent to prevent resource leak
                try
                {
                    _logEvent?.Dispose();
                }
                catch
                {
                    // Ignore disposal errors
                }

                _initialized = false;
            }
        }
    }
}

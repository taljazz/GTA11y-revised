using System;
using DavyKager;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace GrandTheftAccessibility
{
    /// <summary>
    /// Manages all audio output including speech and spatial audio cues
    /// Optimized to minimize allocations and reuse audio resources
    /// </summary>
    public class AudioManager : IDisposable
    {
        // Audio output devices
        private readonly WaveOutEvent _pedTargetOut;
        private readonly WaveOutEvent _vehicleTargetOut;
        private readonly WaveOutEvent _propTargetOut;
        private readonly WaveOutEvent _altitudeOut;
        private readonly WaveOutEvent _pitchOut;

        // Audio file readers
        private readonly AudioFileReader _pedTargetSound;
        private readonly AudioFileReader _vehicleTargetSound;
        private readonly AudioFileReader _propTargetSound;

        // Signal generators for procedural audio - pre-created and reused
        private readonly SignalGenerator _altitudeGenerator;
        private readonly SignalGenerator _pitchGenerator;
        private bool _altitudeInitialized;
        private bool _pitchInitialized;

        // Timers for stopping altitude/pitch audio after duration
        private long _altitudeStopTick;
        private long _pitchStopTick;

        // Aircraft attitude audio - pre-created and reused to avoid allocations
        private readonly WaveOutEvent _aircraftPitchOut;
        private readonly WaveOutEvent _aircraftRollOut;
        private readonly SignalGenerator _aircraftPitchGenerator;
        private readonly SignalGenerator _aircraftRollGenerator;
        private readonly PanningSampleProvider _aircraftRollPanner;  // Reused - just update Pan property
        private bool _aircraftPitchInitialized;
        private bool _aircraftRollInitialized;

        // Timers for stopping audio after duration
        private long _aircraftPitchStopTick;
        private long _aircraftRollStopTick;

        // Landing beacon audio - triangle wave with stereo panning
        private readonly WaveOutEvent _beaconOut;
        private readonly SignalGenerator _beaconGenerator;
        private readonly PanningSampleProvider _beaconPanner;
        private bool _beaconInitialized;
        private long _beaconStopTick;

        private bool _disposed;

        // Tolk state tracking for resilience
        private bool _tolkLoaded;
        private int _tolkFailureCount;
        private long _lastTolkReconnectAttempt;
        private const int MAX_TOLK_FAILURES_BEFORE_RECONNECT = 3;
        private const long TOLK_RECONNECT_COOLDOWN = 50_000_000; // 5 seconds in ticks

        public AudioManager()
        {
            // Initialize screen reader support
            InitializeTolk();

            // Initialize output devices (these don't depend on files)
            _pedTargetOut = new WaveOutEvent();
            _vehicleTargetOut = new WaveOutEvent();
            _propTargetOut = new WaveOutEvent();
            _altitudeOut = new WaveOutEvent();
            _pitchOut = new WaveOutEvent();

            // Initialize audio file readers with error handling
            // Missing files shouldn't crash the mod
            try
            {
                if (System.IO.File.Exists(Constants.AUDIO_TPED_PATH))
                {
                    _pedTargetSound = new AudioFileReader(Constants.AUDIO_TPED_PATH);
                    _pedTargetOut.Init(_pedTargetSound);
                }
                else
                {
                    Logger.Warning($"Audio file not found: {Constants.AUDIO_TPED_PATH}");
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "Loading tped.wav");
            }

            try
            {
                if (System.IO.File.Exists(Constants.AUDIO_TVEHICLE_PATH))
                {
                    _vehicleTargetSound = new AudioFileReader(Constants.AUDIO_TVEHICLE_PATH);
                    _vehicleTargetOut.Init(_vehicleTargetSound);
                }
                else
                {
                    Logger.Warning($"Audio file not found: {Constants.AUDIO_TVEHICLE_PATH}");
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "Loading tvehicle.wav");
            }

            try
            {
                if (System.IO.File.Exists(Constants.AUDIO_TPROP_PATH))
                {
                    _propTargetSound = new AudioFileReader(Constants.AUDIO_TPROP_PATH);
                    _propTargetOut.Init(_propTargetSound);
                }
                else
                {
                    Logger.Warning($"Audio file not found: {Constants.AUDIO_TPROP_PATH}");
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "Loading tprop.wav");
            }

            // Initialize signal generators with default configuration
            _altitudeGenerator = new SignalGenerator
            {
                Gain = Constants.ALTITUDE_GAIN,
                Frequency = Constants.ALTITUDE_BASE_FREQUENCY,
                Type = SignalGeneratorType.Triangle
            };
            _pitchGenerator = new SignalGenerator
            {
                Gain = Constants.PITCH_GAIN,
                Frequency = Constants.PITCH_BASE_FREQUENCY,
                Type = SignalGeneratorType.Square
            };
            _altitudeInitialized = false;
            _pitchInitialized = false;

            // Initialize aircraft attitude audio with pre-created sample providers
            _aircraftPitchOut = new WaveOutEvent();
            _aircraftRollOut = new WaveOutEvent();

            // Pre-configure pitch generator (will update frequency before each play)
            _aircraftPitchGenerator = new SignalGenerator
            {
                Gain = Constants.AIRCRAFT_PITCH_GAIN,
                Frequency = Constants.AIRCRAFT_PITCH_BASE_FREQUENCY,
                Type = SignalGeneratorType.Sin
            };

            // Pre-configure roll generator and create panning chain ONCE
            _aircraftRollGenerator = new SignalGenerator
            {
                Gain = Constants.AIRCRAFT_ROLL_GAIN,
                Frequency = Constants.AIRCRAFT_ROLL_FREQUENCY,
                Type = SignalGeneratorType.SawTooth
            };

            // Create the panning chain once - reuse by updating Pan property
            var monoSignal = new StereoToMonoSampleProvider(_aircraftRollGenerator);
            _aircraftRollPanner = new PanningSampleProvider(monoSignal);

            _aircraftPitchInitialized = false;
            _aircraftRollInitialized = false;

            // Landing beacon - triangle wave with stereo panning for 3D navigation
            _beaconGenerator = new SignalGenerator
            {
                Gain = Constants.BEACON_GAIN,
                Frequency = Constants.BEACON_BASE_FREQUENCY,
                Type = SignalGeneratorType.Triangle
            };
            var beaconMono = new StereoToMonoSampleProvider(_beaconGenerator);
            _beaconPanner = new PanningSampleProvider(beaconMono);
            _beaconOut = new WaveOutEvent();
            _beaconInitialized = false;
        }

        /// <summary>
        /// Initialize or reinitialize Tolk screen reader support
        /// </summary>
        private void InitializeTolk()
        {
            try
            {
                Logger.Debug("Initializing Tolk...");
                Tolk.Load();
                _tolkLoaded = true;
                _tolkFailureCount = 0;
                Logger.Info("Tolk initialized successfully");
            }
            catch (Exception ex)
            {
                _tolkLoaded = false;
                Logger.Exception(ex, "Tolk Initialization");
            }
        }

        /// <summary>
        /// Attempt to reconnect Tolk if it has failed
        /// </summary>
        private void TryReconnectTolk()
        {
            long currentTick = DateTime.Now.Ticks;

            // Respect cooldown to avoid spam
            if (currentTick - _lastTolkReconnectAttempt < TOLK_RECONNECT_COOLDOWN)
                return;

            _lastTolkReconnectAttempt = currentTick;
            Logger.Info("Attempting to reconnect Tolk...");

            try
            {
                // Unload first if previously loaded
                if (_tolkLoaded)
                {
                    // Intentionally swallow exceptions - Tolk may already be in bad state
                    try { Tolk.Unload(); } catch { /* Expected during reconnect */ }
                }

                Tolk.Load();
                _tolkLoaded = true;
                _tolkFailureCount = 0;
                Logger.Info("Tolk reconnected successfully");
            }
            catch (Exception ex)
            {
                _tolkLoaded = false;
                Logger.Exception(ex, "Tolk Reconnect");
            }
        }

        /// <summary>
        /// Speak text through screen reader (non-interrupting by default)
        /// Includes automatic reconnection on failure
        /// </summary>
        public void Speak(string text, bool interrupt = false)
        {
            if (string.IsNullOrEmpty(text))
                return;

            try
            {
                if (!_tolkLoaded)
                {
                    Logger.Warning("Tolk not loaded, attempting reconnect...");
                    TryReconnectTolk();
                    if (!_tolkLoaded) return;
                }

                Tolk.Speak(text, interrupt);
                _tolkFailureCount = 0; // Reset on success
            }
            catch (Exception ex)
            {
                _tolkFailureCount++;
                Logger.Warning($"Tolk.Speak failed (attempt {_tolkFailureCount}): {ex.Message}");

                // Try reconnecting after multiple failures
                if (_tolkFailureCount >= MAX_TOLK_FAILURES_BEFORE_RECONNECT)
                {
                    Logger.Warning("Max Tolk failures reached, attempting reconnect...");
                    _tolkLoaded = false;
                    TryReconnectTolk();

                    // Retry speech once after reconnect
                    if (_tolkLoaded)
                    {
                        // Intentionally swallow - this is a best-effort retry, failure is acceptable
                        try { Tolk.Speak(text, interrupt); }
                        catch { /* Best-effort retry after reconnect */ }
                    }
                }
            }
        }

        /// <summary>
        /// Check Tolk health and reconnect if needed (call periodically from OnTick)
        /// </summary>
        public void CheckTolkHealth()
        {
            if (!_tolkLoaded || _tolkFailureCount >= MAX_TOLK_FAILURES_BEFORE_RECONNECT)
            {
                TryReconnectTolk();
            }
        }

        /// <summary>
        /// Play pedestrian target lock sound
        /// </summary>
        public void PlayPedTargetSound()
        {
            if (_disposed || _pedTargetSound == null || _pedTargetOut == null) return;
            try
            {
                _pedTargetOut.Stop();
                _pedTargetSound.Position = 0;
                _pedTargetOut.Play();
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "PlayPedTargetSound");
            }
        }

        /// <summary>
        /// Play vehicle target lock sound
        /// </summary>
        public void PlayVehicleTargetSound()
        {
            if (_disposed || _vehicleTargetSound == null || _vehicleTargetOut == null) return;
            try
            {
                _vehicleTargetOut.Stop();
                _vehicleTargetSound.Position = 0;
                _vehicleTargetOut.Play();
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "PlayVehicleTargetSound");
            }
        }

        /// <summary>
        /// Play prop target lock sound
        /// </summary>
        public void PlayPropTargetSound()
        {
            if (_disposed || _propTargetSound == null || _propTargetOut == null) return;
            try
            {
                _propTargetOut.Stop();
                _propTargetSound.Position = 0;
                _propTargetOut.Play();
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "PlayPropTargetSound");
            }
        }

        /// <summary>
        /// Play altitude indicator beep based on height
        /// OPTIMIZED: Reuses pre-created generator, only updates frequency
        /// </summary>
        public void PlayAltitudeIndicator(float heightAboveGround)
        {
            if (_disposed || _altitudeOut == null || _altitudeGenerator == null) return;

            try
            {
                _altitudeOut.Stop();

                // Update frequency only - gain and type are pre-set
                // Clamp frequency to reasonable range to avoid audio issues
                float frequency = (float)(Constants.ALTITUDE_BASE_FREQUENCY + (heightAboveGround * Constants.ALTITUDE_FREQUENCY_MULTIPLIER));
                if (frequency < 20f) frequency = 20f;
                if (frequency > 20000f) frequency = 20000f;
                _altitudeGenerator.Frequency = frequency;

                // Only init once - reuse thereafter
                if (!_altitudeInitialized)
                {
                    _altitudeOut.Init(_altitudeGenerator);
                    _altitudeInitialized = true;
                }

                _altitudeOut.Play();
                _altitudeStopTick = DateTime.Now.Ticks + (long)(Constants.ALTITUDE_DURATION_SECONDS * 10_000_000);
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "PlayAltitudeIndicator");
            }
        }

        /// <summary>
        /// Play pitch indicator beep based on camera pitch
        /// OPTIMIZED: Reuses pre-created generator, only updates frequency
        /// </summary>
        public void PlayPitchIndicator(float pitch)
        {
            if (_disposed || _pitchOut == null || _pitchGenerator == null) return;

            try
            {
                _pitchOut.Stop();

                // Update frequency only - gain and type are pre-set
                // Clamp frequency to reasonable range to avoid audio issues
                float frequency = (float)(Constants.PITCH_BASE_FREQUENCY + (pitch * Constants.PITCH_FREQUENCY_MULTIPLIER));
                if (frequency < 20f) frequency = 20f;
                if (frequency > 20000f) frequency = 20000f;
                _pitchGenerator.Frequency = frequency;

                // Only init once - reuse thereafter
                if (!_pitchInitialized)
                {
                    _pitchOut.Init(_pitchGenerator);
                    _pitchInitialized = true;
                }

                _pitchOut.Play();
                _pitchStopTick = DateTime.Now.Ticks + (long)(Constants.PITCH_DURATION_SECONDS * 10_000_000);
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "PlayPitchIndicator");
            }
        }

        /// <summary>
        /// Play aircraft pitch indicator (nose up/down)
        /// Higher frequency = nose up, lower frequency = nose down
        /// OPTIMIZED: Reuses pre-created generator, only updates frequency
        /// </summary>
        public void PlayAircraftPitchIndicator(float pitchDegrees)
        {
            if (_disposed || _aircraftPitchOut == null || _aircraftPitchGenerator == null) return;

            try
            {
                _aircraftPitchOut.Stop();

                // Update frequency only - gain and type are pre-set
                // Pitch: positive = nose up (higher freq), negative = nose down (lower freq)
                // Clamp frequency to reasonable range to avoid audio issues
                float frequency = (float)(Constants.AIRCRAFT_PITCH_BASE_FREQUENCY +
                    (pitchDegrees * Constants.AIRCRAFT_PITCH_FREQUENCY_MULTIPLIER));
                if (frequency < 20f) frequency = 20f;
                if (frequency > 20000f) frequency = 20000f;
                _aircraftPitchGenerator.Frequency = frequency;

                // Only init once - reuse thereafter
                if (!_aircraftPitchInitialized)
                {
                    _aircraftPitchOut.Init(_aircraftPitchGenerator);
                    _aircraftPitchInitialized = true;
                }

                _aircraftPitchOut.Play();
                _aircraftPitchStopTick = DateTime.Now.Ticks + (long)(Constants.AIRCRAFT_ROLL_DURATION_SECONDS * 10_000_000);
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "PlayAircraftPitchIndicator");
            }
        }

        /// <summary>
        /// Play aircraft roll indicator with stereo panning
        /// Roll left = sound pans left, roll right = sound pans right
        /// OPTIMIZED: Reuses pre-created panning chain, only updates Pan property
        /// </summary>
        public void PlayAircraftRollIndicator(float rollDegrees)
        {
            if (_disposed || _aircraftRollOut == null || _aircraftRollPanner == null) return;

            try
            {
                _aircraftRollOut.Stop();

                // Update pan only - panning chain is pre-created
                // Roll angle normalized to -1 (left) to 1 (right)
                float pan = Math.Max(-1f, Math.Min(1f, rollDegrees / 60f));
                _aircraftRollPanner.Pan = pan;

                // Only init once - reuse thereafter
                if (!_aircraftRollInitialized)
                {
                    _aircraftRollOut.Init(_aircraftRollPanner);
                    _aircraftRollInitialized = true;
                }

                _aircraftRollOut.Play();
                _aircraftRollStopTick = DateTime.Now.Ticks + (long)(Constants.AIRCRAFT_ROLL_DURATION_SECONDS * 10_000_000);
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "PlayAircraftRollIndicator");
            }
        }

        /// <summary>
        /// Play a landing beacon pulse with stereo panning and frequency modulation
        /// Pan encodes horizontal bearing, frequency encodes altitude difference
        /// </summary>
        public void PlayBeaconPulse(float pan, float frequency, float gainMultiplier)
        {
            if (_disposed || _beaconOut == null || _beaconGenerator == null) return;

            try
            {
                _beaconOut.Stop();

                // Update frequency (altitude encoding) - clamp to safe range
                if (frequency < Constants.BEACON_MIN_FREQUENCY) frequency = Constants.BEACON_MIN_FREQUENCY;
                else if (frequency > Constants.BEACON_MAX_FREQUENCY) frequency = Constants.BEACON_MAX_FREQUENCY;
                _beaconGenerator.Frequency = frequency;

                // Update volume (caller provides gain factor: 1.0 = full, BEHIND_GAIN_FACTOR when behind)
                _beaconGenerator.Gain = Constants.BEACON_GAIN * gainMultiplier;

                // Update pan (bearing encoding): -1 = left, 0 = center, +1 = right
                // Pan value is pre-clamped by caller
                _beaconPanner.Pan = pan;

                // Only init once - reuse thereafter
                if (!_beaconInitialized)
                {
                    _beaconOut.Init(_beaconPanner);
                    _beaconInitialized = true;
                }

                _beaconOut.Play();
                _beaconStopTick = DateTime.Now.Ticks + Constants.BEACON_PULSE_DURATION_TICKS;
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "AudioManager.PlayBeaconPulse");
            }
        }

        /// <summary>
        /// Stop the landing beacon audio immediately
        /// </summary>
        public void StopBeacon()
        {
            try { _beaconOut?.Stop(); } catch { /* Expected - audio device may be unavailable */ }
            _beaconStopTick = 0;
        }

        /// <summary>
        /// Call this periodically to stop indicator audio after duration
        /// Should be called from OnTick - handles all timed audio stops
        /// </summary>
        public void UpdateAircraftIndicators()
        {
            if (_disposed) return;

            try
            {
                long currentTick = DateTime.Now.Ticks;

                // Stop altitude indicator after duration
                // Bare catch intentional - Stop() may fail if audio device is gone, that's fine
                if (_altitudeStopTick > 0 && currentTick >= _altitudeStopTick)
                {
                    try { _altitudeOut?.Stop(); } catch { /* Expected - audio device may be unavailable */ }
                    _altitudeStopTick = 0;
                }

                // Stop pitch indicator after duration
                if (_pitchStopTick > 0 && currentTick >= _pitchStopTick)
                {
                    try { _pitchOut?.Stop(); } catch { /* Expected - audio device may be unavailable */ }
                    _pitchStopTick = 0;
                }

                // Stop aircraft pitch indicator after duration
                if (_aircraftPitchStopTick > 0 && currentTick >= _aircraftPitchStopTick)
                {
                    try { _aircraftPitchOut?.Stop(); } catch { /* Expected - audio device may be unavailable */ }
                    _aircraftPitchStopTick = 0;
                }

                // Stop aircraft roll indicator after duration
                if (_aircraftRollStopTick > 0 && currentTick >= _aircraftRollStopTick)
                {
                    try { _aircraftRollOut?.Stop(); } catch { /* Expected - audio device may be unavailable */ }
                    _aircraftRollStopTick = 0;
                }

                // Stop landing beacon pulse after duration
                if (_beaconStopTick > 0 && currentTick >= _beaconStopTick)
                {
                    try { _beaconOut?.Stop(); } catch { /* Expected - audio device may be unavailable */ }
                    _beaconStopTick = 0;
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "UpdateAircraftIndicators");
            }
        }

        /// <summary>
        /// Cleanup audio resources
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            _pedTargetOut?.Dispose();
            _vehicleTargetOut?.Dispose();
            _propTargetOut?.Dispose();
            _altitudeOut?.Dispose();
            _pitchOut?.Dispose();
            _aircraftPitchOut?.Dispose();
            _aircraftRollOut?.Dispose();

            try { _beaconOut?.Stop(); } catch { /* Expected during disposal */ }
            _beaconOut?.Dispose();

            _pedTargetSound?.Dispose();
            _vehicleTargetSound?.Dispose();
            _propTargetSound?.Dispose();

            // Safely unload Tolk - swallow exceptions during disposal
            if (_tolkLoaded)
            {
                try { Tolk.Unload(); } catch { /* Expected during disposal - Tolk or Logger may be unavailable */ }
                _tolkLoaded = false;
            }

            _disposed = true;
        }
    }
}

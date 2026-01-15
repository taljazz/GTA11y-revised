#region Imports
using DavyKager;
using GTA;
using GTA.Native;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
#endregion

namespace GrandTheftAccessibility
{
    class GTA11Y : Script
    {
        #region Fields
        private string currentWeapon;
        private string street;
        private string zone;
        private int wantedLevel;
        private float z;
        private float p;
        private bool timeAnnounced;
        private readonly Dictionary<string, string> hashes = new Dictionary<string, string>();
        private readonly bool[] keyState = new bool[20];
        private readonly Random random = new Random();
        private readonly List<Location> locations = new List<Location>();
        private readonly List<VehicleSpawn> spawns = new List<VehicleSpawn>();
        private long targetTicks;
        private long drivingTicks;
        private bool keys_disabled = false;
        private int locationMenuIndex = 0;
        private int spawnMenuIndex = 0;
        private int mainMenuIndex = 0;
        private readonly List<string> mainMenu = new List<string>();
        private int funMenuIndex = 0;
        private readonly List<string> funMenu = new List<string>();
        private int settingsMenuIndex = 0;
        private readonly AccessibilitySettings settings;
        private readonly WaveOutEvent out1, out2, out3, out11, out12;
        private readonly AudioFileReader tped, tvehicle, tprop;
        private readonly SignalGenerator alt, pitch;
        private readonly bool[] headings = new bool[8];
        private bool shifting = false;
        #endregion

        #region Enums
        private enum HeadingDirection
        {
            North = 0,
            NorthNortheast = 22,
            Northeast = 45,
            EastNortheast = 67,
            East = 90,
            EastSoutheast = 112,
            Southeast = 135,
            SouthSoutheast = 157,
            South = 180,
            SouthSouthwest = 202,
            Southwest = 225,
            WestSouthwest = 247,
            West = 270,
            WestNorthwest = 292,
            Northwest = 315,
            NorthNorthwest = 337
        }
        #endregion

        #region Constructor
        /// <summary>
        /// the main constructor for the program.
        /// </summary>
        public GTA11Y()
        {
            Tick += onTick;
            KeyUp += onKeyUp;
            KeyDown += onKeyDown;
            Tolk.Load();
            Tolk.Speak("Mod Ready");

            currentWeapon = Game.Player.Character.Weapons.Current.Hash.ToString();
            foreach (var line in File.ReadAllLines("scripts/hashes.txt"))
            {
                var result = line.Split('=');
                if (!hashes.ContainsKey(result[1]))
                    hashes.Add(result[1], result[0]);
            }

            locations.AddRange(new List<Location>
            {
                new Location("MICHAEL'S HOUSE", new GTA.Math.Vector3(-852.4f, 160.0f, 65.6f)),
                new Location("FRANKLIN'S HOUSE", new GTA.Math.Vector3(7.9f, 548.1f, 175.5f)),
                new Location("TREVOR'S TRAILER", new GTA.Math.Vector3(1985.7f, 3812.2f, 32.2f)),
                new Location("AIRPORT ENTRANCE", new GTA.Math.Vector3(-1034.6f, -2733.6f, 13.8f)),
                new Location("AIRPORT FIELD", new GTA.Math.Vector3(-1336.0f, -3044.0f, 13.9f)),
                new Location("ELYSIAN ISLAND", new GTA.Math.Vector3(338.2f, -2715.9f, 38.5f)),
                new Location("JETSAM", new GTA.Math.Vector3(760.4f, -2943.2f, 5.8f)),
                new Location("StripClub", new GTA.Math.Vector3(96.17191f, -1290.668f, 29.26874f)),
                new Location("ELBURRO HEIGHTS", new GTA.Math.Vector3(1384.0f, -2057.1f, 52.0f)),
                new Location("FERRIS WHEEL", new GTA.Math.Vector3(-1670.7f, -1125.0f, 13.0f)),
                new Location("CHUMASH", new GTA.Math.Vector3(-3192.6f, 1100.0f, 20.2f)),
                new Location("Altruist Cult Camp", new GTA.Math.Vector3(-1170.841f, 4926.646f, 224.295f)),
                new Location("Hippy Camp", new GTA.Math.Vector3(2476.712f, 3789.645f, 41.226f)),
                new Location("Far North San Andreas", new GTA.Math.Vector3(24.775f, 7644.102f, 19.055f)),
                new Location("Fort Zancudo", new GTA.Math.Vector3(-2047.4f, 3132.1f, 32.8f)),
                new Location("Fort Zancudo ATC Entrance", new GTA.Math.Vector3(-2344.373f, 3267.498f, 32.811f)),
                new Location("Playboy Mansion", new GTA.Math.Vector3(-1475.234f, 167.088f, 55.841f)),
                new Location("WINDFARM", new GTA.Math.Vector3(2354.0f, 1830.3f, 101.1f)),
                new Location("MCKENZIE AIRFIELD", new GTA.Math.Vector3(2121.7f, 4796.3f, 41.1f)),
                new Location("DESERT AIRFIELD", new GTA.Math.Vector3(1747.0f, 3273.7f, 41.1f)),
                new Location("CHILLIAD", new GTA.Math.Vector3(425.4f, 5614.3f, 766.5f)),
                new Location("Police Station", new GTA.Math.Vector3(436.491f, -982.172f, 30.699f)),
                new Location("Casino", new GTA.Math.Vector3(925.329f, 46.152f, 80.908f)),
                new Location("Vinewood sign", new GTA.Math.Vector3(711.362f, 1198.134f, 348.526f)),
                new Location("Blaine County Savings Bank", new GTA.Math.Vector3(-109.299f, 6464.035f, 31.627f)),
                new Location("LS Government Facility", new GTA.Math.Vector3(2522.98f, -384.436f, 92.9928f)),
                new Location("CHILIAD MOUNTAIN STATE WILDERNESS", new GTA.Math.Vector3(2994.917f, 2774.16f, 42.33663f)),
                new Location("Beaker's Garage", new GTA.Math.Vector3(116.3748f, 6621.362f, 31.6078f))
            });

            foreach (VehicleHash v in Enum.GetValues(typeof(VehicleHash)))
                spawns.Add(new VehicleSpawn(Game.GetLocalizedString(Function.Call<string>(Hash.GET_DISPLAY_NAME_FROM_VEHICLE_MODEL, v)), v));
            spawns.Sort();

            mainMenu.AddRange(new List<string> { "Teleport to location. ", "Spawn Vehicle. ", "Functions. ", "Settings. " });
            funMenu.AddRange(new List<string> { "Blow up all nearby vehicles", "Make all nearby pedestrians attack each other.", "instantly kill all nearby pedestrians.", "Raise Wanted Level. ", "Clear Wanted Level. " });

            tped = new AudioFileReader(@"scripts/tped.wav");
            tvehicle = new AudioFileReader(@"scripts/tvehicle.wav");
            tprop = new AudioFileReader(@"scripts/tprop.wav");
            out1 = new WaveOutEvent(); out1.Init(tped);
            out2 = new WaveOutEvent(); out2.Init(tvehicle);
            out3 = new WaveOutEvent(); out3.Init(tprop);
            out11 = new WaveOutEvent(); alt = new SignalGenerator(); out11.Init(alt);
            out12 = new WaveOutEvent(); pitch = new SignalGenerator(); out12.Init(pitch);

            settings = new AccessibilitySettings();
        }
        #endregion

        #region Methods
        private void onTick(object sender, EventArgs e)
        {
            if (Game.IsLoading) return;

            UpdateAltitude();
            UpdatePitch();
            UpdateWantedLevel();
            UpdateRadio();
            ApplyCheats();
            UpdateHeadings();
            AnnounceTime();
            UpdateLocation();
            UpdateWeapon();
            UpdateDrivingSpeed();
            UpdateTargetedEntity();
        }

        private void UpdateAltitude()
        {
            if (Math.Abs(Game.Player.Character.HeightAboveGround - z) > 1f)
            {
                z = Game.Player.Character.HeightAboveGround;
                if (settings["altitudeIndicator"] == 1)
                {
                    out11.Stop();
                    alt.Gain = 0.1;
                    alt.Frequency = 120 + (z * 40);
                    alt.Type = SignalGeneratorType.Triangle;
                    out11.Init(alt.Take(TimeSpan.FromSeconds(0.075)));
                    out11.Play();
                }
            }
        }

        private void UpdatePitch()
        {
            if (Math.Abs(GTA.GameplayCamera.RelativePitch - p) > 1f)
            {
                p = GTA.GameplayCamera.RelativePitch;
                if (settings["targetPitchIndicator"] == 1 && GTA.GameplayCamera.IsAimCamActive)
                {
                    out12.Stop();
                    pitch.Gain = 0.08;
                    pitch.Frequency = 600 + (p * 6);
                    pitch.Type = SignalGeneratorType.Square;
                    out12.Init(pitch.Take(TimeSpan.FromSeconds(0.025)));
                    out12.Play();
                }
            }
        }

        private void UpdateWantedLevel()
        {
            if (wantedLevel != Game.Player.WantedLevel)
            {
                wantedLevel = Game.Player.WantedLevel;
                if (settings["neverWanted"] != 1)
                    Tolk.Speak("Wanted level is now " + wantedLevel);
            }
        }

        private void UpdateRadio()
        {
            if (Game.Player.Character.CurrentVehicle != null)
                Game.Player.Character.CurrentVehicle.IsRadioEnabled = settings["radioOff"] != 1;
        }

        private void ApplyCheats()
        {
            Game.Player.IsInvincible = settings["godMode"] == 1;
            SetPlayerProperties(settings["godMode"] == 1);
            SetVehicleInvincibility(Game.Player.Character.CurrentVehicle, settings["vehicleGodMode"] == 1 && Game.Player.Character.IsInVehicle());
            SetVehicleInvincibility(Game.Player.Character.LastVehicle, settings["vehicleGodMode"] == 1 && !Game.Player.Character.IsInVehicle());
            Game.Player.IgnoredByPolice = settings["policeIgnore"] == 1;
            if (settings["neverWanted"] == 1) Game.Player.WantedLevel = 0;
            if (settings["infiniteAmmo"] == 1)
            {
                Game.Player.Character.Weapons.Current.InfiniteAmmoClip = true;
                Game.Player.Character.Weapons.Current.InfiniteAmmo = true;
            }
            else
            {
                Game.Player.Character.Weapons.Current.InfiniteAmmo = false;
                Game.Player.Character.Weapons.Current.InfiniteAmmoClip = false;
            }
            if (settings["exsplosiveAmmo"] == 1) Game.Player.SetExplosiveAmmoThisFrame();
            if (settings["fireAmmo"] == 1) Game.Player.SetFireAmmoThisFrame();
            if (settings["explosiveMelee"] == 1) Game.Player.SetExplosiveMeleeThisFrame();
            if (settings["superJump"] == 1) Game.Player.SetSuperJumpThisFrame();
            if (settings["runFaster"] == 1) Game.Player.SetRunSpeedMultThisFrame(2f);
            if (settings["swimFaster"] == 1) Game.Player.SetSwimSpeedMultThisFrame(2f);
        }

        private void SetPlayerProperties(bool isInvincible)
        {
            Game.Player.Character.CanBeDraggedOutOfVehicle = !isInvincible;
            Game.Player.Character.CanBeKnockedOffBike = !isInvincible;
            Game.Player.Character.CanBeShotInVehicle = !isInvincible;
            Game.Player.Character.CanFlyThroughWindscreen = !isInvincible;
            Game.Player.Character.DrownsInSinkingVehicle = !isInvincible;
        }

        private void SetVehicleInvincibility(Vehicle vehicle, bool isInvincible)
        {
            if (vehicle == null) return;
            vehicle.IsInvincible = isInvincible;
            vehicle.CanWheelsBreak = !isInvincible;
            vehicle.CanTiresBurst = !isInvincible;
            vehicle.CanBeVisiblyDamaged = !isInvincible;
            vehicle.IsBulletProof = isInvincible;
            vehicle.IsCollisionProof = isInvincible;
            vehicle.IsExplosionProof = isInvincible;
            vehicle.IsMeleeProof = isInvincible;
            vehicle.IsFireProof = isInvincible;
        }

        private void UpdateHeadings()
        {
            if (Game.Player.Character.IsFalling || Game.Player.Character.IsGettingIntoVehicle || Game.Player.Character.IsGettingUp || Game.Player.Character.IsProne || Game.Player.Character.IsRagdoll) return;
            double heading = Game.Player.Character.Heading;
            int slice = HeadingSlice(heading);
            if (!headings[slice])
            {
                Array.Clear(headings, 0, headings.Length);
                headings[slice] = true;
                if (settings["announceHeadings"] == 1)
                    Tolk.Speak(HeadingSliceName(heading), true);
            }
        }

        private void AnnounceTime()
        {
            TimeSpan t = World.CurrentTimeOfDay;
            if (t.Minutes == 0 && (t.Hours == 3 || t.Hours == 6 || t.Hours == 9 || t.Hours == 12 || t.Hours == 15 || t.Hours == 18 || t.Hours == 21) && !timeAnnounced)
            {
                timeAnnounced = true;
                if (settings["announceTime"] == 1)
                    Tolk.Speak("The time is now: " + t.Hours + ":00");
            }
            else if (t.Minutes != 0)
                timeAnnounced = false;
        }

        private void UpdateLocation()
        {
            string newStreet = World.GetStreetName(Game.Player.Character.Position);
            string newZone = World.GetZoneLocalizedName(Game.Player.Character.Position);
            if (street != newStreet)
            {
                street = newStreet;
                if (settings["announceZones"] == 1) Tolk.Speak(street);
            }
            if (zone != newZone)
            {
                zone = newZone;
                if (settings["announceZones"] == 1) Tolk.Speak(zone);
            }
        }

        private void UpdateWeapon()
        {
            string newWeapon = Game.Player.Character.Weapons.Current.Hash.ToString();
            if (currentWeapon != newWeapon)
            {
                currentWeapon = newWeapon;
                Tolk.Speak(currentWeapon);
            }
        }

        private void UpdateDrivingSpeed()
        {
            if (DateTime.Now.Ticks - drivingTicks > 25000000 && Game.Player.Character.CurrentVehicle != null && Game.Player.Character.CurrentVehicle.Speed > 1)
            {
                drivingTicks = DateTime.Now.Ticks;
                double speedMph = Game.Player.Character.CurrentVehicle.Speed * 2.23694;
                Tolk.Speak("" + Math.Round(speedMph) + " mph");
            }
        }

        private void UpdateTargetedEntity()
        {
            if (DateTime.Now.Ticks - targetTicks > 2000000 && Game.Player.TargetedEntity != null && Game.Player.Character.Weapons.Current.Hash != WeaponHash.HomingLauncher)
            {
                targetTicks = DateTime.Now.Ticks;
                var entity = Game.Player.TargetedEntity;
                if (entity.EntityType == EntityType.Ped && !entity.IsDead)
                {
                    out1.Stop(); tped.Position = 0; out1.Play();
                }
                else if (entity.EntityType == EntityType.Vehicle && !entity.IsDead)
                {
                    out2.Stop(); tvehicle.Position = 0; out2.Play();
                }
                else if (entity.EntityType == EntityType.Prop && (!entity.IsExplosionProof || !entity.IsBulletProof))
                {
                    out3.Stop(); tprop.Position = 0; out3.Play();
                }
            }
        }

        private void onKeyDown(object sender, KeyEventArgs e)
        {
            shifting = e.Control;

            if (e.KeyCode == Keys.NumPad2 && shifting && !keyState[2])
            {
                keyState[2] = true;
                keys_disabled = !keys_disabled;
                Tolk.Speak("Accessibility keys " + (keys_disabled ? "deactivated" : "activated") + ".");
            }

            if (keys_disabled) return;

            AnnounceNearbyEntities(World.GetNearbyVehicles(Game.Player.Character.Position, 50), 50, "Nearest Vehicles: ",
                v => v.IsVisible && !v.IsDead && (settings["onscreen"] == 0 || v.IsOnScreen) && v != Game.Player.Character.CurrentVehicle,
                v => (v.IsStopped ? "a stationary" : "a moving") + " " + v.LocalizedName, e, 0);

            AnnounceNearbyEntities(World.GetNearbyPeds(Game.Player.Character.Position, 50), 50, "Nearest Characters: ",
                p => hashes.ContainsKey(p.Model.NativeValue.ToString()) && p.IsVisible && !p.IsDead && (settings["onscreen"] == 0 || p.IsOnScreen) && !IsPlayerCharacter(p),
                p => hashes[p.Model.NativeValue.ToString()], e, 2);

            AnnounceNearbyEntities(World.GetNearbyProps(Game.Player.Character.Position, 50), 50, "Nearest Doors: ",
                p => hashes.ContainsKey(p.Model.NativeValue.ToString()) && p.IsVisible && !p.IsAttachedTo(Game.Player.Character) && (settings["onscreen"] == 0 || p.IsOnScreen) && (hashes[p.Model.NativeValue.ToString()].Contains("door") || hashes[p.Model.NativeValue.ToString()].Contains("gate")),
                p => hashes[p.Model.NativeValue.ToString()], e, 1);

            AnnounceNearbyEntities(World.GetNearbyProps(Game.Player.Character.Position, 50), 50, "Nearest Objects: ",
                p => hashes.ContainsKey(p.Model.NativeValue.ToString()) && p.IsVisible && !p.IsAttachedTo(Game.Player.Character) && (settings["onscreen"] == 0 || p.IsOnScreen) && (!hashes[p.Model.NativeValue.ToString()].Contains("door") && !hashes[p.Model.NativeValue.ToString()].Contains("gate")),
                p => hashes[p.Model.NativeValue.ToString()], e, 4);

            if (e.KeyCode == Keys.Decimal && !keyState[10])
            {
                keyState[10] = true;
                Tolk.Speak("facing " + GetDirectionName(Game.Player.Character.Heading));
            }

            if (e.KeyCode == Keys.NumPad0 && !keyState[0])
            {
                keyState[0] = true;
                if (e.Control)
                {
                    TimeSpan t = World.CurrentTimeOfDay;
                    Tolk.Speak("the time is: " + t.Hours + ":" + (t.Minutes < 10 ? "0" : "") + t.Minutes);
                }
                else
                {
                    Tolk.Speak(Game.Player.Character.CurrentVehicle == null
                        ? "Current location: " + street + ", " + zone + "."
                        : "Current location: Inside of a " + Game.Player.Character.CurrentVehicle.DisplayName + " at " + street + ".");
                }
            }

            if (e.KeyCode == Keys.NumPad2 && !shifting && !keyState[2])
            {
                GTA.Audio.PlaySoundFrontend("SELECT", "HUD_FRONTEND_DEFAULT_SOUNDSET");
                keyState[2] = true;
                ExecuteMenuAction();
            }

            NavigateMenu(e, Keys.NumPad1, true);
            NavigateMenu(e, Keys.NumPad3, false);
            NavigateMainMenu(e, Keys.NumPad7, true);
            NavigateMainMenu(e, Keys.NumPad9, false);
        }

        private void ExecuteMenuAction()
        {
            if (mainMenuIndex == 0)
            {
                if (Game.Player.Character.CurrentVehicle != null)
                    Game.Player.Character.CurrentVehicle.Position = locations[locationMenuIndex].Coordinates;
                else
                    Game.Player.Character.Position = locations[locationMenuIndex].Coordinates;
            }
            else if (mainMenuIndex == 1)
            {
                Vehicle vehicle = World.CreateVehicle(spawns[spawnMenuIndex].Id, Game.Player.Character.Position + Game.Player.Character.ForwardVector * 2.0f, Game.Player.Character.Heading + 90);
                vehicle.PlaceOnGround();
                if (settings["warpInsideVehicle"] == 1)
                    Game.Player.Character.SetIntoVehicle(vehicle, VehicleSeat.Driver);
            }
            else if (mainMenuIndex == 2)
            {
                ExecuteFunMenuAction();
            }
            else if (mainMenuIndex == 3)
            {
                settings.Toggle(settingsMenuIndex);
                Tolk.Speak(settings.DisplayName(settingsMenuIndex) + " " + (settings.Value(settingsMenuIndex) == 1 ? "On" : "Off") + "!");
                settings.Save();
            }
        }

        private void ExecuteFunMenuAction()
        {
            if (funMenuIndex == 0)
            {
                foreach (var v in World.GetNearbyVehicles(Game.Player.Character.Position, 100))
                {
                    if (!v.IsDead)
                    {
                        if (settings["vehicleGodMode"] == 0 && Game.Player.Character.CurrentVehicle == v)
                            SetVehicleInvincibility(v, false);
                        v.Explode();
                        v.MarkAsNoLongerNeeded();
                    }
                }
            }
            else if (funMenuIndex == 1)
            {
                var peds = World.GetNearbyPeds(Game.Player.Character.Position, 5000)
                    .Where(p => hashes.ContainsKey(p.Model.NativeValue.ToString()) && !p.IsDead && !IsPlayerCharacter(p)).ToList();
                if (peds.Count < 4)
                    Tolk.Speak("More nearby people are needed.");
                else
                {
                    for (int i = 0; i < peds.Count; i++)
                    {
                        int r = random.Next(0, peds.Count - 1);
                        while (r == i) r = random.Next(0, peds.Count - 1);
                        peds[i].Task.ClearAllImmediately();
                        peds[i].Weapons.Give(WeaponHash.APPistol, 1000, true, true);
                        peds[i].Task.FightAgainst(peds[r]);
                        peds[i].AlwaysKeepTask = true;
                        peds[i].BlockPermanentEvents = true;
                    }
                }
            }
            else if (funMenuIndex == 2)
            {
                foreach (var ped in World.GetNearbyPeds(Game.Player.Character.Position, 5000))
                    if (hashes.ContainsKey(ped.Model.NativeValue.ToString()) && !ped.IsDead && !IsPlayerCharacter(ped))
                        ped.Kill();
            }
            else if (funMenuIndex == 3)
            {
                if (Game.Player.WantedLevel < 5) Game.Player.WantedLevel++;
            }
            else if (funMenuIndex == 4)
            {
                Game.Player.WantedLevel = 0;
            }
        }

        private void NavigateMenu(KeyEventArgs e, Keys key, bool reverse)
        {
            if (e.KeyCode != key || keyState[(int)key - (int)Keys.NumPad0]) return;
            GTA.Audio.PlaySoundFrontend("NAV_LEFT_RIGHT", "HUD_FRONTEND_DEFAULT_SOUNDSET");
            keyState[(int)key - (int)Keys.NumPad0] = true;
            if (mainMenuIndex == 0)
                NavigateMenu(ref locationMenuIndex, locations, l => l.Name, reverse);
            else if (mainMenuIndex == 1)
                NavigateMenu(ref spawnMenuIndex, spawns, s => s.Name, reverse, shifting);
            else if (mainMenuIndex == 2)
                NavigateMenu(ref funMenuIndex, funMenu, f => f, reverse);
            else if (mainMenuIndex == 3)
                NavigateMenu(ref settingsMenuIndex, settings.SettingsList, s => s.DisplayName + " " + (s.Value == 1 ? "On" : "Off"), reverse);
        }

        private void NavigateMainMenu(KeyEventArgs e, Keys key, bool reverse)
        {
            if (e.KeyCode != key || keyState[(int)key - (int)Keys.NumPad0]) return;
            GTA.Audio.PlaySoundFrontend("NAV_UP_DOWN", "HUD_FRONTEND_DEFAULT_SOUNDSET");
            keyState[(int)key - (int)Keys.NumPad0] = true;
            NavigateMenu(ref mainMenuIndex, mainMenu, m => SpeakMenu(), reverse);
        }

        private void NavigateMenu<T>(ref int index, List<T> items, Func<T, string> getDescription, bool reverse, bool shift = false, int shiftAmount = 25)
        {
            int itemCount = items.Count;
            if (reverse)
            {
                if (shift && index >= shiftAmount)
                    index -= shiftAmount;
                else if (!shift && index > 0)
                    index--;
                else
                    index = itemCount - 1 - (shift ? index : 0);
            }
            else
            {
                if (shift && index < itemCount - shiftAmount - 1)
                    index += shiftAmount;
                else if (!shift && index < itemCount - 1)
                    index++;
                else
                    index = shift ? itemCount - 1 - (itemCount - 1 - index) : 0;
            }
            Tolk.Speak(getDescription(items[index]));
        }

        private void AnnounceNearbyEntities<T>(T[] entities, float radius, string prefix, Func<T, bool> isValid, Func<T, string> getName, KeyEventArgs e, int keyIndex) where T : Entity
        {
            if (e.KeyCode != Keys.NumPad4 + keyIndex || keyState[keyIndex]) return;
            keyState[keyIndex] = true;
            var results = new List<Result>();
            foreach (var entity in entities)
            {
                if (isValid(entity))
                {
                    double xyDistance = Math.Round(World.GetDistance(Game.Player.Character.Position, entity.Position) - Math.Abs(Game.Player.Character.Position.Z - entity.Position.Z), 1);
                    double zDistance = Math.Round(entity.Position.Z - Game.Player.Character.Position.Z, 1);
                    string direction = GetDirectionName(calculate_x_y_angle(Game.Player.Character.Position.X, Game.Player.Character.Position.Y, entity.Position.X, entity.Position.Y, 0));
                    results.Add(new Result(getName(entity), xyDistance, zDistance, direction));
                }
            }
            Tolk.Speak(ListToString(results, prefix));
        }

        private void onKeyUp(object sender, KeyEventArgs e)
        {
            shifting = e.Control;
            for (int i = 0; i <= 10; i++)
                if (e.KeyCode == Keys.NumPad0 + i && keyState[i])
                    keyState[i] = false;
            if (e.KeyCode == Keys.Decimal && keyState[10])
                keyState[10] = false;
        }

        private double calculate_x_y_angle(double x1, double y1, double x2, double y2, double deg)
        {
            double x = x1 - x2, y = y2 - y1;
            double rad = (x == 0 || y == 0) ? Math.Atan(0) : Math.Atan(y / x);
            double arctan = rad / Math.PI * 180;
            double fdeg = (x > 0) ? 90 - arctan : (x < 0) ? 270 - arctan : (y > 0) ? 0 : (y < 0) ? 180 : 0;
            fdeg = Math.Floor((fdeg - deg + 360) % 360);
            return fdeg;
        }

        private string GetDirectionName(double heading)
        {
            if (heading >= (int)HeadingDirection.North && heading < (int)HeadingDirection.NorthNortheast) return "north";
            if (heading >= (int)HeadingDirection.NorthNortheast && heading < (int)HeadingDirection.Northeast) return "north-northeast";
            if (heading >= (int)HeadingDirection.Northeast && heading < (int)HeadingDirection.EastNortheast) return "northeast";
            if (heading >= (int)HeadingDirection.EastNortheast && heading < (int)HeadingDirection.East) return "east-northeast";
            if (heading >= (int)HeadingDirection.East && heading < (int)HeadingDirection.EastSoutheast) return "east";
            if (heading >= (int)HeadingDirection.EastSoutheast && heading < (int)HeadingDirection.Southeast) return "east-southeast";
            if (heading >= (int)HeadingDirection.Southeast && heading < (int)HeadingDirection.SouthSoutheast) return "southeast";
            if (heading >= (int)HeadingDirection.SouthSoutheast && heading < (int)HeadingDirection.South) return "south-southeast";
            if (heading >= (int)HeadingDirection.South && heading < (int)HeadingDirection.SouthSouthwest) return "south";
            if (heading >= (int)HeadingDirection.SouthSouthwest && heading < (int)HeadingDirection.Southwest) return "south-southwest";
            if (heading >= (int)HeadingDirection.Southwest && heading < (int)HeadingDirection.WestSouthwest) return "west-southwest";
            if (heading >= (int)HeadingDirection.WestSouthwest && heading < (int)HeadingDirection.West) return "west";
            if (heading >= (int)HeadingDirection.West && heading < (int)HeadingDirection.WestNorthwest) return "west-northwest";
            if (heading >= (int)HeadingDirection.WestNorthwest && heading < (int)HeadingDirection.Northwest) return "northwest";
            if (heading >= (int)HeadingDirection.Northwest && heading < (int)HeadingDirection.NorthNorthwest) return "north-northeast";
            if (heading >= (int)HeadingDirection.NorthNorthwest) return "north-northeast";
            return "";
        }

        private int HeadingSlice(double heading)
        {
            if (heading >= 0 && heading < 45) return 0;
            if (heading >= 45 && heading < 90) return 1;
            if (heading >= 90 && heading < 135) return 2;
            if (heading >= 135 && heading < 180) return 3;
            if (heading >= 180 && heading < 225) return 4;
            if (heading >= 225 && heading < 270) return 5;
            if (heading >= 270 && heading < 315) return 6;
            if (heading >= 315) return 7;
            return -1;
        }

        private string HeadingSliceName(double heading)
        {
            int slice = HeadingSlice(heading);
            if (slice == 0) return "north";
            if (slice == 1) return "northwest";
            if (slice == 2) return "west";
            if (slice == 3) return "southwest";
            if (slice == 4) return "south";
            if (slice == 5) return "southeast";
            if (slice == 6) return "east";
            if (slice == 7) return "northeast";
            return "None";
        }

        private string ListToString(List<Result> results, string prependedText)
        {
            string text = prependedText;
            Result[] sortedResults = results.ToArray();
            Array.Sort(sortedResults);
            foreach (var i in sortedResults)
            {
                string vertical = "";
                if (i.ZDistance != 0)
                {
                    if (i.ZDistance > 0)
                        vertical = " " + Math.Abs(i.ZDistance) + " meters above, ";
                    else
                        vertical = " " + Math.Abs(i.ZDistance) + " meters below, ";
                }
                text = text + i.XYDistance + " meters " + i.Direction + ", " + vertical + i.Name + ". ";
            }
            return text;
        }

        private string SpeakMenu()
        {
            string result = mainMenu[mainMenuIndex];
            if (mainMenuIndex == 0)
                result += locations[locationMenuIndex].Name;
            else if (mainMenuIndex == 1)
                result += spawns[spawnMenuIndex].Name;
            else if (mainMenuIndex == 2)
                result += funMenu[funMenuIndex];
            else if (mainMenuIndex == 3)
                result += settings.DisplayName(settingsMenuIndex) + (settings.Value(settingsMenuIndex) == 1 ? "On" : "Off");
            Tolk.Speak(result, true);
            return result;
        }

        private bool IsPlayerCharacter(Ped ped)
        {
            string model = hashes[ped.Model.NativeValue.ToString()];
            return model == "player_one" || model == "player_two" || model == "player_zero";
        }
        #endregion

        #region Settings Class
        private class AccessibilitySettings
        {
            private readonly Dictionary<string, Setting> _settings = new Dictionary<string, Setting>();
            private readonly string _filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Rockstar Games/GTA V/ModSettings/gta11ySettings.json");
            public List<Setting> SettingsList { get { return _settings.Values.ToList(); } }

            public AccessibilitySettings()
            {
                LoadOrInitializeSettings();
            }

            public int this[string id] { get { return _settings[id].Value; } }
            public int this[int index] { get { return _settings.Values.ElementAt(index).Value; } }
            public string DisplayName(int index) { return _settings.Values.ElementAt(index).DisplayName; }
            public int Value(int index) { return _settings.Values.ElementAt(index).Value; }
            public void Toggle(int index)
            {
                Setting setting = _settings.Values.ElementAt(index);
                setting.Value = setting.Value == 1 ? 0 : 1;
            }
            public void Toggle(string id)
            {
                _settings[id].Value = _settings[id].Value == 1 ? 0 : 1;
            }
            public void Save()
            {
                Dictionary<string, int> dict = _settings.ToDictionary(k => k.Key, v => v.Value.Value);
                File.WriteAllText(_filePath, JsonConvert.SerializeObject(dict, Formatting.Indented));
            }

            private void LoadOrInitializeSettings()
            {
                var defaultSettings = new Dictionary<string, int>
                {
                    {"announceHeadings", 1}, {"announceZones", 1}, {"announceTime", 1}, {"altitudeIndicator", 1}, {"targetPitchIndicator", 1}, {"speed", 1},
                    {"radioOff", 0}, {"warpInsideVehicle", 0}, {"onscreen", 0}, {"godMode", 0}, {"policeIgnore", 0}, {"vehicleGodMode", 0},
                    {"infiniteAmmo", 0}, {"neverWanted", 0}, {"superJump", 0}, {"runFaster", 0}, {"swimFaster", 0}, {"exsplosiveAmmo", 0},
                    {"fireAmmo", 0}, {"explosiveMelee", 0}, {"autoAim", 0}
                };

                if (!Directory.Exists(Path.GetDirectoryName(_filePath)))
                    Directory.CreateDirectory(Path.GetDirectoryName(_filePath));
                if (!File.Exists(_filePath))
                    File.WriteAllText(_filePath, JsonConvert.SerializeObject(defaultSettings, Formatting.Indented));

                Dictionary<string, int> loaded = JsonConvert.DeserializeObject<Dictionary<string, int>>(File.ReadAllText(_filePath));
                if (loaded == null) loaded = defaultSettings;
                foreach (var kvp in defaultSettings)
                {
                    int value;
                    if (loaded.TryGetValue(kvp.Key, out value))
                        _settings[kvp.Key] = new Setting(kvp.Key, IdToName(kvp.Key), value);
                    else
                        _settings[kvp.Key] = new Setting(kvp.Key, IdToName(kvp.Key), kvp.Value);
                }
            }

            private static string IdToName(string id)
            {
                if (id == "godMode") return "God Mode. ;";
                if (id == "radioOff") return "Always Disable vehicle radios. ";
                if (id == "warpInsideVehicle") return "Teleport player inside newly spawned vehicles. ";
                if (id == "onscreen") return "Announce only visible nearby items. ";
                if (id == "speed") return "Announce current vehicle speed. ";
                if (id == "policeIgnore") return "Police Ignore Player. ";
                if (id == "vehicleGodMode") return "Make Current vehicle indestructable. ";
                if (id == "altitudeIndicator") return "audible Altitude Indicator. ";
                if (id == "targetPitchIndicator") return "audible Targetting Pitch Indicator. ";
                if (id == "infiniteAmmo") return "Unlimitted Ammo. ";
                if (id == "neverWanted") return "Wanted Level Never Increases. ";
                if (id == "superJump") return "Super Jump. ";
                if (id == "runFaster") return "Run Faster. ";
                if (id == "swimFaster") return "Fast Swimming. ";
                if (id == "exsplosiveAmmo") return "Explosive Ammo. ";
                if (id == "fireAmmo") return "Fire Ammo. ";
                if (id == "explosiveMelee") return "Explosive Melee. ";
                if (id == "announceTime") return "Time of Day Announcements. ";
                if (id == "announceHeadings") return "Heading Change Announcements. ";
                if (id == "announceZones") return "Street and Zone Change Announcements. ";
                return "None";
            }
        }
        #endregion
    }
}
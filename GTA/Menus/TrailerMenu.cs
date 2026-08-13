using System;
using GTA;
using GTA.Math;
using GTA.Native;

namespace GrandTheftAccessibility.Menus
{
    /// <summary>
    /// Hitches a trailer to whatever the player is driving.
    ///
    /// Coupling a trailer is normally a job done by eye: reverse the cab up to
    /// the trailer, judge the distance, and watch for it to latch. None of that
    /// is available here, so the mod does the whole manoeuvre - it puts the
    /// trailer down in the right place behind the vehicle, hitches it, and then
    /// says whether it actually took. The last part matters most: the game will
    /// silently decline to couple a trailer to a vehicle that has no hitch, and
    /// without a readback the player would be left guessing why nothing towed.
    /// </summary>
    public class TrailerMenu : MenuBase
    {
        #region Types

        private class TrailerOption
        {
            public string Name { get; }
            public VehicleHash Hash { get; }
            public string Description { get; }

            public TrailerOption(string name, VehicleHash hash, string description)
            {
                Name = name;
                Hash = hash;
                Description = description;
            }
        }

        #endregion

        #region Constants

        private const int ITEM_STATUS = 0;
        private const int ITEM_HITCH_NEAREST = 1;
        private const int ITEM_DETACH = 2;
        private const int FIXED_ITEM_COUNT = 3;

        /// <summary>
        /// How far behind the towing vehicle to place a spawned trailer. Far
        /// enough not to spawn inside it, close enough for the hitch to reach.
        /// </summary>
        private const float TRAILER_SPAWN_DISTANCE = 12f;

        /// <summary>Search radius the game is given to find the coupling.</summary>
        private const float HITCH_RADIUS = 30f;

        /// <summary>How far to look when hitching a trailer already in the world.</summary>
        private const float NEAREST_TRAILER_SEARCH = 60f;

        private static readonly TrailerOption[] Trailers =
        {
            new TrailerOption("Anti-aircraft trailer", VehicleHash.TrailerSmall2,
                "A towed anti-aircraft battery. A passenger mans the guns - the one trailer that fights back."),
            new TrailerOption("Container trailer", VehicleHash.Trailers2,
                "A flat trailer carrying a shipping container. Long and heavy."),
            new TrailerOption("Box trailer", VehicleHash.Trailers,
                "A standard enclosed articulated trailer. The ordinary lorry trailer."),
            new TrailerOption("Curtain-side trailer", VehicleHash.Trailers3,
                "An articulated trailer with soft sides."),
            new TrailerOption("Fuel tanker", VehicleHash.Tanker,
                "A tanker full of fuel. It explodes spectacularly when shot, which cuts both ways."),
            new TrailerOption("Army tanker", VehicleHash.ArmyTanker,
                "A military fuel tanker in olive drab. As explosive as the civilian one."),
            new TrailerOption("Army trailer", VehicleHash.ArmyTrailer,
                "A covered military supply trailer."),
            new TrailerOption("Flatbed trailer", VehicleHash.TRFlat,
                "A low flat trailer with no sides. Vehicles can be driven onto it."),
            new TrailerOption("Car transporter", VehicleHash.TR2,
                "A two-level trailer for carrying cars, with ramps at the back."),
            new TrailerOption("Log trailer", VehicleHash.TrailerLogs,
                "A skeletal trailer stacked with tree trunks."),
            new TrailerOption("Grain trailer", VehicleHash.GrainTrailer,
                "A farm trailer with high sides for loose crops."),
            new TrailerOption("Hay bale trailer", VehicleHash.BaleTrailer,
                "A farm trailer loaded with round hay bales."),
            new TrailerOption("Boat trailer", VehicleHash.BoatTrailer,
                "A small trailer with a cradle for a boat."),
            new TrailerOption("Small utility trailer", VehicleHash.TrailerSmall,
                "A short two-wheeled trailer. The lightest option, and the easiest to tow."),
            new TrailerOption("Mobile operations centre trailer", VehicleHash.TrailerLarge,
                "The enormous command trailer from the online Mobile Operations Centre. Very long.")
        };

        #endregion

        #region MenuBase Overrides

        public TrailerMenu(AudioManager audio) : base(audio)
        {
        }

        protected override int ItemCount => FIXED_ITEM_COUNT + Trailers.Length;

        protected override string GetItemText(int index)
        {
            switch (index)
            {
                case ITEM_STATUS:
                    return DescribeStatus();
                case ITEM_HITCH_NEAREST:
                    return "Hitch the nearest trailer already here";
                case ITEM_DETACH:
                    return "Unhitch the trailer";
            }

            int trailerIndex = index - FIXED_ITEM_COUNT;
            if (trailerIndex < 0 || trailerIndex >= Trailers.Length)
                return EmptyMenuText;

            TrailerOption option = Trailers[trailerIndex];
            return $"Bring a {option.Name}: {option.Description}";
        }

        protected override void OnItemActivated(int index)
        {
            switch (index)
            {
                case ITEM_STATUS:
                    Speak(DescribeStatus());
                    return;
                case ITEM_HITCH_NEAREST:
                    HitchNearest();
                    return;
                case ITEM_DETACH:
                    Detach();
                    return;
            }

            int trailerIndex = index - FIXED_ITEM_COUNT;
            if (trailerIndex < 0 || trailerIndex >= Trailers.Length)
                return;

            SpawnAndHitch(Trailers[trailerIndex]);
        }

        public override string GetMenuName()
        {
            return "Trailers";
        }

        #endregion

        #region Hitching

        /// <summary>
        /// Put a trailer on the road behind the vehicle and couple it.
        /// </summary>
        private void SpawnAndHitch(TrailerOption option)
        {
            Vehicle towing = CurrentVehicle;
            if (towing == null)
            {
                Speak("Get into the vehicle you want to tow with first.");
                return;
            }

            try
            {
                if (towing.IsAttachedToTrailer)
                {
                    Speak("Something is already hitched. Unhitch it first.");
                    return;
                }

                var model = new Model(option.Hash);
                if (!model.IsValid || !model.IsInCdImage)
                {
                    Speak($"The {option.Name} is not in this copy of the game.");
                    return;
                }

                // Directly behind, facing the same way, so the hitch lines up
                // without the player having to manoeuvre
                Vector3 behind = towing.Position - (towing.ForwardVector * TRAILER_SPAWN_DISTANCE);

                Vehicle trailer = Vehicle.Create(option.Hash, behind, towing.Heading);
                if (trailer == null)
                {
                    Logger.Warning($"TRAILER|spawn failed|{option.Hash}");
                    Speak($"Could not bring the {option.Name}. The area may be too busy.");
                    return;
                }

                trailer.PlaceOnGround();
                model.MarkAsNoLongerNeeded();

                AttachAndReport(towing, trailer, option.Name);
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "TrailerMenu.SpawnAndHitch");
                Speak("Failed to bring the trailer.");
            }
        }

        /// <summary>
        /// Couple a trailer that is already in the world - the one the player
        /// has driven up to, or that was left behind earlier.
        /// </summary>
        private void HitchNearest()
        {
            Vehicle towing = CurrentVehicle;
            if (towing == null)
            {
                Speak("Get into the vehicle you want to tow with first.");
                return;
            }

            try
            {
                if (towing.IsAttachedToTrailer)
                {
                    Speak("Something is already hitched. Unhitch it first.");
                    return;
                }

                Vehicle nearest = null;
                float nearestDistance = float.MaxValue;

                foreach (Vehicle candidate in World.GetNearbyVehicles(towing.Position, NEAREST_TRAILER_SEARCH))
                {
                    if (candidate == null || !candidate.Exists() || candidate == towing)
                        continue;
                    if (!candidate.IsTrailer)
                        continue;

                    float distance = towing.Position.DistanceTo(candidate.Position);
                    if (distance >= nearestDistance)
                        continue;

                    nearestDistance = distance;
                    nearest = candidate;
                }

                if (nearest == null)
                {
                    Speak($"No trailer within {(int)NEAREST_TRAILER_SEARCH} metres. " +
                          "Choose one from this menu instead and it will be brought to you.");
                    return;
                }

                AttachAndReport(towing, nearest,
                    $"trailer {(int)nearestDistance} metres away");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "TrailerMenu.HitchNearest");
                Speak("Failed to hitch the trailer.");
            }
        }

        /// <summary>
        /// Do the coupling and then CHECK it, because the game does not complain
        /// when it refuses. A vehicle without a hitch - most cars, and some of
        /// the big ones people assume can tow - simply stays uncoupled, and the
        /// player has no way to see that.
        /// </summary>
        private void AttachAndReport(Vehicle towing, Vehicle trailer, string trailerName)
        {
            Function.Call(Hash.ATTACH_VEHICLE_TO_TRAILER, towing.Handle, trailer.Handle, 1.0f);

            bool attached = false;
            try { attached = towing.IsAttachedToTrailer; }
            catch { }

            if (attached)
            {
                // Legs up, or they drag along the road
                try { trailer.SetTrailerLegsRaised(); } catch { }

                Logger.Info($"TRAILER|hitched|{trailer.Model.Hash}|to={towing.Model.Hash}");
                Speak($"Hitched the {trailerName}. It will follow you. " +
                      "Take corners wide and brake early - it is heavy and it swings.");
                return;
            }

            Logger.Info($"TRAILER|refused|{trailer.Model.Hash}|to={towing.Model.Hash}");
            Speak($"This vehicle will not take a trailer - it has no hitch. " +
                  "Try a lorry cab like the Phantom or Hauler, or the Apocalypse Cerberus. " +
                  "The trailer has been left behind you.");
        }

        private void Detach()
        {
            Vehicle towing = CurrentVehicle;
            if (towing == null)
            {
                Speak("You are not in a vehicle.");
                return;
            }

            try
            {
                if (!towing.IsAttachedToTrailer)
                {
                    Speak("Nothing is hitched.");
                    return;
                }

                Vehicle trailer = towing.TrailerVehicle;

                Function.Call(Hash.DETACH_VEHICLE_FROM_TRAILER, towing.Handle);

                // Legs down so it stands rather than dropping on its nose
                if (trailer != null && trailer.Exists())
                {
                    try { trailer.SetTrailerLegsLowered(); } catch { }
                }

                Logger.Info("TRAILER|unhitched");
                Speak("Unhitched. The trailer is standing behind you.");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "TrailerMenu.Detach");
                Speak("Failed to unhitch.");
            }
        }

        #endregion

        #region Helpers

        private static string DescribeStatus()
        {
            Vehicle towing = CurrentVehicle;
            if (towing == null)
                return "Trailer status: you are not in a vehicle";

            try
            {
                if (!towing.IsAttachedToTrailer)
                    return "Trailer status: nothing hitched";

                Vehicle trailer = towing.TrailerVehicle;
                if (trailer == null || !trailer.Exists())
                    return "Trailer status: something is hitched";

                return $"Trailer status: towing a {VehicleDescriber.GetShortDescription(trailer.Model)}";
            }
            catch
            {
                return "Trailer status: unavailable";
            }
        }

        private static Vehicle CurrentVehicle
        {
            get
            {
                try
                {
                    Ped player = Game.Player?.Character;
                    if (player == null || !player.Exists() || !player.IsInVehicle())
                        return null;

                    Vehicle vehicle = player.CurrentVehicle;
                    return vehicle != null && vehicle.Exists() ? vehicle : null;
                }
                catch
                {
                    return null;
                }
            }
        }

        #endregion
    }
}

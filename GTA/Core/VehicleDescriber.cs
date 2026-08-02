using System;
using System.Collections.Generic;
using GTA;
using GTA.Native;

namespace GrandTheftAccessibility
{
    /// <summary>
    /// Describes vehicles and their upgrades for a player who cannot look at them.
    ///
    /// Two layers, deliberately separated:
    ///
    /// 1. FACTS the game itself supplies - manufacturer, class, seats, and the
    ///    handling figures the game stores per model. These cover every vehicle
    ///    in the game and cannot be wrong, because they are read out of the
    ///    game's own data rather than remembered by anyone.
    ///
    /// 2. PROSE written by hand for vehicles worth describing in more detail:
    ///    what it looks like, what it is based on, what it is good for. This
    ///    covers a fraction of the roster ON PURPOSE. A confidently wrong
    ///    description is worse than none, so anything not in the table simply
    ///    falls back to layer one instead of being padded out with guesswork.
    /// </summary>
    public static class VehicleDescriber
    {
        #region Curated Descriptions

        /// <summary>
        /// Hand-written notes, keyed by the game's internal model name in lower
        /// case. Only vehicles that can be described accurately belong here.
        /// </summary>
        private static readonly Dictionary<string, string> Descriptions =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Super
            { "adder", "A wide, low two-seat hypercar with a teardrop roofline, based on the Bugatti Veyron. One of the fastest in a straight line, but heavy and reluctant to change direction." },
            { "zentorno", "An angular wedge-shaped supercar with scissor doors and a huge rear wing, in the mould of a Lamborghini Sesto Elemento. Very fast and very twitchy." },
            { "t20", "A smooth, rounded hypercar with a low nose and an active rear wing, along the lines of a McLaren P1. One of the best all-round supercars: fast and unusually forgiving." },
            { "osiris", "A compact hypercar with a rounded cabin and butterfly doors, resembling a Pagani Huayra. Strong grip and easy to place." },
            { "entityxf", "A long, flat-nosed supercar in the style of a Koenigsegg CCX. Immensely fast in a straight line, poor at slow corners." },
            { "vacca", "A sharp-edged wedge supercar resembling a Lamborghini Gallardo. Loud, bright, and slides easily." },
            { "infernus", "A classic wedge supercar with pop-up-era styling, a long-running GTA staple. Mid-pack now but still quick." },
            { "cheetah", "A low, wide 1980s-style supercar with side strakes, in the mould of a Ferrari Testarossa. Fast but tail-happy." },

            // Sports
            { "elegy2", "A four-door-look sports coupe based on the Nissan GT-R, with all-wheel drive. Grips hard and is very hard to spin - one of the friendliest fast cars to drive." },
            { "comet2", "A rear-engined two-seat sports car unmistakably based on the Porsche 911. Quick, compact, and prone to swapping ends if you lift off mid-corner." },
            { "banshee", "A long-nosed American sports car in the mould of a Dodge Viper. Enormous power, no traction to speak of." },
            { "sultanrs", "A boxy rally-bred saloon with a huge wing and all-wheel drive, based on the Subaru Impreza. Superb grip, especially off tarmac." },
            { "kuruma", "An unremarkable-looking four-door sports saloon. The armoured version is a favourite because its windows stop bullets." },
            { "jester", "A sleek Japanese coupe with a low nose, along the lines of a Honda NSX. Balanced and undemanding." },
            { "feltzer2", "A sharp-edged German roadster resembling a Mercedes SLS. Fast, heavy, and stable." },

            // Muscle
            { "dominator", "A long-bonneted American muscle car based on the Ford Mustang. Loud, fast in a straight line, and happy to spin its wheels." },
            { "sabregt", "A 1970s American muscle coupe with a squared-off body. Slow to stop, very slidey, enormously characterful." },
            { "gauntlet", "A blunt-nosed muscle car in the mould of a Dodge Challenger. Straight-line pace, poor brakes." },
            { "voodoo", "A low-riding 1960s American cruiser with soft suspension. Slow, floaty, and built for style over speed." },

            // Off-road and utility
            { "sanchez", "A lightweight dirt bike. The best vehicle in the game for rough ground, steep hills and narrow trails." },
            { "rebel2", "A rusted, raised pickup with knobbly tyres. Slow on tarmac, excellent in mud and sand." },
            { "sandking", "A vast raised monster pickup on huge tyres. Crushes rough terrain and small cars alike, but rolls over easily." },
            { "dubsta3", "A boxy off-road wagon on enormous wheels, based on the six-wheel Mercedes G-Wagen. Very tall, very stable, very slow to turn." },

            // Emergency and service
            { "police", "A marked police cruiser. Fast enough to keep up with traffic, and driving one does not stop the police reacting to you." },
            { "ambulance", "A tall box-bodied ambulance. Slow and top-heavy, but it carries four and its siren clears traffic." },
            { "firetruk", "A very large fire engine with a working water cannon operated from the roof. Extremely heavy and slow to stop." },
            { "taxi", "A yellow city cab. Unremarkable to drive, and pedestrians will treat you as a taxi if you stop." },

            // Aircraft
            { "buzzard2", "A small, light two-seat helicopter without weapons. Nimble and easy to land in tight spaces - the usual choice for getting around quickly." },
            { "maverick", "A conventional four-seat civilian helicopter. Stable, forgiving, and the best helicopter to learn on." },
            { "titan", "A large four-engine military transport plane with a rear ramp. Slow, extremely stable, and easy to land - a good first aircraft." },
            { "velum", "A small twin-propeller light aircraft with retractable gear. Modest speed and gentle handling." },
            { "dodo", "A small seaplane that can land on water as well as tarmac. Very slow and very forgiving." },
            { "lazer", "A supersonic military fighter jet. Enormously fast, armed, and unforgiving - it lands at high speed and needs a long runway." },
            { "cargobob", "A huge twin-rotor transport helicopter that can pick up vehicles with a winch. Slow and cumbersome." },

            // Boats
            { "dinghy", "A small inflatable boat with an outboard motor. Light, quick, and easy to beach." },
            { "marquis", "A large sailing yacht with a mast. Slow under power and mostly a novelty." },
            { "submersible", "A tiny two-person submarine. Very slow, but it can reach the sea floor safely." }
        };

        #endregion

        #region Upgrade Guide

        /// <summary>
        /// What each upgrade category actually does. Unlike the vehicle notes,
        /// this is complete: the effects are the same on every car, so there is
        /// nothing to guess at.
        /// </summary>
        private static readonly Dictionary<VehicleModType, string> UpgradeEffects =
            new Dictionary<VehicleModType, string>
        {
            { VehicleModType.Engine, "Engine. Raises power, so the car accelerates harder and reaches a higher top speed. The single most useful performance upgrade." },
            { VehicleModType.Brakes, "Brakes. Shortens stopping distance. Worth having on anything fast, and the upgrade you notice most in traffic." },
            { VehicleModType.Transmission, "Transmission. Quicker gear changes, so acceleration improves without changing top speed." },
            { VehicleModType.Suspension, "Suspension. Lowers the car and stiffens it. Improves cornering grip, but a fully lowered car will ground out on kerbs and steep driveways." },
            { VehicleModType.Armor, "Armour. Absorbs damage from collisions and gunfire. Adds weight, so acceleration and braking suffer slightly." },
            { VehicleModType.Horns, "Horn. Purely the sound the horn makes. No effect on how the car drives." },
            { VehicleModType.Spoilers, "Spoiler. A wing on the boot. Adds a small amount of grip at high speed; mostly cosmetic." },
            { VehicleModType.FrontBumper, "Front bumper. Cosmetic - changes the shape of the nose." },
            { VehicleModType.RearBumper, "Rear bumper. Cosmetic - changes the shape of the tail." },
            { VehicleModType.SideSkirt, "Side skirts. Cosmetic panels along the sills between the wheels." },
            { VehicleModType.Exhaust, "Exhaust. Changes the tailpipes and the engine note. Cosmetic, but you will hear it." },
            { VehicleModType.Frame, "Roll cage or frame. Cosmetic on most cars." },
            { VehicleModType.Grille, "Grille. Cosmetic - the panel at the front of the bonnet." },
            { VehicleModType.Hood, "Bonnet. Cosmetic - may add scoops or vents." },
            { VehicleModType.Fender, "Wings. Cosmetic panels over the wheels." },
            { VehicleModType.Roof, "Roof. Cosmetic - scoops, vents or a different roofline." },
            { VehicleModType.FrontWheel, "Front wheels. Changes the wheel design, and on most vehicles both axles. Wheels can slightly affect grip." },
            { VehicleModType.RearWheel, "Rear wheels. On bikes, the back wheel only." },
            { VehicleModType.PlateHolder, "Number plate holder. Cosmetic." },
            { VehicleModType.VanityPlates, "Number plate style. Cosmetic - changes the plate design." },
            { VehicleModType.TrimDesign, "Interior trim. Cosmetic, and only visible from inside." },
            { VehicleModType.Ornaments, "Ornaments. Small cosmetic details such as a hanging charm." },
            { VehicleModType.Dashboard, "Dashboard. Cosmetic interior detail." },
            { VehicleModType.DialDesign, "Dials. Cosmetic instrument faces." },
            { VehicleModType.DoorSpeakers, "Door speakers. Cosmetic interior detail." },
            { VehicleModType.Seats, "Seats. Cosmetic - racing seats or different upholstery." },
            { VehicleModType.SteeringWheels, "Steering wheel. Cosmetic." },
            { VehicleModType.ColumnShifterLevers, "Gear lever. Cosmetic." },
            { VehicleModType.Plaques, "Plaques. Cosmetic interior badges." },
            { VehicleModType.Speakers, "Speakers. Cosmetic boot-mounted audio." },
            { VehicleModType.Trunk, "Boot. Cosmetic." },
            { VehicleModType.Hydraulics, "Hydraulics. Lets the car bounce and lean on command. A lowrider feature - no performance benefit." },
            { VehicleModType.EngineBlock, "Engine block. Cosmetic detail under the bonnet." },
            { VehicleModType.AirFilter, "Air filter. Cosmetic detail under the bonnet." },
            { VehicleModType.Struts, "Strut brace. Cosmetic detail under the bonnet." },
            { VehicleModType.ArchCover, "Arch covers. Cosmetic wheel arch extensions." },
            { VehicleModType.Aerials, "Aerials. Cosmetic." },
            { VehicleModType.Trim, "Trim. Cosmetic." },
            { VehicleModType.Tank, "Tank. Cosmetic - fuel tank styling, mostly on bikes." },
            { VehicleModType.Windows, "Window tint. Darkens the glass. Cosmetic, though heavy tint makes you harder to identify." },
            { VehicleModType.Livery, "Livery. A painted design over the bodywork. Cosmetic." }
        };

        #endregion

        #region Public API

        /// <summary>
        /// One short line for browsing a list: class and seat count. Kept brief
        /// on purpose - this is spoken for every item as the player scrolls.
        /// </summary>
        public static string GetShortDescription(Model model)
        {
            try
            {
                string vehicleClass = DescribeClass(model);
                int seats = GetSeatCount(model);

                if (seats > 0)
                    return $"{vehicleClass}, {seats} {(seats == 1 ? "seat" : "seats")}";

                return vehicleClass;
            }
            catch
            {
                return "vehicle";
            }
        }

        /// <summary>
        /// The full picture: manufacturer, class, seats, how it handles, and the
        /// hand-written note when there is one.
        /// </summary>
        public static string GetFullDescription(Model model)
        {
            try
            {
                var parts = new List<string>();

                string make = GetMakeName(model);
                string name = GetDisplayName(model);
                parts.Add(string.IsNullOrEmpty(make) ? name : $"{make} {name}");

                parts.Add(GetShortDescription(model));

                string handling = DescribeHandling(model);
                if (!string.IsNullOrEmpty(handling))
                    parts.Add(handling);

                string note = GetCuratedNote(model);
                if (!string.IsNullOrEmpty(note))
                    parts.Add(note);

                return string.Join(". ", parts);
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "VehicleDescriber.GetFullDescription");
                return "Description unavailable.";
            }
        }

        /// <summary>The hand-written note for a model, or null if there is none.</summary>
        public static string GetCuratedNote(Model model)
        {
            try
            {
                string internalName;
                if (!HashManager.TryGetName(model.Hash, out internalName) || string.IsNullOrEmpty(internalName))
                    return null;

                string note;
                return Descriptions.TryGetValue(internalName, out note) ? note : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Every upgrade category, in menu order.</summary>
        public static List<VehicleModType> GetUpgradeTypes()
        {
            return new List<VehicleModType>(UpgradeEffects.Keys);
        }

        /// <summary>What a given upgrade category does.</summary>
        public static string GetUpgradeEffect(VehicleModType type)
        {
            string effect;
            return UpgradeEffects.TryGetValue(type, out effect)
                ? effect
                : $"{type}. No description available.";
        }

        /// <summary>How many vehicles carry a hand-written note.</summary>
        public static int CuratedCount => Descriptions.Count;

        #endregion

        #region Game-derived Facts

        /// <summary>
        /// Plain-language vehicle class. Falls back to the shape of the model
        /// when the class lookup is not helpful.
        /// </summary>
        private static string DescribeClass(Model model)
        {
            try
            {
                if (model.IsBicycle) return "bicycle";
                if (model.IsMotorcycle || model.IsBike) return "motorcycle";
                if (model.IsQuadBike) return "quad bike";
                if (model.IsHelicopter) return "helicopter";
                if (model.IsPlane) return "aeroplane";
                if (model.IsSubmarine || model.IsSubmarineCar) return "submarine";
                if (model.IsBoat) return "boat";
                if (model.IsTrain) return "train";
                if (model.IsTrailer) return "trailer";

                VehicleClass vehicleClass = (VehicleClass)Function.Call<int>(
                    Hash.GET_VEHICLE_CLASS_FROM_NAME, model.Hash);

                switch (vehicleClass)
                {
                    case VehicleClass.Compacts: return "compact car";
                    case VehicleClass.Sedans: return "saloon";
                    case VehicleClass.SUVs: return "SUV";
                    case VehicleClass.Coupes: return "coupe";
                    case VehicleClass.Muscle: return "muscle car";
                    case VehicleClass.SportsClassics: return "classic sports car";
                    case VehicleClass.Sports: return "sports car";
                    case VehicleClass.Super: return "supercar";
                    case VehicleClass.OffRoad: return "off-roader";
                    case VehicleClass.Industrial: return "industrial vehicle";
                    case VehicleClass.Utility: return "utility vehicle";
                    case VehicleClass.Vans: return "van";
                    case VehicleClass.Service: return "service vehicle";
                    case VehicleClass.Emergency: return "emergency vehicle";
                    case VehicleClass.Military: return "military vehicle";
                    case VehicleClass.Commercial: return "commercial vehicle";
                    case VehicleClass.OpenWheel: return "open-wheel racer";
                    default: return "vehicle";
                }
            }
            catch
            {
                return "vehicle";
            }
        }

        /// <summary>
        /// Turn the game's own handling figures into something meaningful. These
        /// are read from the model, so they are true for every vehicle including
        /// ones nobody has written a note for.
        /// </summary>
        private static string DescribeHandling(Model model)
        {
            try
            {
                float acceleration = Function.Call<float>(Hash.GET_VEHICLE_MODEL_ACCELERATION, model.Hash);
                float braking = Function.Call<float>(Hash.GET_VEHICLE_MODEL_MAX_BRAKING, model.Hash);
                float traction = Function.Call<float>(Hash.GET_VEHICLE_MODEL_MAX_TRACTION, model.Hash);

                if (acceleration <= 0f && braking <= 0f && traction <= 0f)
                    return null;

                return $"Acceleration {Rate(acceleration, 0.25f, 0.35f, 0.45f)}, " +
                       $"braking {Rate(braking, 0.6f, 0.8f, 1.0f)}, " +
                       $"grip {Rate(traction, 1.6f, 2.0f, 2.3f)}";
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Bucket a raw handling figure into a spoken word.</summary>
        private static string Rate(float value, float low, float mid, float high)
        {
            if (value < low) return "poor";
            if (value < mid) return "moderate";
            if (value < high) return "strong";
            return "excellent";
        }

        private static int GetSeatCount(Model model)
        {
            try { return Function.Call<int>(Hash.GET_VEHICLE_MODEL_NUMBER_OF_SEATS, model.Hash); }
            catch { return 0; }
        }

        private static string GetMakeName(Model model)
        {
            try
            {
                string label = Function.Call<string>(Hash.GET_MAKE_NAME_FROM_VEHICLE_MODEL, model.Hash);
                if (string.IsNullOrEmpty(label) || label == "NULL")
                    return null;

                string localized = Game.GetLocalizedString(label);
                return string.IsNullOrEmpty(localized) || localized == "NULL" ? null : localized;
            }
            catch
            {
                return null;
            }
        }

        private static string GetDisplayName(Model model)
        {
            try
            {
                string label = Function.Call<string>(Hash.GET_DISPLAY_NAME_FROM_VEHICLE_MODEL, model.Hash);
                if (string.IsNullOrEmpty(label) || label == "NULL")
                    return "vehicle";

                string localized = Game.GetLocalizedString(label);
                return string.IsNullOrEmpty(localized) || localized == "NULL" ? label : localized;
            }
            catch
            {
                return "vehicle";
            }
        }

        #endregion
    }
}

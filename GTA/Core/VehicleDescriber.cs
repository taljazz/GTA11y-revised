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
            { "submersible", "A tiny two-person submarine. Very slow, but it can reach the sea floor safely." },

            // ===== WEAPONIZED: ARMED AIRCRAFT =====
            { "buzzard", "The armed Buzzard: a small attack helicopter with nose miniguns and rockets fired by the pilot. Fast, nimble, and the usual choice for armed air support." },
            { "savage", "A heavy gunship helicopter with a stubby armoured body, nose cannon and rockets. Slower and tougher than the Buzzard." },
            { "hunter", "A large two-seat attack helicopter. The pilot has rockets; a passenger in the front seat operates a separate cannon. Heavily armoured and slow to turn." },
            { "akula", "A stealth attack helicopter with folding rotors and retractable gear. Carries missiles and turrets, and can reduce its radar signature." },
            { "annihilator", "A blacked-out police attack helicopter with four miniguns. Available in story mode without any online content." },
            { "annihilator2", "The stealth Annihilator: the same airframe, quieter and darker." },
            { "valkyrie", "A boxy transport helicopter with a nose gun and two side-mounted guns for passengers. Needs a crew to use fully." },
            { "seasparrow", "A tiny two-seat amphibious helicopter with miniguns. Lands on water, and small enough for awkward spaces." },
            { "hydra", "A vertical-takeoff military jet. It can hover like a helicopter or fly like a fighter, and switching between the two is the hard part. Missiles and a cannon." },
            { "raiju", "A very fast vertical-takeoff jet fighter with folding wings. The quickest aircraft in the game and correspondingly unforgiving." },
            { "pyro", "A compact jet fighter. Fast and agile, with cannon and missiles." },
            { "molotok", "A swept-wing jet fighter of 1950s design. Quick and manoeuvrable, easier to fly than the modern jets." },
            { "strikeforce", "A twin-engine ground-attack jet with a heavy cannon and bombs. Slower than a fighter and built for hitting things on the ground." },
            { "b11", "A twin-engine ground-attack jet with a heavy cannon and bombs. Slower than a fighter and built for hitting things on the ground." },
            { "nokota", "A propeller fighter of Second World War design, with machine guns. Slow by jet standards but very agile." },
            { "mogul", "A twin-propeller Second World War bomber with crewed gun turrets and a bomb bay. Large, slow and steady." },
            { "bombushka", "An enormous four-propeller bomber with multiple gun turrets and a bomb bay. Extremely slow, extremely durable, and needs a crew to defend itself." },
            { "volatol", "A very large stealth bomber. Slow and high-flying, with a bomb bay." },
            { "alkonost", "A large stealth bomber built for carrying rather than fighting. Slow, high-flying, and fitted with countermeasures rather than guns." },
            { "rogue", "A propeller-driven attack aircraft with machine guns, a cannon and bombs. Slow, tough, and steady enough to aim from." },
            { "starling", "A propeller fighter with a boost and machine guns. Small, quick and twitchy." },
            { "seabreeze", "An armed light seaplane. Lands on water, carries machine guns, and is slow and forgiving." },
            { "tula", "An amphibious vertical-takeoff bomber. It can hover, land on water, and carries bombs and guns. Bulky and slow to respond." },
            { "avenger", "A huge vertical-takeoff transport aircraft with a rear ramp and gun turrets. It doubles as a mobile base and is very slow to manoeuvre." },
            { "conada", "A small light helicopter. Quick and easy to fly." },

            // ===== WEAPONIZED: TANKS AND ARMOUR =====
            { "rhino", "The classic tank: tracked, very heavily armoured, with a rotating turret and main gun. Extremely slow, and almost nothing can hurt it." },
            { "khanjali", "A modern tank with a rotating turret. Faster and lower than the Rhino, with optional secondary weapons." },
            { "apc", "An amphibious armoured personnel carrier with a roof turret. It floats, shrugs off small arms, and is slow on land." },
            { "halftrack", "A Second World War half-track: wheels at the front, tracks at the back, with mounted machine guns. Slow and very stable." },
            { "chernobog", "A large military truck carrying a bank of surface-to-air missiles. It must stop and deploy before firing." },
            { "barrage", "An open-framed military buggy with mounted guns operated from the passenger seats. Light, fast, and completely unprotected." },
            { "insurgent", "A large armoured off-road truck. Resistant to gunfire and explosions, seats several, and is heavy and slow to stop." },
            { "insurgent2", "The Insurgent Pick-Up: the armoured truck with an open rear bed and a mounted turret for a passenger." },
            { "technical", "A civilian pickup with a heavy machine gun bolted to the bed, fired by a passenger. Fast and completely unarmoured." },
            { "menacer", "A large armoured 4x4 with a boxy body. Tough, heavy, and built to ram." },
            { "nightshark", "An armoured off-road SUV with heavy bodywork and optional side-mounted miniguns. Bulletproof windows make it a common getaway choice." },
            { "rcv", "A riot control vehicle with a water cannon on the roof. Very heavy, very slow." },
            { "minitank", "A tiny remote-control-styled tank you sit inside. Small, low, surprisingly tough, and armed with a cannon." },
            { "thruster", "A jetpack. You fly it standing up, it carries missiles and guns, and it is slow but can go and land almost anywhere." },

            // ===== WEAPONIZED: ARMED CARS =====
            { "vigilante", "A low, jet-black superhero car with an exposed rear engine and an afterburner boost. Rockets, machine guns, and enormous straight-line speed." },
            { "scramjet", "A wedge-shaped weaponized car that can hop over obstacles and boost. Missiles and machine guns." },
            { "deluxo", "A wedge-shaped 1980s sports car with gullwing doors that converts into a hovering, flying car. Missiles and machine guns. Slow on the road, awkward in the air, and hugely capable once mastered." },
            { "stromberg", "A wedge-shaped sports car that submerges and drives underwater. Missiles and machine guns." },
            { "toreador", "A 1970s-styled coupe that submerges and drives underwater, with missiles and a boost. Faster and easier to handle than the Stromberg." },
            { "ruiner2", "A heavily modified muscle car with rockets, a boost, a jump and a parachute. Chaotic and fast." },
            { "tampa3", "A muscle car converted into a weapons platform, with a missile launcher, mortar and mounted guns operated by passengers." },
            { "ardent", "A rounded 1960s sports car with a rear gunner's seat. Needs a passenger to be armed." },
            { "viseris", "A retro wedge-shaped grand tourer with concealed machine guns. Fast and stylish." },
            { "jb7002", "A 1960s spy car with pop-out machine guns. Slow by modern standards and mostly a period piece." },
            { "rcbandito", "A genuine remote-control car, driven from outside it. Tiny, very quick, and can carry explosives." },
            { "turretlimo", "A stretched limousine with a roof turret for a passenger. Long, heavy, and awkward to place." },
            { "dune3", "The Ramp Buggy: an off-road buggy with an enormous wedge-shaped ramp on the nose for launching other cars into the air." },
            { "dune4", "A heavily armed off-road assault buggy with mounted guns and missiles. Open-topped and fast over rough ground." },
            { "dune5", "A heavily armed off-road assault buggy with mounted guns and missiles. Open-topped and fast over rough ground." },

            // ===== WEAPONIZED: MOTORCYCLES =====
            { "oppressor", "A sports motorcycle with deployable wings and a rocket boost, plus missiles. It glides rather than flies, and needs ramps or speed to get airborne." },
            { "oppressor2", "A hovering motorcycle that flies freely in any direction, with missiles and countermeasures. The most mobile vehicle in the game and the hardest to escape." },

            // ===== WEAPONIZED: BOATS =====
            { "patrolboat", "A military patrol boat with a crewed gun turret and a cabin you can walk around inside. Large and slow to turn." },
            { "dinghy5", "An armed inflatable boat with a mounted gun. Small, fast, and easy to beach." },

            // ===== WEAPONIZED: SUPPORT AND UTILITY =====
            { "terrorbyte", "A large windowless command truck. It carries no weapons of its own; it exists to work from, and to launch a drone from inside." },
            { "pounder2", "A large armoured flatbed lorry with mounted weapons operated from the rear. Very heavy and very slow." },
            { "mule4", "A weaponized box lorry with a firing position in the back. Slow, tall, and easy to tip." },
            { "boxville5", "An armoured delivery van with mounted guns. Slow and boxy, and unremarkable to look at, which is the point." },
            { "speedo4", "A weaponized panel van with a firing position in the back. Ordinary-looking and slow." },
            { "trailersmall2", "An anti-aircraft trailer, towed behind a lorry and fired from a seat on top. It cannot move on its own." },

            // ===== ARENA WAR SERIES =====
            //
            // Every Arena War vehicle comes in three themes. Verified against the
            // GTA wiki rather than assumed - the obvious guess is wrong. They are
            // the same bodyshell in three finishes:
            //
            //   Apocalypse   - rusted, scrapyard-built: bare welded plate, exposed
            //                  hardware, the wasteland look.
            //   Future Shock - the same shape cleaned up: smooth painted panels,
            //                  carbon fibre trim, hexagonal-patterned metal.
            //   Nightmare    - the Apocalypse body in bright colours, panels
            //                  alternating blue, green, pink, yellow and purple.
            //                  Despite the name it is the loudest and most
            //                  carnival-looking of the three, NOT a gothic one.
            //
            // None of the three changes how the vehicle drives, and all three take
            // the same spikes and saw blades - spikes kill on contact, saw blades
            // burst tyres. So the theme is worth one clause, not a paragraph.
            { "bruiser", "The Apocalypse Bruiser: a heavily armoured spiked muscle car built to ram, in rusted scrapyard finish." },
            { "brutus", "The Apocalypse Brutus: an armoured monster truck on enormous wheels, built to drive over other cars. Rusted scrapyard finish." },
            { "cerberus", "The Apocalypse Cerberus: an armoured lorry with a spiked ram and a flamethrower. Very heavy and devastating in a collision. Rusted scrapyard finish." },
            { "imperator", "The Apocalypse Imperator: an armoured spiked saloon built for ramming, in rusted scrapyard finish." },
            { "dominator4", "The Apocalypse Dominator: an armoured spiked muscle car, fast in a straight line and built to ram. Rusted scrapyard finish." },
            { "issi4", "The Apocalypse Issi: a tiny city car turned arena weapon - armoured and spiked, and comically small next to the trucks. Rusted scrapyard finish." },
            { "monster3", "The Apocalypse Monster: an armoured monster truck on huge wheels, built to crush. Rusted scrapyard finish." },
            { "sasquatch", "The Apocalypse Sasquatch: an armoured monster truck on huge wheels, built to crush. Rusted scrapyard finish." },
            { "scarab", "The Apocalypse Scarab: a tracked armoured half-track, enormously heavy and nearly unstoppable in a straight line. Rusted scrapyard finish - bare welded plate, exposed hardware, towing hooks and a winch. Takes spikes and saw blades." },
            { "slamvan4", "The Apocalypse Slamvan: an armoured spiked pickup built for ramming, in rusted scrapyard finish." },
            { "zr380", "The Apocalypse ZR380: an armoured spiked sports car - lighter and faster than the arena trucks. Rusted scrapyard finish." },
            { "revolter", "An Arena War sports saloon, armoured and quick. Sold in one finish rather than the three arena themes." },
            { "deathbike", "The Apocalypse Deathbike: an armoured armed motorcycle, fast and fragile next to the cars. Rusted scrapyard finish." },
            { "impaler2", "The Apocalypse Impaler: an armoured spiked muscle car built for ramming, in rusted scrapyard finish." },
            { "impaler3", "The Future Shock Impaler: the same armoured muscle car with clean painted bodywork and carbon trim." },
            { "impaler4", "The Nightmare Impaler: the Apocalypse muscle car in bright alternating colours." },

            // The Arena War themed variants. Apocalypse, Future Shock and
            // Nightmare differ only in how they look, so they share a note - the
            // theme is the one thing a blind player gains nothing from.
            { "bruiser2", "The Future Shock Bruiser: the same armoured rammer with clean painted panels and carbon trim instead of rust." },
            { "bruiser3", "The Nightmare Bruiser: the Apocalypse rammer in bright alternating colours." },
            { "brutus2", "The Future Shock Brutus: the same monster truck with clean painted bodywork and carbon trim." },
            { "brutus3", "The Nightmare Brutus: the Apocalypse monster truck in bright alternating colours." },
            { "cerberus2", "The Future Shock Cerberus: the same flamethrower lorry with clean painted panels and carbon trim." },
            { "cerberus3", "The Nightmare Cerberus: the Apocalypse flamethrower lorry in bright alternating colours." },
            { "imperator2", "The Future Shock Imperator: the same armoured saloon with clean painted bodywork and carbon trim." },
            { "imperator3", "The Nightmare Imperator: the Apocalypse saloon in bright alternating colours." },
            { "dominator5", "The Future Shock Dominator: the same armoured muscle car with clean painted bodywork and carbon trim." },
            { "dominator6", "The Nightmare Dominator: the Apocalypse muscle car in bright colours that alternate between blue, green and yellow." },
            { "issi5", "The Future Shock Issi: the same tiny armoured city car with clean painted bodywork and carbon trim." },
            { "issi6", "The Nightmare Issi: the Apocalypse city car in bright colours alternating between blue, green, pink, yellow and purple." },
            { "monster4", "The Future Shock Monster: the same monster truck with clean painted bodywork and carbon trim." },
            { "monster5", "The Nightmare Monster: the Apocalypse monster truck in bright alternating colours." },
            { "sasquatch2", "The Future Shock Sasquatch: the same monster truck with clean painted bodywork and carbon trim." },
            { "scarab2", "The Future Shock Scarab: the same tracked half-track with clean bodywork instead of rust - smooth painted panels, carbon fibre bumper trim and a hexagonal-patterned bed floor. Drives identically to the Apocalypse version." },
            { "scarab3", "The Nightmare Scarab: the Apocalypse body in bright colours - track panels in green, blue and pink, purple bed floor. Despite the name it is the most carnival-looking of the three. Drives identically." },
            { "slamvan5", "The Future Shock Slamvan: the same armoured pickup with clean painted bodywork and carbon trim." },
            { "slamvan6", "The Nightmare Slamvan: the Apocalypse pickup in bright alternating colours." },
            { "zr3802", "The Future Shock ZR380: the same armoured sports car with clean painted bodywork and carbon trim." },
            { "zr3803", "The Nightmare ZR380: the Apocalypse sports car in bright alternating colours." },
            { "deathbike2", "The Future Shock Deathbike: the same armed motorcycle with clean painted bodywork and carbon trim." },
            { "deathbike3", "The Nightmare Deathbike: the Apocalypse motorcycle in bright alternating colours." },
            { "ruiner3", "The Arena War Ruiner: armoured and spiked, built for ramming." },

            // Variants that genuinely differ from their base vehicle
            { "technical2", "The Technical Aqua: the gun-armed pickup made amphibious. It floats and drives on water, at the cost of being slower on land." },
            { "technical3", "The Technical Custom: the gun-armed pickup with a reinforced open bed and a mounted weapon for a passenger." },
            { "insurgent3", "The Insurgent Pick-Up Custom: an armoured off-road truck with an open bed and a mounted turret, upgradeable in a workshop." },
            { "valkyrie2", "A boxy transport helicopter with a nose gun and two side guns for passengers. A variant of the Valkyrie." },
            { "seasparrow2", "A tiny two-seat amphibious helicopter with miniguns. A variant of the Sea Sparrow." },
            { "seasparrow3", "A tiny two-seat amphibious helicopter with miniguns. A variant of the Sea Sparrow." },
            { "avenger2", "A huge vertical-takeoff transport aircraft with a rear ramp. A variant of the Avenger without the interior workshop." }
        };

        #endregion

        #region Upgrade Guide

        /// <summary>
        /// What each upgrade category does.
        ///
        /// IMPORTANT, and the reason an earlier version of this table was wrong:
        /// several of SHVDN's friendly names are inventions over slots Rockstar
        /// defines only by number, and those slots mean different things on
        /// different vehicles. Checked against Rockstar's own MOD_TYPE enum:
        ///
        ///   SHVDN name      Rockstar's name    What it really is
        ///   Tank        45  MOD_CHASSIS5       generic chassis slot
        ///   Trim        44  MOD_CHASSIS4       generic chassis slot
        ///   Aerials     43  MOD_CHASSIS3       generic chassis slot
        ///   ArchCover   42  MOD_CHASSIS2       generic chassis slot
        ///   Windows     46  MOD_DOOR_L         left door, NOT window tint
        ///   TrimDesign  27  MOD_INTERIOR1      generic interior slot
        ///   Ornaments   28  MOD_INTERIOR2      generic interior slot
        ///   Dashboard   29  MOD_INTERIOR3      generic interior slot
        ///   DialDesign  30  MOD_INTERIOR4      generic interior slot
        ///   DoorSpeakers 31 MOD_INTERIOR5      generic interior slot
        ///
        /// Two further names collide across the two enums: VehicleModType
        /// .Hydraulics is slot 38, MOD_HYDRO, the Benny's hydraulic suspension
        /// part, while VehicleToggleModType.Hydraulics is slot 21,
        /// MOD_HYDRAULICS, the older on-or-off switch. Both are listed, and each
        /// entry says which it is so they do not read as a duplicate.
        ///
        /// On a weaponized vehicle the chassis slots carry the WEAPONS - which
        /// gun is mounted, what the turret is - so calling slot 45 "fuel tank
        /// styling, cosmetic" was not a small slip: it described a weapon choice
        /// as decoration. The notes for those slots now say what they are and
        /// point at the only reliable answer, which is the option names the game
        /// itself returns for the vehicle in front of you.
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
            { VehicleModType.Frame, "Chassis, slot 5. Roll cages on many cars, but on arena and weaponized vehicles this slot often carries armour plating or a ram. Listen to the option names." },
            { VehicleModType.Grille, "Grille. Cosmetic - the panel at the front of the bonnet." },
            { VehicleModType.Hood, "Bonnet. Cosmetic - may add scoops or vents." },
            { VehicleModType.Fender, "Wings. Cosmetic panels over the wheels." },
            { VehicleModType.Roof, "Roof. Scoops, vents or a different roofline on ordinary cars; on weaponized ones this slot sometimes carries a roof-mounted weapon or turret." },
            { VehicleModType.FrontWheel, "Front wheels. Changes the wheel design, and on most vehicles both axles. Wheels can slightly affect grip." },
            { VehicleModType.RearWheel, "Rear wheels, slot 24. On motorcycles this sets the back wheel separately from the front. On most cars it is empty, because the front wheel category already changes both axles." },
            { VehicleModType.PlateHolder, "Number plate holder. Cosmetic." },
            { VehicleModType.VanityPlates, "Number plate style. Cosmetic - changes the plate design." },
            { VehicleModType.TrimDesign, "Interior slot 1. Usually trim or upholstery, but it is a generic slot and some vehicles use it for something else entirely. Listen to the option names." },
            { VehicleModType.Ornaments, "Interior slot 2. Usually small interior details, but generic - some vehicles use it differently." },
            { VehicleModType.Dashboard, "Interior slot 3. Usually the dashboard, but generic - some vehicles use it differently." },
            { VehicleModType.DialDesign, "Interior slot 4. Usually instrument dials, but generic - some vehicles use it differently." },
            { VehicleModType.DoorSpeakers, "Interior slot 5. Usually door speakers, but generic - some vehicles use it differently." },
            { VehicleModType.Seats, "Seats. Cosmetic - racing seats or different upholstery." },
            { VehicleModType.SteeringWheels, "Steering wheel. Cosmetic." },
            { VehicleModType.ColumnShifterLevers, "Gear lever. Cosmetic." },
            { VehicleModType.Plaques, "Plaques. Cosmetic interior badges." },
            { VehicleModType.Speakers, "Speakers. Cosmetic boot-mounted audio." },
            { VehicleModType.Trunk, "Boot. Cosmetic." },
            { VehicleModType.Hydraulics, "Hydraulic suspension, slot 38. The lowrider hydraulics fitted at Benny's, with a choice of setups. Lets the car bounce and lean on command. There is also a plain on-or-off hydraulics entry later in this list; that is the older switch, and this one is the part choice." },
            { VehicleModType.EngineBlock, "Engine block. Cosmetic detail under the bonnet." },
            { VehicleModType.AirFilter, "Air filter. Cosmetic detail under the bonnet." },
            { VehicleModType.Struts, "Strut brace. Cosmetic detail under the bonnet." },
            { VehicleModType.ArchCover, "Slot 42, which Rockstar calls chassis 2. Vehicle-dependent: arch covers or bodywork on some, a weapon or spike choice on arena vehicles. Listen to the option names." },
            { VehicleModType.Aerials, "Slot 43, which Rockstar calls chassis 3. Vehicle-dependent: aerials or bodywork on some, a weapon or armour choice on weaponized ones. Listen to the option names." },
            { VehicleModType.Trim, "Slot 44, which Rockstar calls chassis 4. Vehicle-dependent: bodywork on some, a weapon or turret choice on weaponized ones. Listen to the option names." },
            { VehicleModType.Tank, "Slot 45, which Rockstar calls chassis 5. What it holds depends entirely on the vehicle: on weaponized ones it is usually the MOUNTED WEAPON - plasma cannon, fifty calibre, missile pod and so on - and on bikes it is often the fuel tank. Listen to the option names; they are the only reliable guide." },
            { VehicleModType.Windows, "Slot 46. Despite the name this is the LEFT DOOR slot, not window tint - tint is set separately and is not one of these categories. On some vehicles this slot is bodywork or a weapon." },
            { VehicleModType.Livery, "Livery. A painted design over the bodywork. Cosmetic." }
        };

        /// <summary>
        /// The on-or-off upgrades. These are a separate list in the game -
        /// VehicleToggleModType, slots 17 to 22 - and were missing from the guide
        /// entirely, which left out turbo, the single biggest performance gain
        /// after the engine.
        /// </summary>
        private static readonly Dictionary<VehicleToggleModType, string> ToggleEffects =
            new Dictionary<VehicleToggleModType, string>
        {
            { VehicleToggleModType.Turbo, "Turbo. On or off. A large increase in acceleration and top speed - after the engine, the upgrade you feel most. You will hear it as well." },
            { VehicleToggleModType.Nitrous, "Nitrous. On or off. A short burst of extra speed on demand, refilling over time. Only fitted to arena and some special vehicles." },
            { VehicleToggleModType.SubWoofer, "Subwoofer. On or off. Cosmetic boot speakers." },
            { VehicleToggleModType.TireSmoke, "Tyre smoke. On or off, with a colour chosen separately. Cosmetic - coloured smoke when the wheels spin." },
            { VehicleToggleModType.Hydraulics, "Hydraulics switch. On or off - the older, simpler version of the hydraulic suspension listed above. No performance benefit." },
            { VehicleToggleModType.XenonHeadlights, "Xenon headlights. On or off, with a colour chosen separately. Brighter lights - useful at night, and cosmetic otherwise." }
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
                string note;

                // SHVDN's own VehicleHash enum first. hashes.txt is missing every
                // numbered Arena War variant - no scarab2, no zr3803, none of them -
                // so resolving names through it alone left those notes unreachable.
                // The enum carries all 843 models, and the lookup is case-insensitive
                // so "Scarab2" matches the "scarab2" key.
                string enumName = Enum.GetName(typeof(VehicleHash), (VehicleHash)model.Hash);
                if (!string.IsNullOrEmpty(enumName) && Descriptions.TryGetValue(enumName, out note))
                    return note;

                // Fall back to the hash file for anything the enum does not name
                string internalName;
                if (HashManager.TryGetName(model.Hash, out internalName) &&
                    !string.IsNullOrEmpty(internalName) &&
                    Descriptions.TryGetValue(internalName, out note))
                    return note;

                return null;
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

        /// <summary>The on-or-off upgrades, in menu order.</summary>
        public static List<VehicleToggleModType> GetToggleTypes()
        {
            return new List<VehicleToggleModType>(ToggleEffects.Keys);
        }

        /// <summary>What a given on-or-off upgrade does.</summary>
        public static string GetToggleEffect(VehicleToggleModType type)
        {
            string effect;
            return ToggleEffects.TryGetValue(type, out effect)
                ? effect
                : $"{type}. No description available.";
        }

        /// <summary>
        /// The guidance that applies to the generic slots, spoken once when the
        /// upgrade guide is opened. Without it the vehicle-dependent entries read
        /// as evasive rather than as the honest answer they are.
        /// </summary>
        public static string GetSlotCaveat()
        {
            return "Note: several categories are numbered slots rather than fixed parts, " +
                   "and hold different things on different vehicles. On weaponized vehicles " +
                   "they usually carry the weapons. When a category says it is vehicle-dependent, " +
                   "the option names read out in the Vehicle Mods menu are the reliable answer.";
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

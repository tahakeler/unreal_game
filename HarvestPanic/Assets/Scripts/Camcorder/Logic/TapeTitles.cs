using HarvestPanic.Core;

namespace HarvestPanic.Camcorder
{
    /// <summary>Tabloid VHS box titles ("DAVE vs. THE RIVER, $-240"), design/gdd/camcorder-replay.md comedy beats.</summary>
    public static class TapeTitles
    {
        public static string Opponent(BlameVerb verb)
        {
            switch (verb)
            {
                case BlameVerb.DroppedInRiver: case BlameVerb.Drowned: case BlameVerb.LassoedIntoRiver: return "THE RIVER";
                case BlameVerb.BrokePlank: case BlameVerb.Stranded: return "THE BRIDGE";
                case BlameVerb.TippedWagon: case BlameVerb.SpilledCargo: case BlameVerb.Crashed: return "THE WAGON";
                case BlameVerb.LetEscape: case BlameVerb.SpookedHerd: return "THE LIVESTOCK";
                case BlameVerb.SummonedCreature: return "THE NIGHT";
                case BlameVerb.YeetedTeammate: case BlameVerb.KnockedOut: return "A FRIEND";
                case BlameVerb.LandlordFine: return "THE LANDLORD";
                case BlameVerb.Stolen: case BlameVerb.Pocketed: return "THE LAW";
                case BlameVerb.Slipped: return "THE MUD";
                default: return "GRAVITY";
            }
        }

        public static string Money(int dollars) => dollars < 0 ? "$−" + (-dollars) : "$" + dollars;

        /// <summary>Title for a disaster tape.</summary>
        public static string ForDisaster(string who, BlameVerb verb, int dollarDelta)
        {
            string name = string.IsNullOrWhiteSpace(who) ? "NATURE" : who.Trim().ToUpperInvariant();
            return dollarDelta != 0 ? $"{name} vs. {Opponent(verb)}, {Money(dollarDelta)}" : $"{name} vs. {Opponent(verb)}";
        }

        /// <summary>Title for a manual tape.</summary>
        public static string ForManual(string who, int dayIndex) =>
            $"{(string.IsNullOrWhiteSpace(who) ? "SOMEBODY" : who.Trim().ToUpperInvariant())} WAS FILMING (DAY {dayIndex + 1})";
    }
}

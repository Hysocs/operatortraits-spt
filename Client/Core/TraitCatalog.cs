using System.Collections.Generic;

namespace OperatorTraits
{
    internal sealed class TraitDefinition
    {
        internal TraitDefinition(
            string id,
            string name,
            string description,
            int points,
            bool implemented = true)
        {
            Id = id;
            Name = name;
            Description = description;
            Points = points;
            Implemented = implemented;
        }

        internal string Id { get; }
        internal string Name { get; }
        internal string Description { get; }
        internal int Points { get; }
        internal bool Implemented { get; }
    }

    internal static class TraitCatalog
    {
        internal static readonly IReadOnlyList<TraitDefinition> Strengths =
            new[]
            {
                new TraitDefinition(TraitIds.StreetTax, "Street Tax",
                    "Once per week, some Scavs pay you protection money.", 1),
                new TraitDefinition(TraitIds.Diet, "Diet",
                    "Food and drink items consume 50% less resource when used.", 2),
                new TraitDefinition(TraitIds.JuiceTime, "Juice Time",
                    "Drinking juice grants the Painkiller effect for 60 seconds.", 2),
                new TraitDefinition(TraitIds.Hypodipsia, "Hypodipsia",
                    "Hydration is consumed 20% slower.", 2),
                new TraitDefinition(TraitIds.SailorsNostalgia, "Sailor's Nostalgia",
                    "Eating canned fish grants Health Regeneration +2 for 30 seconds.", 2),
                new TraitDefinition(TraitIds.Sprinter, "Sprinter",
                    "Running speed is increased by 5%.", 3),
                new TraitDefinition(TraitIds.Polyphagia, "Polyphagia",
                    "Energy is consumed 20% slower.", 2),
                new TraitDefinition(TraitIds.Thrombophilia, "Thrombophilia",
                    "Bleeding chance is decreased by 25%.", 2),
                new TraitDefinition(TraitIds.MarathonRunner, "Marathon Runner",
                    "Arm and leg stamina are consumed 20% slower.", 3),
                new TraitDefinition(TraitIds.Youth, "Youth",
                    "Energy drains 20% slower and arm and leg stamina increase by 10.", 5),
                new TraitDefinition(TraitIds.SturdyBones, "Sturdy Bones",
                    "Limb-fracture chance is reduced by 25% and falling damage by 20%.", 3),
                new TraitDefinition(TraitIds.BushBorne, "Bush Borne",
                    "Vegetation makes 75% less noise and causes 75% less slowdown.", 5),
                new TraitDefinition(TraitIds.Safecracker, "Safecracker",
                    "Mechanical keys have a 20% chance to lose no durability when used.", 6)
            };

        internal static readonly IReadOnlyList<TraitDefinition> Scars =
            new[]
            {
                new TraitDefinition(TraitIds.ChronicFatigueSyndrome,
                    "Chronic Fatigue Syndrome", "Energy is consumed 15% faster.", 2),
                new TraitDefinition(TraitIds.ThirdLeg, "Third Leg",
                    "Move 1% slower, but Therapist sells items 5% cheaper.", 1),
                new TraitDefinition(TraitIds.Polydipsia, "Polydipsia",
                    "Hydration is consumed 20% faster.", 2),
                new TraitDefinition(TraitIds.DrJekyll, "Dr. Jekyll",
                    "Fresh Wound status cannot be removed until the raid ends.", 1),
                new TraitDefinition(TraitIds.Hemophilia, "Hemophilia",
                    "Bleeding chance is increased by 25%.", 2),
                new TraitDefinition(TraitIds.WellThatHurt, "Well That Hurt!",
                    "All medkit uses consume 25% more resource.", 2),
                new TraitDefinition(TraitIds.Allergic, "Allergic",
                    "Become allergic to three random Provision or Medication items.", 3),
                new TraitDefinition(TraitIds.Osteoporosis, "Osteoporosis",
                    "Limb-fracture chance is increased by 25% and falling damage by 20%.", 3),
                new TraitDefinition(TraitIds.BrokenSecureContainer,
                    "Broken Secure Container",
                    "Secure container is restricted to cash, keys, dogtags, special equipment, and certain containers.", 6),
                new TraitDefinition(TraitIds.Exhaustion, "Exhaustion",
                    "Arm and leg stamina recover 20% slower and are reduced by 10.", 5),
                new TraitDefinition(TraitIds.NoFleaMarket, "No Flea Market",
                    "Player-to-player Flea Market trading is unavailable.", 10)
            };
    }
}

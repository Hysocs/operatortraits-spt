# Operator Traits

A standalone SPT character-trait mod. Version 0.1.0 adds a native **Traits**
tab immediately after **Tasks** in Tarkov's character menu, with trait
selection, point balancing, persistence, and gameplay effects.

## Repository layout

- `Client/` — BepInEx client plugin and client project
- `Server/` — SPT server mod and server project
- `dist/` — generated release packages

## Build

```powershell
dotnet build OperatorTraits.sln -c Release -p:SkipDeploy=true
```

Omit `SkipDeploy=true` to install into the configured SPT 4.1.1 directory.

## Street Tax

Street Tax pays random rouble, dollar, and euro stacks through in-game messages
on the first Traits-state check of each Monday-to-Sunday week. Its defaults are
based on observed live-EFT payouts: 150,000-400,000 roubles, 1,000-1,500
dollars, and 1,000-1,500 euros. Settings and per-profile payment dates are kept
together in the server mod's `data/traits.json` file.

For testing, open the BepInEx F12 configuration menu and enable
`Street Tax Testing > Simulate payment`. The toggle resets automatically and
test payments do not alter the real weekly payment date.

## Diet

When the `diet` trait is active, partial use of a multi-use food or drink item
consumes 50% less resource while retaining the normal energy and hydration
effect. For example, using 30 from a 60-resource item leaves 45. `Use All`
instead consumes and removes the whole item normally but grants twice its
energy and hydration value. The client and server apply matching calculations.
Single-use provisions with a maximum resource of 1 remain single-use.

## Juice Time

With `juice-time` active, successfully drinking Russian Army pineapple, Apple,
Grand, or Vita juice grants EFT's native Painkiller effect for 60 seconds.
Water, milk, soda, alcohol, and energy drinks are excluded. The effect uses the
normal health-effect system in both stash and raids.

## Hypodipsia

With `hypodipsia` active, EFT's passive in-raid hydration drain is multiplied
by 0.8, making hydration drain 20% slower. It modifies the native metabolism
rate and timed ticks, including the destroyed-stomach case, without changing
stash regeneration or hydration changes caused by items and stimulants.

## Sailor's Nostalgia

With `sailor-s-nostalgia` active, successfully finishing Pacific saury, pink
salmon, herring, or sprats in a raid grants EFT's native Health Regeneration
+2 effect for 30 seconds. Interrupted eating does not trigger the effect.

## Sprinter

With `sprinter` active, horizontal movement while the local player is actually
sprinting is multiplied by 1.05. Walking and non-player movement are unchanged.

## Polyphagia

With `polyphagia` active, EFT's passive in-raid energy drain is multiplied by
0.8. Stash regeneration and energy changes from provisions or stimulants are
unchanged.

## Thrombophilia

With `thrombophilia` active, the local player's native heavy- and
light-bleeding rolls from incoming damage are reduced by 25%. The modifier
composes with Vitality's bleed resistance and does not alter fractures, bots,
existing bleeds, or effects that explicitly apply bleeding.

## Marathon Runner

With `marathon-runner` active, the local player's arm and leg stamina are
consumed 20% slower. Both passive tick drain and one-shot consumptions (jump,
vault, weapon swap, etc.) are affected; restoration is unchanged. The modifier
applies only to the local player's `Stamina` and `HandsStamina` instances and
composes cleanly with `Youth`.

## Youth

With `youth` active, passive in-raid energy drain is multiplied by 0.8 and the
local player's body and arm stamina capacities each gain 10 points. The energy
modifier composes with Polyphagia when both traits are active.

## Sturdy Bones

With `sturdy-bones` active, native fall damage is reduced by 20% and both
bullet-hit and falling fracture probabilities are reduced by 25% for the local
player.

## Bush Borne

With `bush-borne` active, local vegetation rustling volume is reduced by 75%
and vegetation/swamp obstruction applies only 25% of its normal slowdown.

## Safecracker

With `safecracker` active, a successful mechanical-key unlock has a 20% chance
not to consume durability. Keycards are excluded, and saved uses are logged.

## Chronic Fatigue Syndrome

With `chronic-fatigue-syndrome` active, EFT's passive in-raid energy drain is
multiplied by 1.15. Stash regeneration and energy changes from provisions or
stimulants are unchanged. The modifier composes with Polyphagia and Youth when
multiple energy traits are active.

## Third Leg

With `third-leg` active, the local player's horizontal movement is multiplied
by 0.99. Therapist's currency prices are multiplied by 0.95 for that profile;
the adjustment is applied to the profile-specific cloned assortment and also
flows through Therapist offers shown on the Flea Market. Barter-item counts are
not changed.

## Polydipsia

With `polydipsia` active, EFT's passive in-raid hydration drain is multiplied
by 1.2. The modifier composes with Hypodipsia when both are active.

## Hemophilia

With `hemophilia` active, the local player's native heavy- and light-bleeding
rolls from incoming damage are increased by 25%. The modifier composes with
Vitality's bleed resistance and with Thrombophilia when both traits are active.
It does not alter fractures, bots, existing bleeds, or effects that explicitly
apply bleeding.

## Osteoporosis

With `osteoporosis` active, native fall damage is increased by 20% and both
bullet-hit and falling fracture probabilities are increased by 25% for the
local player. The modifier composes with Sturdy Bones when both are active.

## Exhaustion

With `exhaustion` active, arm and leg stamina recover 20% slower and each
capacity is reduced by 10. The capacity modifier composes with Youth (net zero
capacity change when both are active); the recovery modifier is independent.

## Allergic

When `allergic` is active, three random provision or medication items become
allergens for the profile. The set is generated by the server the first time
the trait is added, persisted in `data/traits.json`, and shipped to the client
with the rest of the saved traits. Using one of the allergens in a raid
triggers a 45-to-75-second allergic reaction using EFT's native intoxication,
tremor, and tunnel-vision effects. Stash usage does not trigger the allergy.
Removing `allergic` clears the allergen set so re-picking starts fresh next
time the trait is taken.

## Broken Secure Container

With `broken-secure-container` active, attempted moves into the secure
container accept only money, keys, dogtags, special-equipment items (including
the weapon repair kit), and compact cash/key/dogtag utility cases. The
injector case and loose stimulants are intentionally excluded to match the
live seasonal rule. Both the client and
server validate moves, swaps, splits, nested destinations, and the contents of
a case being moved. Existing items are not deleted or relocated. The mod does
not modify EFT item templates or container filters, so removing the trait
immediately restores vanilla secure-container behavior.

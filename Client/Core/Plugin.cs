using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Newtonsoft.Json.Linq;
using SPT.Common.Http;
using HarmonyLib;
using UnityEngine;
using System.Collections;

namespace OperatorTraits
{
    [BepInPlugin(Guid, Name, Version)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.hysocs.operatortraits";
        public const string Name = "Operator Traits";
        public const string Version = "0.1.0";

        internal static Plugin Instance { get; private set; }
        internal static ManualLogSource Log { get; private set; }

        private Harmony _harmony;
        private ConfigEntry<bool> _simulateStreetTaxPayment;
        private bool _resettingSimulationToggle;
        private readonly Dictionary<string, ConfigEntry<bool>> _devToggles =
            new Dictionary<string, ConfigEntry<bool>>(StringComparer.Ordinal);
        private void Awake()
        {
            Instance = this;
            Log = Logger;
            _harmony = new Harmony(Guid);
            _simulateStreetTaxPayment = Config.Bind(
                "Street Tax Testing",
                "Simulate payment",
                false,
                "Turn this on to send a test Street Tax payment. It resets itself and does not change the weekly paid date.");
            _simulateStreetTaxPayment.SettingChanged += OnSimulateStreetTaxPayment;

            BindDevToggleSection();

            _harmony.PatchAll();
            Logger.LogInfo($"{Name} {Version} loaded.");
            StartCoroutine(LoadTraitsWhenProfileIsReady());
        }

        private void BindDevToggleSection()
        {
            const string positiveSection = "Dev Testing - Positive";
            const string negativeSection = "Dev Testing - Negative";
            foreach (TraitDefinition trait in TraitCatalog.Strengths)
                BindDevToggle(positiveSection, trait);
            foreach (TraitDefinition trait in TraitCatalog.Scars)
                BindDevToggle(negativeSection, trait);
        }

        private void BindDevToggle(string section, TraitDefinition trait)
        {
            string prefix = trait.Implemented ? "[Added] " : "";
            ConfigEntry<bool> entry = Config.Bind(
                section,
                trait.Id,
                false,
                $"{prefix}{trait.Name}\n\n{trait.Description}\n\n" +
                "Tick to add this trait for client-side testing. Untick to " +
                "stop the override. Saved traits remain active and unchanged.");
            entry.SettingChanged += OnDevToggleChanged;
            _devToggles[trait.Id] = entry;
        }

        private void OnDevToggleChanged(object sender, EventArgs args)
        {
            ApplyDevToggles();
            Plugin.Log.LogInfo(
                $"Active Operator Traits after dev toggle: {_activeTraits.Count}.");
        }

        internal static void ApplyDevToggles()
        {
            _activeTraits.Clear();
            _activeTraits.UnionWith(_savedTraits);

            if (Instance == null)
                return;

            foreach (KeyValuePair<string, ConfigEntry<bool>> pair in Instance._devToggles)
            {
                if (pair.Value.Value)
                    _activeTraits.Add(pair.Key);
            }
        }

        internal static bool HasTrait(string traitId) =>
            _activeTraits.Contains(traitId);

        internal static void SetActiveTraits(IEnumerable<string> traits)
        {
            _savedTraits.Clear();
            if (traits != null)
                foreach (string trait in traits)
                    if (!string.IsNullOrWhiteSpace(trait))
                        _savedTraits.Add(trait);
            ApplyDevToggles();
            if (!_activeTraits.Contains(TraitIds.Allergic))
                _activeAllergies.Clear();
        }

        internal static void SetActiveAllergies(IEnumerable<string> allergens)
        {
            _activeAllergies.Clear();
            if (allergens == null ||
                !_activeTraits.Contains(TraitIds.Allergic))
                return;
            foreach (string id in allergens)
                if (!string.IsNullOrWhiteSpace(id))
                    _activeAllergies.Add(id);
        }

        internal static bool HasAllergen(string templateId) =>
            _activeAllergies.Contains(templateId);

        private static readonly HashSet<string> _activeTraits =
            new HashSet<string>(StringComparer.Ordinal);

        private static readonly HashSet<string> _savedTraits =
            new HashSet<string>(StringComparer.Ordinal);

        private static readonly HashSet<string> _activeAllergies =
            new HashSet<string>(StringComparer.Ordinal);

        private IEnumerator LoadTraitsWhenProfileIsReady()
        {
            while (true)
            {
                yield return new WaitForSeconds(2f);
                try
                {
                    JObject response = JObject.Parse(RequestHandler.PostJson(
                        "/operator-traits/load", "{}"));
                    bool success = response.Value<bool?>("success") ??
                                   response.Value<bool?>("Success") ?? false;
                    JToken traitsToken = response["traits"] ?? response["Traits"];
                    if (!success || traitsToken == null)
                        continue;

                    SetActiveTraits(traitsToken.ToObject<List<string>>());

                    JToken allergiesToken =
                        response["allergies"] ?? response["Allergies"];
                    if (allergiesToken != null)
                        SetActiveAllergies(allergiesToken.ToObject<List<string>>());

                    Logger.LogInfo(
                        $"Loaded {_activeTraits.Count} active Operator Traits " +
                        $"with {_activeAllergies.Count} allergen(s).");
                    yield break;
                }
                catch
                {
                    // The active profile/session is not available yet.
                }
            }
        }

        private void OnSimulateStreetTaxPayment(object sender, EventArgs args)
        {
            if (_resettingSimulationToggle || !_simulateStreetTaxPayment.Value)
                return;

            try
            {
                string json = RequestHandler.PostJson(
                    "/operator-traits/street-tax/simulate", "{}");
                JObject response = JObject.Parse(json);
                bool success = response.Value<bool?>("success") ??
                               response.Value<bool?>("Success") ?? false;
                string message = response.Value<string>("message") ??
                                 response.Value<string>("Message") ??
                                 "Street Tax simulation returned no message.";
                if (success)
                    Logger.LogInfo(message);
                else
                    Logger.LogWarning(message);
            }
            catch (Exception exception)
            {
                Logger.LogError($"Could not simulate Street Tax payment: {exception}");
            }
            finally
            {
                _resettingSimulationToggle = true;
                _simulateStreetTaxPayment.Value = false;
                _resettingSimulationToggle = false;
            }
        }

        private void OnDestroy()
        {
            if (_simulateStreetTaxPayment != null)
                _simulateStreetTaxPayment.SettingChanged -= OnSimulateStreetTaxPayment;
            foreach (ConfigEntry<bool> entry in _devToggles.Values)
                entry.SettingChanged -= OnDevToggleChanged;
            _harmony?.UnpatchSelf();
            Instance = null;
        }

    }
}

using System.Collections;
using EFT;
using EFT.HealthSystem;
using EFT.InventoryLogic;
using HarmonyLib;
using UnityEngine;

namespace OperatorTraits
{
    [HarmonyPatch(
        typeof(ActiveHealthController.MedEffect),
        nameof(ActiveHealthController.MedEffect.Residue))]
    internal static class AllergicPatch
    {
        // Keep this below the duration that could kill a full-health player.
        private const float MinTotalDuration = 45f;
        private const float MaxTotalDuration = 75f;

        private const float MinPulseGap = 10f;
        private const float MaxPulseGap = 25f;
        private const float MinPulseDuration = 3f;
        private const float MaxPulseDuration = 7f;

        private static void Prefix(
            ActiveHealthController.MedEffect __instance,
            out bool __state)
        {
            __state = !__instance._interrupted &&
                       Plugin.HasTrait("allergic") &&
                       __instance.MedItem != null &&
                       Plugin.HasAllergen(__instance.MedItem.StringTemplateId);
        }

        private static void Postfix(
            ActiveHealthController.MedEffect __instance,
            bool __state)
        {
            if (!__state || __instance._interrupted)
                return;

            ActiveHealthController health = __instance.HealthController;
            if (health?.Player == null || !health.Player.IsYourPlayer)
                return;

            string templateId = __instance.MedItem.StringTemplateId;
            float totalDuration = Random.Range(MinTotalDuration, MaxTotalDuration);

            // Intoxication is non-stackable; repeated use extends it instead.
            health.AddEffect<ActiveHealthController.Intoxication>(
                ActiveHealthController.Intoxication.DefaultBodyPart,
                null,
                totalDuration,
                null,
                null,
                null);

            health.AddEffect<ActiveHealthController.Tremor>(
                EBodyPart.Head,
                null,
                totalDuration,
                0f,
                null,
                null);

            // The plugin owns this coroutine so it survives the MedEffect frame.
            Plugin.Instance.StartCoroutine(
                TunnelVisionPulseRoutine(health, totalDuration));

            Plugin.Log.LogInfo(
                $"Allergic reaction to {templateId}: intoxication + tremor for " +
                $"{totalDuration:F1}s, tunnel-vision pulses every {MinPulseGap:F0}-" +
                $"{MaxPulseGap:F0}s.");
        }

        private static IEnumerator TunnelVisionPulseRoutine(
            ActiveHealthController health,
            float totalDuration)
        {
            float elapsed = 0f;
            while (health != null && health.IsAlive && elapsed < totalDuration)
            {
                float gap = Random.Range(MinPulseGap, MaxPulseGap);
                if (elapsed + gap > totalDuration)
                    gap = Mathf.Max(0f, totalDuration - elapsed);
                yield return new WaitForSeconds(gap);
                elapsed += gap;
                if (elapsed >= totalDuration || !health.IsAlive)
                    break;

                float pulseDuration = Random.Range(
                    MinPulseDuration, MaxPulseDuration);
                if (elapsed + pulseDuration > totalDuration)
                    pulseDuration = Mathf.Max(
                        1f, totalDuration - elapsed);

                health.AddEffect<ActiveHealthController.TunnelVision>(
                    EBodyPart.Head,
                    null,
                    pulseDuration,
                    null,
                    null,
                    null);

                yield return new WaitForSeconds(pulseDuration);
                elapsed += pulseDuration;
            }
        }
    }
}

using HarmonyLib;

namespace ToastersRinkCompanion.handlers;

/// On training-flavor Toaster's Rink servers, replace the vanilla HUD phase
/// label "WARMUP" with "TRAINING". The vanilla controller funnels every phase
/// change through UIGameState.SetPhase, so a postfix here catches all cases
/// without us having to listen to GameState events ourselves.
public static class TrainingHudLabel
{
    [HarmonyPatch(typeof(UIGameState), nameof(UIGameState.SetPhase))]
    public static class SetPhaseLabelPatch
    {
        [HarmonyPrefix]
        public static void Prefix(ref string text)
        {
            if (!MessagingHandler.connectedToToastersRink) return;
            if (!string.Equals(MessagingHandler.serverFlavor, "training", System.StringComparison.OrdinalIgnoreCase)) return;
            if (text == "WARMUP") text = "TRAINING";
        }
    }
}

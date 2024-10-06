using HarmonyLib;

using TaleWorlds.CampaignSystem;

namespace BannerlordPlayerSettlement.Patches.Compatibility.Interfaces
{
    public interface ICompatibilityPatch
    {
        public void PatchSubmoduleLoad(Harmony harmony);
        public void PatchAfterMenus(Harmony harmony);
        void AddBehaviors(CampaignGameStarter gameInitializer);
    }
}

using Dalamud.Interface.FontIdentifier;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace LazyLoot
{
    internal class Utils
    {
        public unsafe static int GetPlayerIlevel()
        {
            return UIState.Instance()->CurrentItemLevel;
            var atkArrayDataHolder = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance()->GetUIModule()->GetRaptureAtkModule()->AtkModule.AtkArrayDataHolder;
            return atkArrayDataHolder.NumberArrays[64]->IntArray[21];
        }
    }
}

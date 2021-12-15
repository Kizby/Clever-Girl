namespace XRL.World.CleverGirl {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using ConsoleLib.Console;
    using XRL.UI;
    using XRL.World;

    public static class InterfaceCompanions {
        public static bool DoInterface(InventoryActionEvent E, ref GameObject Companion) {
            var companions = Utility.CollectNearbyCompanions(E.Actor).Where(c => c.IsTrueKin()).ToList();
            var options = new List<string>();
            var icons = new List<IRenderable>();
            foreach (var companion in companions) {
                options.Add(companion.one(WithIndefiniteArticle: true));
                icons.Add(companion.RenderForUI());
            }
            var index = 0;
            while (true) {
                index = Popup.ShowOptionList("{{W|Your " + (companions.Count > 1 ? "companions are" : "companion is") + " ready to {{gray|BECOME}}.}}",
                                                Options: options.ToArray(),
                                                Icons: icons.ToArray(),
                                                iconPosition: 6,
                                                centerIntro: true,
                                                defaultSelected: index,
                                                AllowEscape: true);
                if (index == -1) {
                    return false;
                }
                Companion = companions[index];
                CyberneticsTerminal.ShowTerminal(E.Item, Companion);
                E.RequestInterfaceExit();
                return true;
            }
        }
    }
}

// equipment screen logic copied and modified from Caves of Qud's EquipmentScreen.cs

using ConsoleLib.Console;
using Qud.API;
using System.Collections.Generic;
using XRL.Core;
using XRL.UI;
using XRL.World.Parts;

namespace XRL.World.CleverGirl
{
    public class ManageGear {
        public static readonly Utility.InventoryAction ACTION = new Utility.InventoryAction{
            Name = "Clever Girl - Manage Gear",
            Display = "manage g{{inventoryhotkey|e}}ar",
            Command = "CleverGirl_ManageGear",
            Key = 'e',
        };

        public static bool Manage(GameObject Leader, GameObject Follower) {
            GameManager.Instance.PushGameView("Equipment");
            ScreenBuffer screenBuffer = ScreenBuffer.GetScrapBuffer1();
            Keys keys = Keys.None;
            int selectedIndex = 0;
            int windowStart = 0;
            ScreenTab screenTab = ScreenTab.Equipment;
            bool Done = false;
            bool Changed = false;
            Body body = Follower.Body;
            List<BodyPart> relevantBodyParts = new List<BodyPart>();
            List<GameObject> allCybernetics = new List<GameObject>();
            List<GameObject> allEquippedOrDefault = new List<GameObject>();
            List<GameObject> allEquipped = new List<GameObject>();
            Dictionary<char, int> keymap = new Dictionary<char, int>();
            HashSet<GameObject> wornElsewhere = new HashSet<GameObject>();
            while (!Done) {
                bool HasCybernetics = false;
                relevantBodyParts.Clear();
                allCybernetics.Clear();
                allEquippedOrDefault.Clear();
                allEquipped.Clear();
                foreach (BodyPart loopPart in body.LoopParts()) {
                    if (screenTab == ScreenTab.Equipment) {
                        if (loopPart.Equipped != null) {
                            // equipped item
                            relevantBodyParts.Add(loopPart);
                            allEquippedOrDefault.Add(loopPart.Equipped);
                            allEquipped.Add(loopPart.Equipped);
                            allCybernetics.Add(null);
                        } else if (loopPart.DefaultBehavior != null) {
                            // natural weapon
                            relevantBodyParts.Add(loopPart);
                            allEquippedOrDefault.Add(loopPart.DefaultBehavior);
                            allEquipped.Add(null);
                            allCybernetics.Add(null);
                        } else {
                            // empty slot
                            relevantBodyParts.Add(loopPart);
                            allEquippedOrDefault.Add(null);
                            allEquipped.Add(null);
                            allCybernetics.Add(null);
                        }
                    }
                    if (loopPart.Cybernetics != null) {
                        if (screenTab == ScreenTab.Cybernetics) {
                            relevantBodyParts.Add(loopPart);
                            allEquippedOrDefault.Add(null);
                            allEquipped.Add(loopPart.Cybernetics);
                            allCybernetics.Add(loopPart.Cybernetics);
                        }
                        HasCybernetics = true;
                    }
                }
                bool CanChangePrimaryLimb = !Follower.AreHostilesNearby();
                bool CacheValid = true;
                while (!Done && CacheValid) {
                    Event.ResetPool(false);
                    if (!XRLCore.Core.Game.Running) {
                        GameManager.Instance.PopGameView();
                        return false;
                    }
                    wornElsewhere.Clear();
                    screenBuffer.Clear();
                    screenBuffer.SingleBox(0, 0, 79, 24, ColorUtility.MakeColor(TextColor.Grey, TextColor.Black));
                    if (screenTab == ScreenTab.Equipment) {
                        screenBuffer.Goto(35, 0);
                        screenBuffer.Write("[ {{W|Equipment}} ]");
                    } else {
                        screenBuffer.Goto(35, 0);
                        screenBuffer.Write("[ {{W|Cybernetics}} ]");
                    }
                    screenBuffer.Goto(60, 0);
                    screenBuffer.Write(" {{W|ESC}} or {{W|5}} to exit ");
                    screenBuffer.Goto(25, 24);
                    if (CanChangePrimaryLimb && !relevantBodyParts[selectedIndex].Abstract) {
                        screenBuffer.Write("[{{W|Tab}} - Set primary limb]");
                    } else {
                        screenBuffer.Write("[{{K|Tab - Set primary limb}}]");
                    }
                    int rowCount = 22;
                    int firstRow = 2;
                    if (HasCybernetics) {
                        screenBuffer.Goto(3, firstRow);
                        if (screenTab == ScreenTab.Cybernetics) {
                            screenBuffer.Write("{{K|Equipment}} {{Y|Cybernetics}}");
                        } else {
                            screenBuffer.Write("{{Y|Equipment}} {{K|Cybernetics}}");
                        }
                        rowCount -= 2;
                        firstRow += 2;
                    }
                    if (relevantBodyParts != null) {
                        keymap.Clear();
                        for (int partIndex = windowStart; partIndex < relevantBodyParts.Count && partIndex - windowStart < rowCount; ++partIndex) {
                            var currentRow = firstRow + partIndex - windowStart;
                            if (selectedIndex == partIndex) {
                                screenBuffer.Goto(27, currentRow);
                                screenBuffer.Write("{{K|>}}");
                            }
                            screenBuffer.Goto(1, currentRow);
                            string cursorString = selectedIndex == partIndex ? "{{Y|>}}" : " ";
                            string partDesc = "";
                            if (allCybernetics[partIndex] == null && Options.IndentBodyParts) {
                                partDesc += new string(' ', body.GetPartDepth(relevantBodyParts[partIndex]));
                            }
                            partDesc += relevantBodyParts[partIndex].GetCardinalDescription();
                            if (relevantBodyParts[partIndex].Primary) {
                                partDesc += " {{G|*}}";
                            }
                            char key = (char) ('a' + partIndex);
                            if (key > 'z') {
                                key = ' ';
                            } else {
                                keymap.Add(key, partIndex);
                            }

                            var keyString = (selectedIndex == partIndex ? "{{W|" : "{{w|") + key + "}}) ";
                            screenBuffer.Write(cursorString + keyString + partDesc);

                            screenBuffer.Goto(28, currentRow);
                            RenderEvent icon = null;
                            string name = "";
                            bool fade = false;
                            if (allEquippedOrDefault[partIndex] == null) {
                                // nothing in this slot
                                screenBuffer.Write(selectedIndex == partIndex ? "{{Y|-}}" : "{{K|-}}");
                            } else if (screenTab == ScreenTab.Cybernetics) {
                                // cybernetics
                                icon = allCybernetics[partIndex].RenderForUI();
                                name = allCybernetics[partIndex].DisplayName;
                            } else if (allEquipped[partIndex] == null) {
                                // natural weapons
                                icon = allEquippedOrDefault[partIndex].RenderForUI();
                                name = allEquippedOrDefault[partIndex].DisplayName;
                                fade = true;
                            } else {
                                // all other equipment
                                icon = allEquipped[partIndex].RenderForUI();
                                name = allEquipped[partIndex].DisplayName;

                                fade = !wornElsewhere.Add(allEquipped[partIndex]) ||
                                    allEquipped[partIndex].HasTag("RenderImplantGreyInEquipment") && allEquipped[partIndex].GetPart<Cybernetics2BaseItem>()?.ImplantedOn != null;
                            }
                            if (icon != null) {
                                if (fade) {
                                    screenBuffer.Write(icon, ColorString: "&K", TileColor: "&K", DetailColor: new char?('K'));
                                } else {
                                    screenBuffer.Write(icon);
                                }
                                screenBuffer.Goto(30, currentRow);
                                screenBuffer.Write(fade ? "{{K|" + ColorUtility.StripFormatting(name) + "}}" : name);
                            }
                        }
                        if (windowStart + rowCount < relevantBodyParts.Count) {
                            screenBuffer.Goto(2, 24);
                            screenBuffer.Write("<more...>");
                        }
                        if (windowStart > 0) {
                            screenBuffer.Goto(2, 0);
                            screenBuffer.Write("<more...>");
                        }
                        Popup._TextConsole.DrawBuffer(screenBuffer);
                        keys = Keyboard.getvk(Options.MapDirectionsToKeypad);
                        ScreenBuffer.ClearImposterSuppression();
                        char key1 = (char.ToLower((char) Keyboard.Char).ToString() + " ").ToLower()[0];
                        if (keys == Keys.MouseEvent && Keyboard.CurrentMouseEvent.Event == "RightClick") {
                            Done = true;
                        } else if (keys == Keys.Escape || keys == Keys.NumPad5) {
                            Done = true;
                        } else if (keys == Keys.Prior) {
                            selectedIndex = 0;
                            windowStart = 0;
                        } else if ((keys == Keys.NumPad4 || keys == Keys.NumPad6) && HasCybernetics) {
                            screenTab = screenTab != ScreenTab.Cybernetics ? ScreenTab.Cybernetics : ScreenTab.Equipment;
                            selectedIndex = 0;
                            windowStart = 0;
                            CacheValid = false;
                        } else if (keys == Keys.Next) {
                            selectedIndex = relevantBodyParts.Count - 1;
                            if (relevantBodyParts.Count > rowCount) {
                                windowStart = relevantBodyParts.Count - rowCount;
                            }
                        } else if (keys == Keys.NumPad8) {
                            if (selectedIndex - windowStart <= 0) {
                                if (windowStart > 0) {
                                    --windowStart;
                                }
                            } else if (selectedIndex > 0) {
                                --selectedIndex;
                            }
                        } else if (keys == Keys.NumPad2 && selectedIndex < relevantBodyParts.Count - 1) {
                            if (selectedIndex - windowStart == rowCount - 1) {
                                    ++windowStart;
                            }
                            ++selectedIndex;
                        } else if ((Keyboard.vkCode == Keys.Left || keys == Keys.NumPad4) && allEquipped[selectedIndex] != null) {
                            BodyPart bodyPart = relevantBodyParts[selectedIndex];
                            Follower.FireEvent(Event.New("CommandUnequipObject", "BodyPart", (object) bodyPart));
                            CacheValid = false;
                        } else if (keys == Keys.Tab) {
                            relevantBodyParts[selectedIndex].SetAsPreferredDefault();
                        } else {
                            if (keys == Keys.Enter) {
                                keys = Keys.Space;
                            }
                            bool useSelected = (keys == Keys.Space || keys == Keys.Enter);
                            if (useSelected || (keys >= Keys.A && keys <= Keys.Z && keymap.ContainsKey(key1))) {
                                int pressedIndex = useSelected ? selectedIndex : keymap[key1];
                                if (allEquipped[pressedIndex] != null) {
                                    EquipmentAPI.TwiddleObject(Follower, allEquipped[pressedIndex], ref Done);
                                } else {
                                    ShowBodypartEquipUI(Leader, Follower, relevantBodyParts[pressedIndex]);
                                }
                                CacheValid = false;
                            }
                        }
                    }
                }
            }
            GameManager.Instance.PopGameView();
            return Changed;
        }
        public static void ShowBodypartEquipUI(GameObject Leader, GameObject Follower, BodyPart SelectedBodyPart) {
            Inventory leaderInventory = Leader.Inventory;
            Inventory inventory = Follower.Inventory;
            if (inventory != null || leaderInventory != null) {
                List<GameObject> EquipmentList = new List<GameObject>(16);
                inventory?.GetEquipmentListForSlot(EquipmentList, SelectedBodyPart.Type);
                leaderInventory?.GetEquipmentListForSlot(EquipmentList, SelectedBodyPart.Type);

                if (EquipmentList.Count > 0) {
                string CategoryPriority = null;
                if (SelectedBodyPart.Type == "Hand") {
                    CategoryPriority = "Melee Weapon,Shield,Light Source";
                } else if (SelectedBodyPart.Type == "Thrown Weapon") {
                    CategoryPriority = "Grenades";
                }
                GameObject toEquip = PickItem.ShowPicker(EquipmentList, CategoryPriority, PreserveOrder: true);
                if (toEquip == null) {
                    return;
                }
                Follower.FireEvent(Event.New("CommandEquipObject", "Object", toEquip, "BodyPart", SelectedBodyPart));
                } else {
                   Popup.Show("Neither of you have anything to use in that slot.");
                }
            } else {
                Popup.Show("You both have no inventory!");
            }
        }
        private enum ScreenTab {
            Equipment,
            Cybernetics,
        }
    }
}
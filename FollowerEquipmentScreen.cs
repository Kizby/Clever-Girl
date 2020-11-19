// copied and modified from Caves of Qud's EquipmentScreen.cs

using ConsoleLib.Console;
using Qud.API;
using System.Collections.Generic;
using XRL.Core;
using XRL.World;
using XRL.World.Parts;

namespace XRL.UI.CleverGirl
{
  public class FollowerEquipmentScreen {
    public static void ShowBodypartEquipUI(GameObject Leader, GameObject Follower, BodyPart SelectedBodyPart) {
      Inventory leaderInventory = Leader.Inventory;
      Inventory inventory = Follower.Inventory;
      if (inventory != null || leaderInventory != null) {
        List<GameObject> EquipmentList = new List<GameObject>(16);
        inventory?.GetEquipmentListForSlot(EquipmentList, SelectedBodyPart.Type);
        leaderInventory?.GetEquipmentListForSlot(EquipmentList, SelectedBodyPart.Type);
        
        if (EquipmentList.Count > 0) {
          string CategoryPriority = null;
          if (SelectedBodyPart.Type == "Hand")
            CategoryPriority = "Melee Weapon,Shield,Light Source";
          if (SelectedBodyPart.Type == "Thrown Weapon")
            CategoryPriority = "Grenades";
          GameObject toEquip = PickItem.ShowPicker(EquipmentList, CategoryPriority, PreserveOrder: true);
          if (toEquip == null)
            return;
          Follower.FireEvent(Event.New("CommandEquipObject", "Object", toEquip, "BodyPart", SelectedBodyPart));
        } else {
          Popup.Show("Neither of you have anything to use in that slot.");
        }
      } else {
        Popup.Show("You both have no inventory!");
      }
    }

    public ScreenReturn Show(GameObject Leader, GameObject Follower)
    {
      GameManager.Instance.PushGameView("Equipment");
      ScreenBuffer screenBuffer = ScreenBuffer.GetScrapBuffer1();
      Keys keys = Keys.None;
      int selectedPart = 0;
      int windowStart = 0;
      ScreenTab screenTab = ScreenTab.Equipment;
      bool Done = false;
      Body body = Follower.Body;
start:
      bool HasCybernetics = false;
      List<BodyPart> bodyPartList = new List<BodyPart>();
      List<GameObject> gameObjectList1 = new List<GameObject>();
      List<GameObject> gameObjectList2 = new List<GameObject>();
      List<GameObject> gameObjectList3 = new List<GameObject>();
      Dictionary<char, int> dictionary = new Dictionary<char, int>();
      foreach (BodyPart loopPart in body.LoopParts())
      {
        if (screenTab == ScreenTab.Equipment)
        {
          if (loopPart.Equipped != null)
          {
            bodyPartList.Add(loopPart);
            gameObjectList2.Add(loopPart.Equipped);
            gameObjectList3.Add(loopPart.Equipped);
            gameObjectList1.Add((GameObject) null);
          }
          else if (loopPart.DefaultBehavior != null)
          {
            bodyPartList.Add(loopPart);
            gameObjectList2.Add(loopPart.DefaultBehavior);
            gameObjectList3.Add((GameObject) null);
            gameObjectList1.Add((GameObject) null);
          }
          else
          {
            bodyPartList.Add(loopPart);
            gameObjectList2.Add((GameObject) null);
            gameObjectList3.Add((GameObject) null);
            gameObjectList1.Add((GameObject) null);
          }
        }
        if (loopPart.Cybernetics != null)
        {
          if (screenTab == ScreenTab.Cybernetics)
          {
            bodyPartList.Add(loopPart);
            gameObjectList2.Add((GameObject) null);
            gameObjectList3.Add(loopPart.Cybernetics);
            gameObjectList1.Add(loopPart.Cybernetics);
          }
          HasCybernetics = true;
        }
      }
      bool CanChangePrimaryLimb = !Follower.AreHostilesNearby();
      while (!Done)
      {
        Event.ResetPool(false);
        List<GameObject> list = Event.NewGameObjectList();
        if (!XRLCore.Core.Game.Running)
        {
          GameManager.Instance.PopGameView();
          return ScreenReturn.Exit;
        }
        screenBuffer.Clear();
        screenBuffer.SingleBox(0, 0, 79, 24, ColorUtility.MakeColor(TextColor.Grey, TextColor.Black));
        if (screenTab == ScreenTab.Equipment)
        {
          screenBuffer.Goto(35, 0);
          screenBuffer.Write("[ {{W|Equipment}} ]");
        }
        else
        {
          screenBuffer.Goto(35, 0);
          screenBuffer.Write("[ {{W|Cybernetics}} ]");
        }
        screenBuffer.Goto(60, 0);
        screenBuffer.Write(" {{W|ESC}} or {{W|5}} to exit ");
        screenBuffer.Goto(25, 24);
        if (CanChangePrimaryLimb && !bodyPartList[selectedPart].Abstract) {
          screenBuffer.Write("[{{W|Tab}} - Set primary limb]");
        } else {
          screenBuffer.Write("[{{K|Tab - Set primary limb}}]");
        }
        int rowCount = 22;
        int y = 2;
        if (HasCybernetics)
        {
          screenBuffer.Goto(3, y);
          if (screenTab == ScreenTab.Cybernetics)
            screenBuffer.Write("{{K|Equipment}} {{Y|Cybernetics}}");
          else
            screenBuffer.Write("{{Y|Equipment}} {{K|Cybernetics}}");
          rowCount -= 2;
          y += 2;
        }
        if (bodyPartList != null)
        {
          dictionary.Clear();
          for (int i = windowStart; i < bodyPartList.Count && i - windowStart < rowCount; ++i)
          {
            if (selectedPart == i)
            {
              screenBuffer.Goto(27, y + i - windowStart);
              screenBuffer.Write("{{K|>}}");
            }
            screenBuffer.Goto(1, y + i - windowStart);
            string str = selectedPart != i ? " {{w|" : "{{Y|>}}{{W|";
            string prefix = "";
            if (gameObjectList1[i] == null && Options.IndentBodyParts)
            {
              prefix += new string(' ', body.GetPartDepth(bodyPartList[i]));
            }
            prefix += bodyPartList[i].GetCardinalDescription();
            if (bodyPartList[i].Primary)
              prefix += " {{G|*}}";
            char key = (char) ('a' + i);
            if (key > 'z')
              key = ' ';
            else
              dictionary.Add(key, i);
            if (gameObjectList1[i] != null)
            {
              if (selectedPart == i)
              {
                screenBuffer.Write(str + key.ToString() + "}}) " + prefix);
                screenBuffer.Goto(28, y + i - windowStart);
                screenBuffer.Write((IRenderable) gameObjectList1[i].RenderForUI());
                screenBuffer.Goto(30, y + i - windowStart);
                screenBuffer.Write(gameObjectList1[i].DisplayName);
              }
              else
              {
                screenBuffer.Write(str + "}}{{K|" + key.ToString() + ") " + prefix + "}}");
                screenBuffer.Goto(28, y + i - windowStart);
                screenBuffer.Write((IRenderable) gameObjectList1[i].RenderForUI(), ColorString: "&K", TileColor: "&K", DetailColor: new char?('K'));
                screenBuffer.Goto(30, y + i - windowStart);
                screenBuffer.Write("{{K|" + gameObjectList1[i].DisplayNameStripped + "}}");
              }
            }
            else
            {
              screenBuffer.Write(str + key.ToString() + "}}) " + prefix);
              if (bodyPartList[i].Equipped == null)
              {
                if (bodyPartList[i].DefaultBehavior == null)
                {
                  screenBuffer.Goto(28, y + i - windowStart);
                  if (selectedPart == i)
                    screenBuffer.Write("{{Y|-}}");
                  else
                    screenBuffer.Write("{{K|-}}");
                }
                else
                {
                  screenBuffer.Goto(28, y + i - windowStart);
                  screenBuffer.Write((IRenderable) bodyPartList[i].DefaultBehavior.RenderForUI(), ColorString: "&K", TileColor: "&K", DetailColor: new char?('K'));
                  screenBuffer.Write(" ");
                  screenBuffer.Goto(30, y + i - windowStart);
                  screenBuffer.Write("{{K|" + bodyPartList[i].DefaultBehavior.DisplayName + "}}");
                }
              }
              else if (bodyPartList[i].Equipped != null && list.CleanContains<GameObject>(bodyPartList[i].Equipped))
              {
                screenBuffer.Goto(28, y + i - windowStart);
                screenBuffer.Write((IRenderable) bodyPartList[i].Equipped.RenderForUI(), ColorString: "&K", TileColor: "&K", DetailColor: new char?('K'));
                screenBuffer.Write(" ");
                screenBuffer.Goto(30, y + i - windowStart);
                screenBuffer.Write("{{K|" + bodyPartList[i].Equipped.DisplayNameStripped + "}}");
              }
              else if (bodyPartList[i].Equipped != null && bodyPartList[i].Equipped.HasTag("RenderImplantGreyInEquipment") && !list.CleanContains<GameObject>(bodyPartList[i].Equipped))
              {
                if (bodyPartList[i].Equipped.HasPart("Cybernetics2BaseItem"))
                {
                  if ((bodyPartList[i].Equipped.GetPart("Cybernetics2BaseItem") as Cybernetics2BaseItem).ImplantedOn != null)
                  {
                    screenBuffer.Goto(28, y + i - windowStart);
                    screenBuffer.Write((IRenderable) bodyPartList[i].Equipped.RenderForUI(), ColorString: "&K", TileColor: "&K", DetailColor: new char?('K'));
                    screenBuffer.Write(" ");
                    screenBuffer.Goto(30, y + i - windowStart);
                    screenBuffer.Write("{{K|" + bodyPartList[i].Equipped.DisplayNameStripped + "}}");
                  }
                  else
                  {
                    list.Add(bodyPartList[i].Equipped);
                    screenBuffer.Goto(28, y + i - windowStart);
                    screenBuffer.Write((IRenderable) bodyPartList[i].Equipped.RenderForUI());
                    screenBuffer.Write(" ");
                    screenBuffer.Goto(30, y + i - windowStart);
                    screenBuffer.Write(bodyPartList[i].Equipped.DisplayName);
                  }
                }
              }
              else
              {
                list.Add(bodyPartList[i].Equipped);
                screenBuffer.Goto(28, y + i - windowStart);
                screenBuffer.Write((IRenderable) bodyPartList[i].Equipped.RenderForUI());
                screenBuffer.Goto(30, y + i - windowStart);
                screenBuffer.Write(bodyPartList[i].Equipped.DisplayName);
              }
            }
          }
          if (windowStart + rowCount < bodyPartList.Count)
          {
            screenBuffer.Goto(2, 24);
            screenBuffer.Write("<more...>");
          }
          if (windowStart > 0)
          {
            screenBuffer.Goto(2, 0);
            screenBuffer.Write("<more...>");
          }
          Popup._TextConsole.DrawBuffer(screenBuffer);
          keys = Keyboard.getvk(Options.MapDirectionsToKeypad);
          ScreenBuffer.ClearImposterSuppression();
          char key1 = (char.ToLower((char) Keyboard.Char).ToString() + " ").ToLower()[0];
          if (keys == Keys.MouseEvent && Keyboard.CurrentMouseEvent.Event == "RightClick")
            Done = true;
          if (keys == Keys.Escape || keys == Keys.NumPad5)
            Done = true;
          else if (keys == Keys.NumPad7 || keys == Keys.NumPad9 && Keyboard.RawCode != Keys.Prior && Keyboard.RawCode != Keys.Next)
            Done = true;
          else if (keys == Keys.Prior)
          {
            selectedPart = 0;
            windowStart = 0;
          }
          else if ((keys == Keys.NumPad4 || keys == Keys.NumPad6) && HasCybernetics)
          {
            screenTab = screenTab != ScreenTab.Cybernetics ? ScreenTab.Cybernetics : ScreenTab.Equipment;
            selectedPart = 0;
            windowStart = 0;
            goto start;
          }
          switch (keys)
          {
            case Keys.Next:
              selectedPart = bodyPartList.Count - 1;
              if (bodyPartList.Count > rowCount)
              {
                windowStart = bodyPartList.Count - rowCount;
                goto case Keys.F2;
              }
              else
                goto case Keys.F2;
            case Keys.NumPad8:
              if (selectedPart - windowStart <= 0)
              {
                if (windowStart > 0)
                {
                  --windowStart;
                  goto case Keys.F2;
                }
                else
                  goto case Keys.F2;
              }
              else if (selectedPart > 0)
              {
                --selectedPart;
                goto case Keys.F2;
              }
              else
                goto case Keys.F2;
            case Keys.F2:
              if (keys == Keys.Enter)
                keys = Keys.Space;
              if (keys == Keys.Tab)
                bodyPartList[selectedPart].SetAsPreferredDefault();
              if ((keys == Keys.Space || keys >= Keys.A && keys <= Keys.Z) && (dictionary.ContainsKey(key1) || keys == Keys.Space))
              {
                int index2 = keys != Keys.Space ? dictionary[key1] : selectedPart;
                if (gameObjectList3[index2] != null)
                {
                  if (keys == Keys.Tab)
                  {
                    InventoryActionEvent.Check(gameObjectList2[index2], XRLCore.Core.Game.Player.Body, gameObjectList2[index2], "Look");
                    goto start;
                  }
                  else
                  {
                    EquipmentAPI.TwiddleObject(Follower, gameObjectList3[index2], ref Done);
                    goto start;
                  }
                }
                else if (keys != Keys.Tab)
                {
                  FollowerEquipmentScreen.ShowBodypartEquipUI(Leader, Follower, bodyPartList[index2]);
                  goto start;
                }
                else
                  goto start;
              }
              else
                continue;
            default:
              if (keys == Keys.NumPad2 && selectedPart < bodyPartList.Count - 1)
              {
                if (selectedPart - windowStart == rowCount - 1)
                  ++windowStart;
                ++selectedPart;
                goto case Keys.F2;
              }
              else if ((Keyboard.vkCode == Keys.Left || keys == Keys.NumPad4) && gameObjectList2[selectedPart] != null)
              {
                BodyPart bodyPart = bodyPartList[selectedPart];
                Follower.FireEvent(Event.New("CommandUnequipObject", "BodyPart", (object) bodyPart));
                goto start;
              }
              else
                goto case Keys.F2;
          }
        }
      }
      GameManager.Instance.PopGameView();
      if (keys == Keys.NumPad7)
        return ScreenReturn.Previous;
      return keys == Keys.NumPad9 ? ScreenReturn.Next : ScreenReturn.Exit;
    }

    private enum ScreenTab
    {
      Equipment,
      Cybernetics,
    }
  }
}

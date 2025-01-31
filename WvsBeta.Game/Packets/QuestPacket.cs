﻿using System;
using System.Collections.Generic;
using System.Linq;
using WvsBeta.Common;
using WvsBeta.Common.Enums;
using WvsBeta.Common.Objects;
using WvsBeta.Common.Sessions;
using WvsBeta.Game.Packets;

namespace WvsBeta.Game
{
    public class QuestPacket : Packet
    {
        private QuestPacket(short questID, QuestState state) : base(ServerMessages.MESSAGE)
        {
            WriteByte((byte)MessageType.QuestRecord);
            WriteShort(questID);
            WriteByte((byte)state);
        }

        public static void SendRemoveQuest(GameCharacter chr, short questID)
        {
            var pw = new QuestPacket(questID, QuestState.Available);
            chr.SendPacket(pw);
        }
        public static void SendQuestUpdateData(GameCharacter chr, short questID, string data)
        {
            var pw = new QuestPacket(questID, QuestState.InProgress);
            pw.WriteString(data);
            chr.SendPacket(pw);
        }

        public static void SendCompleteQuest(GameCharacter chr, short questID, long filetime)
        {
            var pw = new QuestPacket(questID, QuestState.Completed);
            pw.WriteLong(filetime);
            chr.SendPacket(pw);
        }

        public static void SendGainItemChat(GameCharacter chr, params KeyValuePair<int, int>[] pItems)
        {
            Packet pw = new Packet(ServerMessages.PLAYER_EFFECT);
            pw.WriteByte(0x03);
            pw.WriteByte((byte)pItems.Length);
            foreach (var kvp in pItems)
            {
                pw.WriteInt(kvp.Key);
                pw.WriteInt(kvp.Value);
            }
            chr.SendPacket(pw);
        }
        public static void QuestClear(GameCharacter chr, short qid)
        {
            var pw = new Packet(ServerMessages.QUEST_CLEAR);
            pw.WriteShort(qid);
            chr.SendPacket(pw);
        }
        public static void SendQuestActionResultError(GameCharacter chr, QuestActionResult result)
        {
            SendQuestActionResult(chr, result, 0, 0);
        }
        public static void SendQuestActionResult(GameCharacter chr, QuestActionResult result, int npcID, short currentQuest, params short[] nextQuests)
        {
            var pw = new Packet(ServerMessages.QUEST_ACTION_RESULT);

            pw.WriteByte((byte)result);
            pw.WriteShort(currentQuest); // Send 0 to ignore
            if (result == QuestActionResult.Success)
            {
                pw.WriteInt(npcID);
                for (int i = 0; i < nextQuests.Length; i++)
                {
                    pw.WriteShort(nextQuests[i]); // Next quest
                }
                pw.WriteShort(0);
            }
            chr.SendPacket(pw);
        }
        public enum QuestActionResult
        {
            // < 4 = Nothing happens (0xFF > 9 so it goto END;)
            // 4 = Complete with npc window
            // 5 = The quest has ended due to an unknown error.
            // 6 = Item inventory is full
            // 7 = You have not enough mesos.
            // 8 = Failed to retrieve the equipped item. Please unequip it first.
            // 9 = You cannot carry more than one for this item.
            Success = 4,
            UnknownError = 5,
            InventoryFull = 6,
            NotEnoughMesos = 7,
            FailedRetrieveEquippedItem = 8,
            CannotCarryMoreThanOne = 9
        }
        class QuestException : Exception
        {
            public QuestActionResult Result { get; private set; }
            public QuestException(QuestActionResult result)
            {
                Result = result;
            }
        }
        public static void HandleQuestCheck(GameCharacter chr, WZQuestCheck check)
        {
            foreach (var item in check.Items.Select(i => i.Value)) { if (!chr.Inventory.CanExchange(0, (item.ItemID, (short)-item.Amount))) throw new QuestException(QuestActionResult.UnknownError); }
            if (check.Mesos > 0 && !chr.Inventory.CanExchange(-check.Mesos)) throw new QuestException(QuestActionResult.NotEnoughMesos);
            if (check.LvMin > 0 && chr.Level < check.LvMin) throw new QuestException(QuestActionResult.UnknownError);
            if (check.LvMax > 0 && chr.Level > check.LvMax) throw new QuestException(QuestActionResult.UnknownError);

            foreach (var qt in check.Quests)
            {
                if (qt.NewState == QuestState.Completed)
                {
                    chr.Quests.SetComplete(qt.QuestID);
                }
            }
        }
        public static void HandleQuestAct(GameCharacter chr, int npcid, WZQuestAct act)
        {
            int nMax = act.Items.Sum(i => i.Prop);
            int n = Rand32.NextBetween(0, nMax);
            int from = 0;
            int to = 0;
            var itemsToGive = new List<QuestItem>();
            foreach (QuestItem item in act.Items)
            {
                if (item.Prop > 0)
                {
                    to += item.Prop;
                    bool win = from <= n && n < to;
                    from += item.Prop;
                    if (!win) continue;
                }
                if (!chr.Inventory.CanExchange(0, (item.ItemID, item.Amount))) throw new QuestException(QuestActionResult.InventoryFull);
                itemsToGive.Add(item);
            }
            if (act.Mesos > 0 && !chr.Inventory.CanExchange(act.Mesos)) throw new QuestException(QuestActionResult.UnknownError);
            if (act.Exp > 0 && chr.Level == 200) throw new QuestException(QuestActionResult.UnknownError);

            if (act.Items.Count > 0)
            {
                foreach (QuestItem item in itemsToGive)
                {
                    chr.Inventory.MassExchange(0, (item.ItemID, item.Amount));
                }
            }
            if (act.Mesos != 0)
            {
                chr.IncMoney(act.Mesos, MessageAppearType.ChatGrey);
            }
            if (act.Exp != 0) chr.IncEXP(act.Exp, MessageAppearType.ChatGrey);

            if (act.Stage.Stage == QuestStage.Start)
            {
                chr.Quests.StartQuest(act.Stage.Quest.QuestID);
                SendQuestActionResult(chr, QuestActionResult.Success, npcid, act.Stage.Quest.QuestID);
            }
            else if (act.Stage.Stage == QuestStage.Complete)
            {
                if (act.NextQuest > 0)
                {
                    if (!DataProvider.Quests.TryGetValue(act.NextQuest, out WZQuestData nextQuest)) throw new QuestException(QuestActionResult.UnknownError);
                    SendQuestActionResult(chr, QuestActionResult.Success, npcid, 0, act.NextQuest);
                }
                else
                {
                    chr.Quests.SetComplete(act.Stage.Quest.QuestID);
                    SendQuestActionResult(chr, QuestActionResult.Success, npcid, act.Stage.Quest.QuestID);
                }
            }
        }
        public static void HandleQuestStage(GameCharacter chr, int npcid, WZQuestStage stage)
        {
            try
            {
                HandleQuestCheck(chr, stage.Check);
                HandleQuestAct(chr, npcid, stage.Act);
            }
            catch (QuestException e)
            {
                SendQuestActionResultError(chr, e.Result);
            }
        }
        public static void HandleAction(GameCharacter chr, Packet packet)
        {
            byte type = packet.ReadByte(); // 0 = lost item, 1 = start, 2 = complete, 3 = forfeit
            short qid = packet.ReadShort();
            switch (type)
            {
                case 0:
                    {
                        // lost item [42] [00] [E9 03] [01 00 00 00] [1B 82 3D 00]
                        int amount = packet.ReadInt();
                        int itemid = packet.ReadInt();
                        if (!chr.Inventory.MassExchange(0, (itemid, (short)amount)))
                        {
                            SendQuestActionResultError(chr, QuestActionResult.InventoryFull);
                        }
                        break;
                    }
                case 1:
                    {
                        int npcid = packet.ReadInt();
                        // start quest [42] [01] [E8 03] [35 08 00 00]
                        if (!DataProvider.Quests.TryGetValue(qid, out WZQuestData qd))
                        {
                            SendQuestActionResultError(chr, QuestActionResult.UnknownError);
                            return;
                        }
                        HandleQuestStage(chr, npcid, qd.Stages[QuestStage.Start]);
                    }
                    break;
                case 2:
                    {
                        int npcid = packet.ReadInt();
                        // complete quest [42] [02] [E8 03] [34 08 00 00] [FF FF FF FF]
                        packet.ReadInt();

                        if (!DataProvider.Quests.TryGetValue(qid, out WZQuestData qd))
                        {
                            SendQuestActionResultError(chr, QuestActionResult.UnknownError);
                            return;
                        }
                        HandleQuestStage(chr, npcid, qd.Stages[QuestStage.Complete]);
                    }
                    break;
                case 3: // forfeit quest [42] [03] [EB 03]
                    chr.Quests.RemoveQuest(qid);
                    break;
                default:
                    break;
            }
        }
    }
}

﻿using System;
using WvsBeta.Common.Character;
using WvsBeta.Common.Sessions;

namespace WvsBeta.Common
{
    public class CharacterBase : MovableLife
    {
        public string Name { get => CharacterStat.Name; set => CharacterStat.Name = value; }
        public int ID { get => CharacterStat.ID; set => CharacterStat.ID = value; }
        public short Job { get => CharacterStat.Job; set => CharacterStat.Job = value; }
        public byte Level { get => CharacterStat.Level; set => CharacterStat.Level = value; }

        public byte Gender { get => CharacterStat.Gender; set => CharacterStat.Gender = value; }
        public byte Skin { get => CharacterStat.Skin; set => CharacterStat.Skin = value; }
        public int Face { get => CharacterStat.Face; set => CharacterStat.Face = value; }
        public int Hair { get => CharacterStat.Hair; set => CharacterStat.Hair = value; }
        public int MapID { get => CharacterStat.MapID; set => CharacterStat.MapID = value; }
        public byte PortalID { get; set; }
        public virtual int PartyID { get; set; }

        public bool IsOnline { get; set; }

        public byte GMLevel { get; set; }
        public bool IsGM { get => GMLevel > 0; }
        public bool IsAdmin { get => GMLevel >= 3; }
        public virtual BaseCharacterInventory Inventory { get; set; }
        public virtual BaseCharacterSkills Skills { get; protected set; }
        public virtual BaseCharacterPrimaryStats PrimaryStats { get; protected set; }
        public virtual BaseCharacterQuests Quests { get; protected set; }
        public virtual void DamageHP(short amount) => throw new NotImplementedException();
        public GW_CharacterStat CharacterStat { get; } = new GW_CharacterStat();
        public byte BuddyListCapacity { get; set; }
        public void EncodeForTransfer(Packet pw)
        {
            pw.WriteString(CharacterStat.Name);
            pw.WriteInt(CharacterStat.ID);
            pw.WriteShort(CharacterStat.Job);
            pw.WriteByte(CharacterStat.Level);

            pw.WriteByte(CharacterStat.Gender);
            pw.WriteByte(CharacterStat.Skin);
            pw.WriteInt(CharacterStat.Face);
            pw.WriteInt(CharacterStat.Hair);

            pw.WriteInt(CharacterStat.MapID);
            pw.WriteInt(PartyID);
            pw.WriteBool(IsOnline);
            pw.WriteByte(GMLevel);
        }


        public void DecodeForTransfer(Packet pr)
        {
            CharacterStat.Name = pr.ReadString();
            CharacterStat.ID = pr.ReadInt();
            CharacterStat.Job = pr.ReadShort();
            CharacterStat.Level = pr.ReadByte();

            CharacterStat.Gender = pr.ReadByte();
            CharacterStat.Skin = pr.ReadByte();
            CharacterStat.Face = pr.ReadInt();
            CharacterStat.Hair = pr.ReadInt();

            CharacterStat.MapID = pr.ReadInt();
            PartyID = pr.ReadInt();
            IsOnline = pr.ReadBool();
            GMLevel = pr.ReadByte();
        }
    }
}

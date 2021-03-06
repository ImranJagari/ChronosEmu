﻿using Chronos.ORM;
using Chronos.ORM.SubSonic.SQLGeneration.Schema;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chronos.Server.Databases.Items
{
    [TableName("characters_items")]
    public class BagItemRecord : ItemStorage<BagItemRecord>, IAutoGeneratedRecord
    {        
        public int Hitpoint { get; set; }
        public int MaxHitpoint { get; set; }
        public uint Word { get; set; }
        public int Option { get; set; }
        public byte ItemResist { get; set; }
        public byte ItemResistAbilityOption { get; set; }
        public int KeepTime { get; set; }
        public byte ItemLock { get; set; }
        public int BindEndTime { get; set; }
        public byte Stability { get; set; }
        public byte Quality { get; set; }
        public sbyte AbilityRate { get; set; }
        public int UseTime { get; set; }
        public int BuyTm { get; set; }
        public int Price { get; set; }
        public int PriceToken { get; set; }
        public int PriceFreeToken { get; set; }
    }
    public class ItemRecordRelator
    {
        public const string FetchQueryByOwnerId = "SELECT * FROM characters_items WHERE OwnerId={0}";
    }
}

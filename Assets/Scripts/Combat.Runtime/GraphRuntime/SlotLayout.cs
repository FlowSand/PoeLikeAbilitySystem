namespace Combat.Runtime.GraphRuntime
{
    public readonly struct SlotLayout
    {
        public readonly int numberSlotCount;
        public readonly int entitySlotCount;
        public readonly int damageSpecSlotCount;

        public SlotLayout(int numberSlotCount, int entitySlotCount, int damageSpecSlotCount)
        {
            this.numberSlotCount = numberSlotCount;
            this.entitySlotCount = entitySlotCount;
            this.damageSpecSlotCount = damageSpecSlotCount;
        }
    }
}


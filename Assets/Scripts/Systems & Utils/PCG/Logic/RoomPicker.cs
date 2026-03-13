using System;
using System.Collections.Generic;
using UnityEngine;

namespace PCG.RoomAssembler.Logic
{
    public class RoomPicker
    {
        private readonly System.Random rng;

        public RoomPicker(System.Random rng)
        {
            this.rng = rng;
        }

        public RoomDefinition PickNonEndRoom(
            List<RoomDefinition> pool,
            bool endPlaced)
        {
            if (!endPlaced)
                return WeightedPickFiltered(pool, r => r != null && !r.isDeadEnd);

            return WeightedPick(pool);
        }

        public RoomDefinition PickRoomWithBiasToEnd(
            List<RoomDefinition> pool,
            RoomDefinition endRoom,
            bool forceEnd)
        {
            if (forceEnd) return endRoom;

            // 20% chance after minSteps
            if (rng.Next(0, 100) < 20) return endRoom;

            return WeightedPick(pool);
        }

        private RoomDefinition WeightedPick(List<RoomDefinition> list)
        {
            if (list == null || list.Count == 0) return null;

            int total = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == null) continue;
                total += Mathf.Max(1, list[i].weight);
            }

            if (total <= 0) return null;

            int roll = rng.Next(0, total); // Roll determines which room we pick, weighted by their individual weights
            int sum = 0;

            for (int i = 0; i < list.Count; i++)
            {
                var r = list[i];
                if (r == null) continue;

                sum += Mathf.Max(1, r.weight);
                if (roll < sum) return r;
            }

            return list[list.Count - 1];
        }

        private RoomDefinition WeightedPickFiltered(
            List<RoomDefinition> list,
            Func<RoomDefinition, bool> predicate)
        {
            var filtered = new List<RoomDefinition>();
            for (int i = 0; i < list.Count; i++)
            {
                var r = list[i];
                if (r == null) continue;
                if (predicate(r)) filtered.Add(r);
            }
            return WeightedPick(filtered);
        }

        public RoomDefinition PickAny(List<RoomDefinition> pool)
        {
            return WeightedPick(pool);
        }
    }
}

using OnixRuntime.Api.Maths;
using OnixRuntime.Api.World;
using VeinMiner.Helpers;

namespace VeinMiner {
    public class MiningManager {
        public List<BlockPos> BlocksToMine { get; } = [];
        public List<BlockPos> ItemsToTeleport { get; } = [];
        public List<ulong> TeleportedActorIds { get; } = [];
        public bool IsVeinMining { get; set; }
        public bool IsProcessingVeinMining { get; private set; }
        public int BreakDelay { get; set; }

        public void ProcessOreVein(WorldBlocks region, BlockPos position, string baseOreType) {
            IsProcessingVeinMining = true;
            BlocksToMine.Clear();
            ItemsToTeleport.Clear();
            TeleportedActorIds.Clear();
            
            ItemsToTeleport.Add(position);
            
            HashSet<BlockPos> visited = [];
            HashSet<BlockPos> blocksToMineSet = [];
            
            FindConnectedOres(region, position, baseOreType, blocksToMineSet, visited);
            
            BlocksToMine.AddRange(blocksToMineSet);
            if (blocksToMineSet.Count > 0) {
                ItemsToTeleport.AddRange(blocksToMineSet);
            }
            
            IsProcessingVeinMining = false;
            
            IsVeinMining = blocksToMineSet.Count > 0;
        }
        
        public void ProcessLogVein(WorldBlocks region, BlockPos position, string woodType, bool breakLeaves = false, int maxLeafDistance = 8) {
            IsProcessingVeinMining = true;
            BlocksToMine.Clear();
            ItemsToTeleport.Clear();
            TeleportedActorIds.Clear();
            
            ItemsToTeleport.Add(position);
            
            HashSet<BlockPos> visited = [];
            HashSet<BlockPos> blocksToMineSet = [];
            HashSet<BlockPos> leavesToMineSet = [];
            
            bool isTreeValid = ValidateTree(region, position, woodType);
            
            if (isTreeValid) {
                FindConnectedLogs(region, position, woodType, blocksToMineSet, visited);
                
                if (breakLeaves) {
                    foreach (BlockPos logPos in blocksToMineSet) {
                        FindConnectedLeaves(region, logPos, woodType, leavesToMineSet, visited, maxLeafDistance);
                    }
                    
                    blocksToMineSet.UnionWith(leavesToMineSet);
                }
            }
            
            BlocksToMine.AddRange(blocksToMineSet);
            ItemsToTeleport.AddRange(blocksToMineSet);
            
            IsProcessingVeinMining = false;
            
            if (BlocksToMine.Count > 0) {
                IsVeinMining = true;
                BreakDelay = 0;
            }
            else {
                IsVeinMining = false;
            }
        }
        
        private bool ValidateTree(WorldBlocks region, BlockPos startPos, string woodType) {
            bool hasLeavesNearby = false;
            const int searchRadius = 4;
            const int maxChecks = 200;
            int checkCount = 0;
            
            for (int x = -searchRadius; x <= searchRadius && !hasLeavesNearby && checkCount < maxChecks; x++) {
                for (int y = 0; y <= searchRadius*2 && !hasLeavesNearby && checkCount < maxChecks; y++) {
                    for (int z = -searchRadius; z <= searchRadius && !hasLeavesNearby && checkCount < maxChecks; z++) {
                        checkCount++;
                        if (x == 0 && y == 0 && z == 0) continue;
                        
                        try {
                            BlockPos checkPos = new(startPos.X + x, startPos.Y + y, startPos.Z + z);
                            
                            if (checkPos.Y is < -60 or > 320) continue;
                            
                            Block block = region.GetBlock(checkPos);
                            if (BlockHelper.IsMatchingLeaves(block.Name, woodType)) {
                                hasLeavesNearby = true;
                                break;
                            }
                        }
                        catch {
                            continue;
                        }
                    }
                }
            }
            
            return hasLeavesNearby || startPos.Y <= 62;
        }
        
        private static void FindConnectedOres(WorldBlocks region, BlockPos startPos, string oreType, HashSet<BlockPos> blocksToMine, HashSet<BlockPos> visited, int maxBlocks = 128) {
            Queue<BlockPos> queue = new();
            queue.Enqueue(startPos);
            visited.Add(startPos);
            
            while (queue.Count > 0 && blocksToMine.Count < maxBlocks) {
                BlockPos current = queue.Dequeue();
                
                for (int x = -1; x <= 1; x++) {
                    for (int y = -1; y <= 1; y++) {
                        for (int z = -1; z <= 1; z++) {
                            if (x == 0 && y == 0 && z == 0) continue;
                            BlockPos pos = new(current.X + x, current.Y + y, current.Z + z);
                            
                            if (!visited.Add(pos)) continue;

                            Block adjacentBlock = region.GetBlock(pos);
                            string adjacentBlockName = adjacentBlock.Name;
                            
                            bool isMatchingOre = BlockHelper.IsMatchingOre(adjacentBlockName, oreType);
                            
                            if (!isMatchingOre) continue;
                            blocksToMine.Add(pos);
                            queue.Enqueue(pos);
                        }
                    }
                }
            }
        }
        
        private static void FindConnectedLogs(WorldBlocks region, BlockPos startPos, string woodType, HashSet<BlockPos> blocksToMine, HashSet<BlockPos> visited, int maxBlocks = 64) {
            Queue<BlockPos> queue = new();
            queue.Enqueue(startPos);
            visited.Add(startPos);
            
            while (queue.Count > 0 && blocksToMine.Count < maxBlocks) {
                BlockPos current = queue.Dequeue();
                
                for (int x = -1; x <= 1; x++) {
                    for (int y = -1; y <= 1; y++) {
                        for (int z = -1; z <= 1; z++) {
                            if (x == 0 && y == 0 && z == 0) continue;
                            
                            BlockPos pos = new(current.X + x, current.Y + y, current.Z + z);
                            
                            if (!visited.Add(pos)) continue;

                            Block adjacentBlock = region.GetBlock(pos);
                            string adjacentBlockName = adjacentBlock.Name;
                            
                            bool isMatchingLog = BlockHelper.IsMatchingLogAndWood(adjacentBlockName, woodType);
                            
                            if (!isMatchingLog) continue;
                            blocksToMine.Add(pos);
                            queue.Enqueue(pos);
                        }
                    }
                }
            }
        }
        
        private void FindConnectedLeaves(WorldBlocks region, BlockPos startPos, string woodType, HashSet<BlockPos> leavesToMine, HashSet<BlockPos> visited, int maxDistance = 8, int maxLeafBlocks = 512) {
            if (leavesToMine.Count >= maxLeafBlocks) return;
            
            Queue<BlockPos> queue = new();
            Dictionary<BlockPos, int> distanceFromLog = new();
            
            queue.Enqueue(startPos);
            distanceFromLog[startPos] = 0;
            
            int checkCount = 0;
            const int maxChecks = 1000;
            
            while (queue.Count > 0 && leavesToMine.Count < maxLeafBlocks && checkCount < maxChecks) {
                checkCount++;
                BlockPos current = queue.Dequeue();
                int currentDistance = distanceFromLog[current];
                
                if (currentDistance >= maxDistance) continue;
                
                for (int x = -1; x <= 1; x++) {
                    for (int y = -1; y <= 1; y++) {
                        for (int z = -1; z <= 1; z++) {
                            if (x == 0 && y == 0 && z == 0) continue;
                            
                            BlockPos pos = new(current.X + x, current.Y + y, current.Z + z);
                            
                            if (pos.Y is < -64 or > 364) continue;
                            
                            if (!visited.Add(pos)) continue;

                            try {
                                Block adjacentBlock = region.GetBlock(pos);
                                string adjacentBlockName = adjacentBlock.Name;
                                
                                if (BlockHelper.IsMatchingLeaves(adjacentBlockName, woodType)) {
                                    leavesToMine.Add(pos);
                                    queue.Enqueue(pos);
                                    distanceFromLog[pos] = currentDistance + 1;
                                }
                            }
                            catch {
                                continue;
                            }
                        }
                    }
                }
            }
        }
    }
}

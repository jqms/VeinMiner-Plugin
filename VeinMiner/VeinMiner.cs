using System.Text.Json.Nodes;
using OnixRuntime.Api;
using OnixRuntime.Api.Entities;
using OnixRuntime.Api.Maths;
using OnixRuntime.Api.Rendering;
using OnixRuntime.Api.UI;
using OnixRuntime.Api.Utils;
using OnixRuntime.Api.World;
using OnixRuntime.Plugin;
using VeinMiner.Helpers;

namespace VeinMiner {
    public class VeinMiner : OnixPluginBase {
        private readonly MiningManager _miningManager = new();
        private Vec3 _playerPosition = new(0, 0, 0);
        private Vec3 _firstBlockPosition = new(0, 0, 0);
        private int _itemCheckCounter;
        private const int ItemCheckFrequency = 2;
        private int _postMiningDelay;
        private const int PostMiningDelay = 60;
        
        public enum ItemTeleportMode {
            NoTeleport,
            ToFirstBlock,
            ToPlayer
        }

        private static VeinMinerConfig Config { get; set; } = null!;

        public VeinMiner(OnixPluginInitInfo initInfo) : base(initInfo) {
            DisablingShouldUnloadPlugin = false;
        }

        protected override void OnLoaded() {
            Config = new VeinMinerConfig(PluginDisplayModule);
            Onix.Events.Player.BreakBlock += PlayerOnBreakBlock;
            Onix.Events.Rendering.HudRenderGame += RenderingOnHudRender;
            Onix.Events.LocalServer.Tick += LocalServerOnTick;
        }
        private void LocalServerOnTick() {
            bool shouldProcessTeleport = (_miningManager.IsVeinMining || _postMiningDelay > 0) && Config.TeleportMode != ItemTeleportMode.NoTeleport && _miningManager.ItemsToTeleport.Count > 0;
            if (shouldProcessTeleport) {
                _itemCheckCounter++;
                if (_itemCheckCounter >= ItemCheckFrequency) {
                    _itemCheckCounter = 0;
                    _playerPosition = Onix.LocalServer!.LocalPlayer!.Position;
                    Vec3 teleportTarget = Config.TeleportMode == ItemTeleportMode.ToFirstBlock ? _firstBlockPosition : _playerPosition;
                    
                    teleportTarget += new Vec3(0, 1f, 0);
                    
                    foreach (Entity entity in Onix.LocalServer.World.Entities) {
                        if (_miningManager.TeleportedActorIds.Contains(entity.UniqueId)) continue;
                        
                        Vec3 entityPos = entity.Position;
                        BlockPos entityBlockPos = new((int)entityPos.X, (int)entityPos.Y, (int)entityPos.Z);
                        bool shouldTeleport = false;
                        
                        if (entity is ItemEntity || entity.TypeName == "xp_orb") {
                            if (_miningManager.ItemsToTeleport.Any(miningPos => Math.Abs(entityBlockPos.X - miningPos.X) <= 3 && Math.Abs(entityBlockPos.Y - miningPos.Y) <= 3 && Math.Abs(entityBlockPos.Z - miningPos.Z) <= 3)) {
                                shouldTeleport = true;
                            }
                        }

                        if (!shouldTeleport) continue;
                        entity.Position = teleportTarget;
                        _miningManager.TeleportedActorIds.Add(entity.UniqueId);
                    }
                }
            }
            
            if (_miningManager is { IsVeinMining: true, BlocksToMine.Count: > 0 }) {
                if (_miningManager.BreakDelay > 0) {
                    _miningManager.BreakDelay--;
                    return;
                }
                
                BlockPos? nearestBlock = null;
                float nearestDistance = float.MaxValue;
                
                BlockPos playerPos = new((int)_playerPosition.X, (int)_playerPosition.Y, (int)_playerPosition.Z);
                
                foreach (BlockPos blockPos in _miningManager.BlocksToMine) {
                    float distance = playerPos.Distance(blockPos);
                    if (!(distance < nearestDistance)) continue;
                    nearestDistance = distance;
                    nearestBlock = blockPos;
                }
                
                if (nearestBlock == null && _miningManager.BlocksToMine.Count > 0) {
                    nearestBlock = _miningManager.BlocksToMine.FirstOrDefault();
                }
                
                if (nearestBlock.HasValue) {
                    string command = $"/setblock {nearestBlock.Value.X} {nearestBlock.Value.Y} {nearestBlock.Value.Z} air destroy";
                    Onix.Game.ExecuteCommand(command);
                    _miningManager.BlocksToMine.Remove(nearestBlock.Value);
                    
                    _miningManager.BreakDelay = 1;
                }

                if (_miningManager.BlocksToMine.Count != 0) return;
                _miningManager.IsVeinMining = false;
                _postMiningDelay = PostMiningDelay;
            } else if (_postMiningDelay > 0) {
                _postMiningDelay--;

                if (_postMiningDelay != 0) return;
                _miningManager.TeleportedActorIds.Clear();
                _miningManager.ItemsToTeleport.Clear();
            }
        }

        private void RenderingOnHudRender(RendererGame gfx, float delta) {
            _playerPosition = Onix.LocalPlayer!.Position;
        }

        private bool PlayerOnBreakBlock(LocalPlayer player, BlockPos position, BlockFace face) {
            if (_miningManager.IsProcessingVeinMining || _miningManager.IsVeinMining)
                return false;
            
            Block block = player.Region.GetBlock(position);
            string blockName = block.Name;
            
            _firstBlockPosition = new Vec3(position.X + 0.5f, position.Y + 0.5f, position.Z + 0.5f);
            
            if (Config.EnableOreMining && blockName.EndsWith("_ore")) {
                string baseOreType = BlockHelper.ExtractOreType(blockName);
                _miningManager.ProcessOreVein(player.Region, position, baseOreType);
                
                if (!_miningManager.IsVeinMining && _miningManager.ItemsToTeleport.Count > 0) {
                    _postMiningDelay = PostMiningDelay;
                }
                return false;
            }

            if (!Config.EnableTreeMining || (!blockName.EndsWith("_log") && !blockName.EndsWith("_stem")))
                return false;
            string woodType = BlockHelper.ExtractLogType(blockName);
            _miningManager.ProcessLogVein(player.Region, position, woodType, Config.BreakLeaves, Config.MaxLeafDistance);

            return false;
        }

        protected override void OnUnloaded() {
            Onix.Events.Player.BreakBlock -= PlayerOnBreakBlock;
            Onix.Events.Rendering.HudRenderGame -= RenderingOnHudRender;
            Onix.Events.LocalServer.Tick -= LocalServerOnTick;
        }
    }
}


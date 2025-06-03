using OnixRuntime.Api.OnixClient;

namespace VeinMiner {
    public partial class VeinMinerConfig : OnixModuleSettingRedirector {
        [Value(true)]
        public partial bool BreakLeaves { get; set; }

        [Value(8)]
        [MinMax(1, 32)]
        public partial int MaxLeafDistance { get; set; }
        
        [Value(nameof(VeinMiner.ItemTeleportMode.ToPlayer))]
        public partial VeinMiner.ItemTeleportMode TeleportMode { get; set; }
        
        [Value(true)]
        public partial bool EnableTreeMining { get; set; }
        
        [Value(true)]
        public partial bool EnableOreMining { get; set; }
    }
}
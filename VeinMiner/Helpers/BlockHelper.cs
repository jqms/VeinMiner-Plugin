namespace VeinMiner.Helpers;

public static class BlockHelper {
    public static string ExtractOreType(string blockName) {
        if (blockName.StartsWith("lit_"))
            blockName = blockName[4..];
            
        if (blockName.StartsWith("deepslate_"))
            blockName = blockName[10..];
            
        int oreIndex = blockName.IndexOf("_ore", StringComparison.Ordinal);
        return oreIndex > 0 ? blockName[..oreIndex] : blockName;
    }

    public static bool IsMatchingOre(string blockName, string oreType) {
        if (blockName == oreType + "_ore") return true;
            
        if (blockName == "deepslate_" + oreType + "_ore") return true;
            
        if (blockName == "lit_" + oreType + "_ore") return true;
            
        return blockName == "lit_deepslate_" + oreType + "_ore";
    }

    public static string ExtractLogType(string blockName) {
        if (blockName.EndsWith("_log"))
            return blockName[..^4];
        else if (blockName.EndsWith("_stem"))
            return blockName[..^5];
        else
            return blockName;
    }

    public static bool IsMatchingLeaves(string blockName, string logType) {
        return blockName == logType + "_leaves";
    }

    public static bool IsMatchingLogAndWood(string blockName, string logType) {
        return blockName == logType + "_log" || blockName == logType + "_stem" || blockName == "stripped_" + logType + "_log" || blockName == "stripped_" + logType + "_stem";
    }
}
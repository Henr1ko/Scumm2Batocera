using System.Collections.Generic;

namespace ScummIdGui.Models
{
    public class ScummGameCatalog
    {
        public string GeneratedAt { get; set; } = string.Empty;
        public string ScummvmPath { get; set; } = string.Empty;
        public string ScummvmVersion { get; set; } = string.Empty;
     public string ListCommand { get; set; } = string.Empty;
      public int GameCount { get; set; }
    public List<ScummGame> Games { get; set; } = new();
    }

    public class ScummGame
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string NormalizedTitle { get; set; } = string.Empty;
        public List<string> Tokens { get; set; } = new();
   
      // Extract short ID (e.g., "atlantis" from "scumm:atlantis")
        public string ShortId => Id.Contains(':') ? Id.Split(':')[1] : Id;
    }

    public class IdentifiedGame
    {
        public string FolderName { get; set; } = string.Empty;
        public string FolderPath { get; set; } = string.Empty;
        public ScummGame? MatchedGame { get; set; }
        public int ConfidenceScore { get; set; }
        public string MatchReason { get; set; } = string.Empty;
        
        public string DisplayName => MatchedGame?.Title ?? FolderName;
        public string ShortId => MatchedGame?.ShortId ?? string.Empty;
        public string FullId => MatchedGame?.Id ?? string.Empty;
    }
}

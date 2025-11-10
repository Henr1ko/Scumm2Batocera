using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using ScummIdGui.Models;

namespace ScummIdGui.Services
{
    public class GameIdentificationEngine
    {
        private readonly List<ScummGame> _games;

   // Common stop words that should be ignored in matching
        private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
        {
    "cd", "dvd", "disc", "disk", "disc1", "disc2", "disk1", "disk2",
 "dos", "windows", "win", "amiga", "atari", "st", "mac", "pc",
            "floppy", "vga", "ega", "cga", "fm", "towns",
      "demo", "english", "eng", "usa", "us", "uk", "de", "fr", "es", "it",
       "multi", "multilang", "multilanguage",
 "install", "v1", "v2", "v3", "version",
       "the", "a", "an", "of", "and", "in"
        };

  // Synonyms and variations
        private static readonly Dictionary<string, string[]> Synonyms = new()
        {
            { "indy", new[] { "indiana", "jones" } },
            { "mi", new[] { "monkey", "island" } },
      { "sam", new[] { "samnmax", "sam&max" } },
     { "dott", new[] { "tentacle", "day" } },
    { "ft", new[] { "fullthrottle", "full" } },
     { "comi", new[] { "curse", "monkey", "island" } }
        };

        public GameIdentificationEngine(List<ScummGame> games)
      {
_games = games ?? throw new ArgumentNullException(nameof(games));
        }

  public static GameIdentificationEngine LoadFromFile(string jsonPath)
        {
 if (!File.Exists(jsonPath))
    {
    throw new FileNotFoundException($"Could not find '{jsonPath}'");
     }

  var json = File.ReadAllText(jsonPath);
            var catalog = JsonSerializer.Deserialize<ScummGameCatalog>(json, new JsonSerializerOptions
      {
          PropertyNameCaseInsensitive = true
          });

            if (catalog == null || catalog.Games == null || catalog.Games.Count == 0)
     {
    throw new InvalidOperationException("Invalid or empty game catalog");
    }

     return new GameIdentificationEngine(catalog.Games);
        }

        public static GameIdentificationEngine LoadDefault()
        {
  var exeDir = AppContext.BaseDirectory;
      var jsonPath = Path.Combine(exeDir, "scummvm-games.json");
    return LoadFromFile(jsonPath);
        }

        public int GameCount => _games.Count;

        public IdentifiedGame? IdentifyGame(string folderName, string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderName))
                return null;

       // Normalize input
            var normalized = NormalizeText(folderName);
            var inputTokens = TokenizeAndFilter(normalized);

     // Try multiple strategies
   var candidates = new List<(ScummGame game, int score, string reason)>();

  foreach (var game in _games)
            {
  int score = 0;
    var reasons = new List<string>();

 // Strategy 1: Exact normalized title match (highest priority)
   if (string.Equals(normalized, game.NormalizedTitle, StringComparison.OrdinalIgnoreCase))
     {
     score += 1000;
          reasons.Add("Exact title match");
            }

     // Strategy 2: Short ID match (IMPROVED - avoid generic matches)
            var shortId = game.ShortId.ToLowerInvariant();
        
        // Only use short ID if it's meaningful (3+ chars) or exact match
             bool isGenericId = shortId.Length <= 2 && !string.Equals(normalized, shortId, StringComparison.OrdinalIgnoreCase);
    
   if (!isGenericId)
   {
if (normalized.Contains(shortId) || shortId.Contains(normalized))
        {
      // Higher score for exact match
       if (string.Equals(normalized, shortId, StringComparison.OrdinalIgnoreCase))
              {
        score += 900;
       reasons.Add($"Exact ID match ({shortId})");
      }
           else
         {
             score += 800;
         reasons.Add($"ID match ({shortId})");
        }
     }
    }

    // Strategy 3: Token overlap
     if (game.Tokens != null && game.Tokens.Count > 0)
         {
       var commonTokens = inputTokens.Intersect(game.Tokens, StringComparer.OrdinalIgnoreCase).ToList();
     var tokenScore = (commonTokens.Count * 150) + (commonTokens.Count * 50 / Math.Max(inputTokens.Count, 1));
     score += tokenScore;
         if (commonTokens.Count > 0)
{
     reasons.Add($"{commonTokens.Count} matching keywords");
 }
      }

 // Strategy 4: Substring matching
     if (!string.IsNullOrEmpty(game.NormalizedTitle))
              {
var gameNorm = game.NormalizedTitle.ToLowerInvariant();
          if (gameNorm.Contains(normalized))
{
          score += 400;
        reasons.Add("Contains folder name");
   }
     else if (normalized.Contains(gameNorm))
           {
         score += 350;
            reasons.Add("Folder contains game title");
      }
    else
         {
     // Check for partial substring matches
   var gameParts = gameNorm.Split(' ', StringSplitOptions.RemoveEmptyEntries);
  var inputParts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            int partialMatches = gameParts.Count(gp => inputParts.Any(ip => 
 (gp.Length > 3 && ip.Contains(gp)) || (ip.Length > 3 && gp.Contains(ip))));
          
    if (partialMatches > 0)
            {
 score += partialMatches * 100;
           reasons.Add($"{partialMatches} partial matches");
                 }
   }
       }

        // Strategy 5: Fuzzy string similarity (Levenshtein-based)
       if (!string.IsNullOrEmpty(game.NormalizedTitle) && normalized.Length > 2)
         {
             var similarity = CalculateSimilarity(normalized, game.NormalizedTitle);
       var simScore = (int)(similarity * 300);
           score += simScore;
           if (similarity > 0.5)
          {
       reasons.Add($"{(int)(similarity * 100)}% similar");
      }
           }

 // Strategy 6: Acronym matching (e.g., "MI2" -> "Monkey Island 2")
        if (normalized.Length <= 5 && char.IsLetter(normalized[0]))
      {
 var acronym = CreateAcronym(game.Title);
          if (string.Equals(normalized, acronym, StringComparison.OrdinalIgnoreCase))
     {
         score += 700;
                  reasons.Add("Acronym match");
      }
          }

   // Strategy 7: Number matching (important for sequels)
        var inputNumbers = ExtractNumbers(folderName);
      var gameNumbers = ExtractNumbers(game.Title);
        if (inputNumbers.Count > 0 && gameNumbers.Count > 0 && inputNumbers.SequenceEqual(gameNumbers))
         {
        score += 200;
        reasons.Add("Matching numbers");
        }

      // Penalties
     // Penalize significantly different lengths
            if (!string.IsNullOrEmpty(game.NormalizedTitle))
          {
  int lengthDiff = Math.Abs(normalized.Length - game.NormalizedTitle.Length);
   if (lengthDiff > 20)
        {
             score -= lengthDiff * 2;
     }
}

           // PENALTY: Generic/ambiguous short IDs should require higher confidence
        if (shortId.Length <= 2)
      {
  // If match is only based on short ID, penalize heavily
         bool hasSubstantialMatch = reasons.Any(r => 
      r.Contains("matching keywords") || 
         r.Contains("Exact title") ||
            r.Contains("similar") && score > 500);
       
        if (!hasSubstantialMatch)
                {
     score -= 500; // Heavy penalty for generic ID-only matches
       }
    }

                if (score > 0)
        {
   candidates.Add((game, score, string.Join(", ", reasons)));
           }
   }

         // Get best match with higher threshold
            var bestMatch = candidates.OrderByDescending(c => c.score).FirstOrDefault();
 
        // Increased threshold to reduce false positives
          if (bestMatch.game == null || bestMatch.score < 200)
                return null;

    return new IdentifiedGame
            {
FolderName = folderName,
             FolderPath = folderPath,
                MatchedGame = bestMatch.game,
    ConfidenceScore = bestMatch.score,
       MatchReason = bestMatch.reason
          };
        }

        private static string NormalizeText(string text)
        {
         if (string.IsNullOrWhiteSpace(text))
        return string.Empty;

         // Convert to lowercase
    text = text.ToLowerInvariant();

// Remove special characters and extra spaces
            text = Regex.Replace(text, @"[^\w\s\d]", " ");
   text = Regex.Replace(text, @"\s+", " ");

      return text.Trim();
        }

        private static List<string> TokenizeAndFilter(string text)
     {
 if (string.IsNullOrWhiteSpace(text))
    return new List<string>();

    var tokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
      .Where(t => !StopWords.Contains(t) && t.Length > 1)
          .ToList();

            // If no tokens left after filtering, use original
      return tokens.Count > 0 ? tokens : text.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        private static double CalculateSimilarity(string s1, string s2)
        {
          if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2))
     return 0;

          int distance = LevenshteinDistance(s1, s2);
            int maxLen = Math.Max(s1.Length, s2.Length);
        
            return 1.0 - ((double)distance / maxLen);
        }

        private static int LevenshteinDistance(string s, string t)
        {
            if (string.IsNullOrEmpty(s)) return t?.Length ?? 0;
            if (string.IsNullOrEmpty(t)) return s.Length;

 int n = s.Length;
  int m = t.Length;
            var d = new int[n + 1, m + 1];

   for (int i = 0; i <= n; i++)
         d[i, 0] = i;
            for (int j = 0; j <= m; j++)
      d[0, j] = j;

     for (int i = 1; i <= n; i++)
   {
        for (int j = 1; j <= m; j++)
       {
        int cost = s[i - 1] == t[j - 1] ? 0 : 1;
       d[i, j] = Math.Min(
    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
        d[i - 1, j - 1] + cost);
    }
       }

      return d[n, m];
      }

        private static string CreateAcronym(string text)
        {
        if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

       var words = text.Split(new[] { ' ', '-', ':' }, StringSplitOptions.RemoveEmptyEntries);
   return string.Concat(words.Where(w => !StopWords.Contains(w) && w.Length > 0)
    .Select(w => w[0]));
        }

        private static List<int> ExtractNumbers(string text)
        {
     if (string.IsNullOrWhiteSpace(text))
    return new List<int>();

        var matches = Regex.Matches(text, @"\d+");
       return matches.Select(m => int.TryParse(m.Value, out var num) ? num : -1)
                     .Where(n => n >= 0)
          .ToList();
        }

        public List<IdentifiedGame> IdentifyGames(string rootFolder)
        {
        if (!Directory.Exists(rootFolder))
   return new List<IdentifiedGame>();

            var results = new List<IdentifiedGame>();

            try
    {
                var subdirectories = Directory.GetDirectories(rootFolder);
        
           foreach (var dir in subdirectories)
      {
    var folderName = Path.GetFileName(dir);
   var identified = IdentifyGame(folderName, dir);
        
  if (identified != null)
          {
   results.Add(identified);
             }
        }
            }
            catch (Exception ex)
    {
    // Log or handle exception
                System.Diagnostics.Debug.WriteLine($"Error scanning folders: {ex.Message}");
     }

    return results.OrderByDescending(r => r.ConfidenceScore).ToList();
      }
    }
}

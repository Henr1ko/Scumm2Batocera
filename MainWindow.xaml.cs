using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using ScummIdGui.Models;
using ScummIdGui.Services;
using WinForms = System.Windows.Forms;

namespace ScummIdGui
{
    public partial class MainWindow : Window
    {
    private GameIdentificationEngine? _engine;
  private List<IdentifiedGame> _identifiedGames = new();

        public MainWindow()
        {
            InitializeComponent();
        InitializeEngine();
        }

        private void InitializeEngine()
        {
     try
   {
        _engine = GameIdentificationEngine.LoadDefault();
 StatusTextBlock.Text = $"? Loaded {_engine.GameCount} games from scummvm-games.json";
 }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"? Error loading database: {ex.Message}";
                MessageBox.Show(
           $"Could not load scummvm-games.json.\n\n{ex.Message}\n\nPlease ensure scummvm-games.json is in the program folder.",
         "Database Error",
      MessageBoxButton.OK,
       MessageBoxImage.Warning);
      }
        }

        private void BrowseSourceButton_Click(object sender, RoutedEventArgs e)
        {
     using var dialog = new WinForms.FolderBrowserDialog
            {
            Description = "Select folder containing ScummVM game subfolders",
        UseDescriptionForTitle = true
   };

 if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
     SourceFolderTextBox.Text = dialog.SelectedPath;
            }
  }

        private void BrowseOutputButton_Click(object sender, RoutedEventArgs e)
    {
  using var dialog = new WinForms.FolderBrowserDialog
            {
  Description = "Select output folder for Batocera-ready games",
   UseDescriptionForTitle = true
          };

  if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
    OutputFolderTextBox.Text = dialog.SelectedPath;
            }
}

        private async void ScanButton_Click(object sender, RoutedEventArgs e)
        {
            if (_engine == null)
          {
     MessageBox.Show("Game database not loaded. Please check scummvm-games.json.",
               "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        return;
      }

            if (string.IsNullOrWhiteSpace(SourceFolderTextBox.Text) ||
     !Directory.Exists(SourceFolderTextBox.Text))
          {
  MessageBox.Show("Please select a valid source folder.",
       "Invalid Folder", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
          }

            await ScanFoldersAsync();
        }

        private async Task ScanFoldersAsync()
  {
         StatusTextBlock.Text = "Scanning folders...";
            ProgressBar.Visibility = Visibility.Visible;
            ProgressBar.IsIndeterminate = true;
 ScanButton.IsEnabled = false;
       ExportButton.IsEnabled = false;
     ResultsDataGrid.ItemsSource = null;

            try
    {
  // Get folder list first (fast operation)
       var folders = Directory.GetDirectories(SourceFolderTextBox.Text);
    int totalFolders = folders.Length;

     if (totalFolders == 0)
     {
          MessageBox.Show("No subfolders found in the selected directory.",
         "No Folders", MessageBoxButton.OK, MessageBoxImage.Information);
   return;
    }

          // Switch to determinate progress
      ProgressBar.IsIndeterminate = false;
ProgressBar.Maximum = totalFolders;
       ProgressBar.Value = 0;

           // Process folders asynchronously with progress updates
     var identifiedGames = new List<IdentifiedGame>();
   var unidentifiedFolders = new List<string>();

          await Task.Run(() =>
  {
                    for (int i = 0; i < folders.Length; i++)
 {
                var folder = folders[i];
                 var folderName = Path.GetFileName(folder);

  // Update UI on UI thread
             Dispatcher.Invoke(() =>
  {
          StatusTextBlock.Text = $"Scanning {i + 1}/{totalFolders}: {folderName}";
     ProgressBar.Value = i + 1;
              });

       // Identify game (CPU-bound operation)
             var identified = _engine!.IdentifyGame(folderName, folder);
     if (identified != null)
         {
    identifiedGames.Add(identified);
        }
              else
        {
       unidentifiedFolders.Add(folderName);
   }
     }
  });

  // Store results and update UI
    _identifiedGames = identifiedGames.OrderByDescending(g => g.ConfidenceScore).ToList();
                ResultsDataGrid.ItemsSource = _identifiedGames;

      int identified = _identifiedGames.Count;

        // Save identification log
      await SaveIdentificationLogAsync(_identifiedGames, unidentifiedFolders, totalFolders);

       StatusTextBlock.Text = $"? Identified {identified} out of {totalFolders} folders. " +
            $"Log saved to identification-log.txt";

        ExportButton.IsEnabled = _identifiedGames.Count > 0;

              if (identified == 0)
       {
         MessageBox.Show("No games were identified in the selected folder.\n\n" +
           "Make sure the folder contains subfolders with ScummVM game names.\n\n" +
      "Check identification-log.txt for details.",
  "No Games Found", MessageBoxButton.OK, MessageBoxImage.Information);
                }
    else if (identified < totalFolders)
      {
     var unidentified = totalFolders - identified;
     MessageBox.Show($"Successfully identified {identified} games!\n\n" +
              $"{unidentified} folders could not be identified automatically.\n\n" +
    $"Check identification-log.txt for details.",
  "Scan Complete", MessageBoxButton.OK, MessageBoxImage.Information);
    }
     else
             {
     MessageBox.Show($"Success! All {identified} games identified!\n\n" +
 "Click 'Export Batocera Ready' to prepare them for Batocera.\n\n" +
    "Check identification-log.txt for match details.",
           "Perfect Match!", MessageBoxButton.OK, MessageBoxImage.Information);
 }
            }
            catch (Exception ex)
 {
    StatusTextBlock.Text = $"? Error during scan: {ex.Message}";
        MessageBox.Show($"Error scanning folder:\n\n{ex.Message}",
     "Scan Error", MessageBoxButton.OK, MessageBoxImage.Error);
  }
            finally
   {
                ProgressBar.Visibility = Visibility.Collapsed;
                ProgressBar.Value = 0;
ScanButton.IsEnabled = true;
       }
        }

        private async Task SaveIdentificationLogAsync(List<IdentifiedGame> identified, List<string> unidentified, int totalFolders)
        {
  try
        {
var logPath = Path.Combine(AppContext.BaseDirectory, "identification-log.txt");
   var sb = new StringBuilder();

  sb.AppendLine("=".PadRight(80, '='));
        sb.AppendLine("ScummVM Game Identification Log");
      sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"Source Folder: {SourceFolderTextBox.Text}");
          sb.AppendLine("=".PadRight(80, '='));
        sb.AppendLine();

         sb.AppendLine($"SUMMARY:");
       sb.AppendLine($"  Total Folders:     {totalFolders}");
      sb.AppendLine($"  Identified:     {identified.Count}");
           sb.AppendLine($"  Unidentified:         {unidentified.Count}");
       sb.AppendLine($"  Success Rate:         {(totalFolders > 0 ? (identified.Count * 100.0 / totalFolders).ToString("F1") : "0")}%");
  sb.AppendLine();

           if (identified.Count > 0)
    {
           sb.AppendLine("=".PadRight(80, '='));
    sb.AppendLine($"IDENTIFIED GAMES ({identified.Count}):");
        sb.AppendLine("=".PadRight(80, '='));
        sb.AppendLine();

            foreach (var game in identified.OrderByDescending(g => g.ConfidenceScore))
        {
          sb.AppendLine($"Folder:      {game.FolderName}");
      sb.AppendLine($"Matched:     {game.DisplayName}");
 sb.AppendLine($"Short ID:    {game.ShortId}");
         sb.AppendLine($"Full ID:     {game.FullId}");
       sb.AppendLine($"Confidence:  {game.ConfidenceScore} points");
      sb.AppendLine($"Reason:      {game.MatchReason}");
  sb.AppendLine($"Path:        {game.FolderPath}");
      sb.AppendLine("-".PadRight(80, '-'));
    }
     sb.AppendLine();
       }

    if (unidentified.Count > 0)
         {
         sb.AppendLine("=".PadRight(80, '='));
        sb.AppendLine($"UNIDENTIFIED FOLDERS ({unidentified.Count}):");
    sb.AppendLine("=".PadRight(80, '='));
     sb.AppendLine();
          sb.AppendLine("These folders could not be matched to any game in the database.");
           sb.AppendLine("Possible reasons: typos, non-ScummVM games, or missing from database.");
         sb.AppendLine();

   foreach (var folder in unidentified.OrderBy(f => f))
          {
            sb.AppendLine($"  - {folder}");
           }
    sb.AppendLine();
   }

        sb.AppendLine("=".PadRight(80, '='));
              sb.AppendLine("END OF LOG");
   sb.AppendLine("=".PadRight(80, '='));

 await File.WriteAllTextAsync(logPath, sb.ToString(), Encoding.UTF8);
   }
     catch (Exception ex)
            {
    System.Diagnostics.Debug.WriteLine($"Failed to save log: {ex.Message}");
   }
        }

        private async void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            if (_identifiedGames.Count == 0)
    {
         MessageBox.Show("No identified games to export.",
    "Nothing to Export", MessageBoxButton.OK, MessageBoxImage.Warning);
      return;
      }

  if (string.IsNullOrWhiteSpace(OutputFolderTextBox.Text))
    {
    MessageBox.Show("Please select an output folder.",
   "Output Folder Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
   }

        var result = MessageBox.Show(
                $"This will:\n\n" +
      $"1. Copy {_identifiedGames.Count} identified games to the output folder\n" +
       $"2. Rename folders to end with '.scummvm'\n" +
          $"3. Create [gamename].scummvm files containing the game ID\n\n" +
        "This makes them ready for Batocera!\n\n" +
                "Continue?",
 "Export Batocera Ready Games",
           MessageBoxButton.YesNo,
       MessageBoxImage.Question);

          if (result != MessageBoxResult.Yes)
     return;

       await ExportBatoceraGamesAsync();
        }

        private async Task ExportBatoceraGamesAsync()
        {
  StatusTextBlock.Text = "Exporting games...";
    ProgressBar.Visibility = Visibility.Visible;
   ProgressBar.IsIndeterminate = false;
   ProgressBar.Maximum = _identifiedGames.Count;
    ProgressBar.Value = 0;
    ExportButton.IsEnabled = false;
      ScanButton.IsEnabled = false;

 int successCount = 0;
 int errorCount = 0;
   var errors = new List<(string gameName, string error)>();
  var exportLog = new StringBuilder();
  
            // Capture the output folder path before entering Task.Run
     var outputFolder = OutputFolderTextBox.Text;

  exportLog.AppendLine("=".PadRight(80, '='));
      exportLog.AppendLine("Batocera Export Log");
exportLog.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
  exportLog.AppendLine($"Output Folder: {outputFolder}");
   exportLog.AppendLine("=".PadRight(80, '='));
  exportLog.AppendLine();

  try
  {
   // Ensure output directory exists
  Directory.CreateDirectory(outputFolder);

    // Process each game
    for (int i = 0; i < _identifiedGames.Count; i++)
  {
       var game = _identifiedGames[i];
     var currentIndex = i + 1;
  var totalCount = _identifiedGames.Count;

        // Update UI on UI thread
        Dispatcher.Invoke(() =>
 {
            StatusTextBlock.Text = $"Exporting {currentIndex}/{totalCount}: {game.FolderName}";
    ProgressBar.Value = currentIndex;
 });

    try
       {
  // Validate source folder exists
       if (!Directory.Exists(game.FolderPath))
 {
     throw new DirectoryNotFoundException($"Source folder not found: {game.FolderPath}");
      }

 // Run the copy operation on background thread
       await Task.Run(() =>
 {
       // Create safe folder name
 var originalFolderName = Path.GetFileName(game.FolderPath);
    var safeFolderName = GetSafeFileName(originalFolderName);
   var newFolderName = $"{safeFolderName}.scummvm";
  var destFolder = Path.Combine(outputFolder, newFolderName);

 // Check if destination already exists
     if (Directory.Exists(destFolder))
    {
Directory.Delete(destFolder, true); // Delete existing
}

  // Copy folder
CopyDirectory(game.FolderPath, destFolder);

   // Create .scummvm file with short ID
 var scummvmFileName = $"{safeFolderName}.scummvm";
    var scummvmFilePath = Path.Combine(destFolder, scummvmFileName);
      File.WriteAllText(scummvmFilePath, game.ShortId, Encoding.UTF8);
   });

   successCount++;
exportLog.AppendLine($"? SUCCESS: {game.FolderName}");
exportLog.AppendLine($"    ? {GetSafeFileName(Path.GetFileName(game.FolderPath))}.scummvm");
       exportLog.AppendLine($"    ID: {game.ShortId}");
  exportLog.AppendLine();
     }
 catch (Exception ex)
      {
      errorCount++;
    var errorMsg = $"{ex.GetType().Name}: {ex.Message}";
errors.Add((game.FolderName, errorMsg));
    exportLog.AppendLine($"? FAILED: {game.FolderName}");
     exportLog.AppendLine($"  Error: {errorMsg}");
   exportLog.AppendLine();
     }
   }

 // Save export log
      exportLog.AppendLine("=".PadRight(80, '='));
      exportLog.AppendLine("EXPORT SUMMARY:");
  exportLog.AppendLine($"  Total:        {_identifiedGames.Count}");
  exportLog.AppendLine($"  Successful:   {successCount}");
          exportLog.AppendLine($"  Failed:       {errorCount}");
     exportLog.AppendLine("=".PadRight(80, '='));

var exportLogPath = Path.Combine(AppContext.BaseDirectory, "export-log.txt");
    await File.WriteAllTextAsync(exportLogPath, exportLog.ToString(), Encoding.UTF8);

    StatusTextBlock.Text = $"? Export complete! {successCount}/{_identifiedGames.Count} games exported. Log saved.";

      // Build result message
      var message = new StringBuilder();
         message.AppendLine("Export Summary:");
   message.AppendLine();
    message.AppendLine($"? Successfully exported: {successCount} games");

       if (errorCount > 0)
      {
message.AppendLine($"? Failed: {errorCount} games");
  message.AppendLine();
   message.AppendLine("Failed games:");
      foreach (var error in errors.Take(5))
    {
    message.AppendLine($"  - {error.gameName}");
     message.AppendLine($"    {error.error}");
      }
 if (errors.Count > 5)
 message.AppendLine($"  ... and {errors.Count - 5} more");
       }

     message.AppendLine();
  message.AppendLine($"Output folder: {outputFolder}");
    message.AppendLine($"Detailed log: export-log.txt");

     MessageBox.Show(message.ToString(), "Export Complete",
     MessageBoxButton.OK,
     errorCount > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);

            // Show "All done!" message after successful export
            if (errorCount == 0)
  {
           MessageBox.Show("All done!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
     }
  }
  catch (Exception ex)
   {
       StatusTextBlock.Text = $"? Export failed: {ex.Message}";
   MessageBox.Show($"Export failed:\n\n{ex.Message}\n\nCheck export-log.txt for details.",
 "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
     }
 finally
 {
     ProgressBar.Visibility = Visibility.Collapsed;
   ProgressBar.Value = 0;
  ExportButton.IsEnabled = true;
 ScanButton.IsEnabled = true;
  }
      }

        private static string GetSafeFileName(string fileName)
{
            // Remove invalid path characters
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", fileName.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
        }

      private static void CopyDirectory(string sourceDir, string destDir)
        {
            // Create destination directory
 Directory.CreateDirectory(destDir);

            // Copy all files
            foreach (var file in Directory.GetFiles(sourceDir))
   {
     try
     {
  var fileName = Path.GetFileName(file);
    var destFile = Path.Combine(destDir, fileName);
             File.Copy(file, destFile, true);
      }
  catch (Exception ex)
      {
      System.Diagnostics.Debug.WriteLine($"Failed to copy file {file}: {ex.Message}");
           // Continue with other files
      }
         }

  // Recursively copy subdirectories
 foreach (var subDir in Directory.GetDirectories(sourceDir))
         {
        try
       {
            var dirName = Path.GetFileName(subDir);
 var destSubDir = Path.Combine(destDir, dirName);
        CopyDirectory(subDir, destSubDir);
       }
        catch (Exception ex)
              {
    System.Diagnostics.Debug.WriteLine($"Failed to copy directory {subDir}: {ex.Message}");
       // Continue with other directories
        }
  }
        }
    }
}

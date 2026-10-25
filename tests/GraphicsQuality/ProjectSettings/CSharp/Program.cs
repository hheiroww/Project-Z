using ProjectSettingsProbe;

if (QualityProbe.CheckMetadata(args)) return;
using var game = new EntryGame(args);
game.Run();

sealed class EntryGame(string[] args) : QualityProbe(args);

using ProjectSettingsProbe;

if (QualityProbe.CheckMetadata(args)) return;
using var game = new QualityProbe(args);
game.Run();

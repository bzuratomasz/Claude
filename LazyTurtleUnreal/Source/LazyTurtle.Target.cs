using UnrealBuildTool;

public class LazyTurtleTarget : TargetRules
{
	public LazyTurtleTarget(TargetInfo Target) : base(Target)
	{
		Type = TargetType.Game;
		DefaultBuildSettings = BuildSettingsVersion.Latest;
		IncludeOrderVersion = EngineIncludeOrderVersion.Latest;
		ExtraModuleNames.Add("LazyTurtle");
	}
}

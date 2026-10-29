using UnrealBuildTool;

public class LazyTurtleEditorTarget : TargetRules
{
	public LazyTurtleEditorTarget(TargetInfo Target) : base(Target)
	{
		Type = TargetType.Editor;
		DefaultBuildSettings = BuildSettingsVersion.Latest;
		IncludeOrderVersion = EngineIncludeOrderVersion.Latest;
		ExtraModuleNames.Add("LazyTurtle");
	}
}

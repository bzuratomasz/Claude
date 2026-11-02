using UnrealBuildTool;

public class LazyTurtle : ModuleRules
{
	public LazyTurtle(ReadOnlyTargetRules Target) : base(Target)
	{
		PCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;

		PublicDependencyModuleNames.AddRange(new[]
		{
			"Core", "CoreUObject", "Engine", "InputCore", "EnhancedInput", "DeveloperSettings",
		});
		PrivateDependencyModuleNames.AddRange(new[]
		{
			"Slate", "SlateCore", "Json", "JsonUtilities", "AudioMixer", "UMG",
		});

		// Reading questions aloud uses the Windows speech API (SAPI 5), present on every Windows 10/11.
		if (Target.Platform == UnrealTargetPlatform.Win64)
		{
			PublicSystemLibraries.AddRange(new[] { "sapi.lib", "ole32.lib" });
		}
	}
}

using System;
using System.Collections;

namespace ThinkWind.Services;

internal static class LaunchEnvironment
{
    // A launcher started from an IDE must not pass the IDE's Electron/Node
    // bootstrap mode to unrelated desktop applications. Process scope only:
    // never modify the user's or machine's persistent environment.
    internal static void RemoveDevelopmentBootstrap()
    {
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is not string name) continue;
            if (name.Equals("ELECTRON_RUN_AS_NODE", StringComparison.OrdinalIgnoreCase)
                || name.Equals("ELECTRON_NO_ASAR", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("VSCODE_", StringComparison.OrdinalIgnoreCase))
                Environment.SetEnvironmentVariable(name, null, EnvironmentVariableTarget.Process);
        }
    }
}

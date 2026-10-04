using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("Mute my Mic")]
[assembly: AssemblyDescription("System-wide microphone mute with a global hotkey")]
[assembly: AssemblyProduct("Mute my Mic")]
[assembly: AssemblyCompany("RIGIOL")]
[assembly: AssemblyCopyright("Copyright (c) 2026 RIGIOL. MIT License.")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

// Load system DLLs used through P/Invoke (user32, kernel32, gdi32, ole32, winmm) only from
// System32. winmm.dll is not a "KnownDLL", so without this a DLL planted next to the exe would
// be loaded instead of the real one (DLL search-order hijacking).
[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]

# Project-local Unity 7 compatibility patch

Embedded from com.unity.ide.rider 3.0.38. Original license and sources retained.

Only three upstream C# files change:
- EditorPluginInterop.cs: enumerate Unity's current assemblies; get the Unity-managed assembly path.
- RiderInitializer.cs: use the same assembly path helper in its diagnostic.
- SyncTestRunEventsHandler.cs: load handlers/dependencies through Unity's assembly context; accept file URIs.

All replacements are guarded by UNITY_7000_0_OR_NEWER. Older Unity versions keep the original implementation. The local ruleset disables only UAL0010/UAL0013 for the Unity.Rider.Editor assembly: IDE caches, test callbacks and session state intentionally persist across Play Mode, matching the original registry package behavior. Project/runtime analyzer rules remain enabled. No PackageCache files are patched.

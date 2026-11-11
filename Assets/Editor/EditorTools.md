# Scene shortcuts and method buttons

The **Quick Scenes** panel appears automatically in the bottom-right of every Scene view.
It lists all scenes in Build Settings, updates when that list changes, and marks the active scene.
Click a scene to switch; modified scenes prompt to save first. Excluded scenes are still available
for editing. Missing scene entries and scene switching during Play mode are disabled.
Use **Hunting Game > Quick Scenes** to show or hide the panel.

Optional manually curated lists remain available under **Hunting Game > Quick Scene Assets**.
It already references GetStarted_Scene. Drag more scene assets into its Scenes list.
Open prompts to save modified scenes before replacing them; Additive retains existing scenes.
Scene switching is disabled while playing or compiling. Create more shortcut collections via
**Assets > Create > Hunting Game > Quick Scene Shortcuts**. These assets belong in an Editor folder.

Add the attribute to a method on any MonoBehaviour or ScriptableObject:

```csharp
using HuntingGame.Tools;

[MethodButton("Reset values", MethodButtonMode.EditMode)]
void ResetValues() { /* reset this object's serialized fields */ }

[MethodButton("Run action", MethodButtonMode.PlayMode)]
void RunAction() { /* invoke a gameplay action */ }
```

Methods must be non-static, non-generic, non-async, parameterless, and return void.
Public and private methods work. Omit the label to derive it from the method name.
The default mode is Both. Invalid signatures show a diagnostic instead of an executable button.
Multi-object selection invokes the action once per compatible target; exceptions are logged.

Existing custom editors take priority. To add buttons to one, call
`MethodButtonInspector.DrawButtons(targets)` after its normal Inspector UI.
Edit-mode actions record Undo for the inspected objects and persist prefab overrides.
Actions creating, deleting or changing other objects must explicitly use Unity's Undo API
for those objects. No scene or asset is automatically saved after invoking an action.

Validation: runtime and editor assemblies compile against the project's Unity 6.6 libraries.
Inspector clicks and scene transitions still require an in-editor check.
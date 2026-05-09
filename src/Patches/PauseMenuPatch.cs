using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.PauseMenu;
using MegaCrit.Sts2.Core.Runs;

namespace QuickReload;

[HarmonyPatch(typeof(NPauseMenu), nameof(NPauseMenu._Ready))]
static class QuickReloadPauseMenuPatch
{
    private const string QuickReloadNodeName = "QuickReload_QuickReloadButton";

    static void Postfix(NPauseMenu __instance)
    {
        if (RunManager.Instance.NetService.Type == NetGameType.Client)
            return;

        // Use Traverse to access private fields that we know work in beta
        var buttonContainer = Traverse.Create(__instance)
            .Field<Control>("_buttonContainer")
            .Value;
        if (buttonContainer == null) return;

        if (buttonContainer.GetNodeOrNull<Node>(QuickReloadNodeName) != null)
            return;

        var saveAndQuitButton = Traverse.Create(__instance)
            .Field<NPauseMenuButton>("_saveAndQuitButton")
            .Value;
        if (saveAndQuitButton == null) return;

        var restartButton = saveAndQuitButton.Duplicate(
            (int)(Node.DuplicateFlags.Groups | Node.DuplicateFlags.Scripts)
        ) as NPauseMenuButton;

        if (restartButton == null) return;

        restartButton.Name = QuickReloadNodeName;
        restartButton.GetNode<MegaLabel>("Label").SetTextAutoSize("ReLoad");
        MakeVisualsUnique(restartButton);

        buttonContainer.AddChild(restartButton);
        buttonContainer.MoveChild(restartButton, saveAndQuitButton.GetIndex());

        ConnectFocusNeighbors(buttonContainer, restartButton);

        restartButton.Connect(
            NClickableControl.SignalName.Released,
            Callable.From<NButton>(_ => OnQuickReloadPressed(__instance))
        );

        Log.Info("[QUICKRELOAD]: Quick Reload button added.");
    }

    private static void ConnectFocusNeighbors(Control buttonContainer, NPauseMenuButton restartButton)
    {
        var buttons = new List<NPauseMenuButton>();
        foreach (var button in buttonContainer.GetChildren())
        {
            if (button is NPauseMenuButton { Visible: true } pauseMenuButton)
                buttons.Add(pauseMenuButton);
        }

        var index = buttons.IndexOf(restartButton);
        if (index <= 0 || index >= buttons.Count - 1)
            return;

        var previousButton = buttons[index - 1];
        var nextButton = buttons[index + 1];

        previousButton.FocusNeighborBottom = restartButton.GetPath();
        restartButton.FocusNeighborTop = previousButton.GetPath();
        nextButton.FocusNeighborTop = restartButton.GetPath();
        restartButton.FocusNeighborBottom = nextButton.GetPath();
    }

    private static void MakeVisualsUnique(NPauseMenuButton button)
    {
        var image = button.GetNodeOrNull<TextureRect>("ButtonImage");
        if (image?.Material != null)
            image.Material = image.Material.Duplicate() as Material;

        var label = button.GetNodeOrNull<CanvasItem>("Label");
        if (label?.Material != null)
            label.Material = label.Material.Duplicate() as Material;
    }

    private static void OnQuickReloadPressed(NPauseMenu pauseMenu)
    {
        Log.Info("[QUICKRELOAD]: Quick Reload pressed.");
        TaskHelper.RunSafely(QuickReloadRunner.RestartAsync(pauseMenu));
    }
}

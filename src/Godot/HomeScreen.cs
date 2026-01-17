using System;
using Godot;
using SimGame.Core;

namespace SimGame.Godot;

public partial class HomeScreen : Control
{
    [Signal]
    public delegate void NewGameRequestedEventHandler();

    [Signal]
    public delegate void LoadGameRequestedEventHandler(string slotName);

    [Export]
    public NodePath SavesListPath { get; set; } = null!;

    [Export]
    public NodePath NewGameButtonPath { get; set; } = null!;

    private VBoxContainer? _savesList;
    private Button? _newGameButton;

    public override void _Ready()
    {
        _savesList = GetNodeOrNull<VBoxContainer>(SavesListPath);
        _newGameButton = GetNodeOrNull<Button>(NewGameButtonPath);

        if (_newGameButton != null)
        {
            _newGameButton.Pressed += OnNewGamePressed;
        }

        RefreshSavesList();
    }

    public void RefreshSavesList()
    {
        if (_savesList == null)
            return;

        // Clear existing entries
        foreach (var child in _savesList.GetChildren())
        {
            child.QueueFree();
        }

        // Load save metadata
        var saves = SaveFileManager.GetAllSaves();

        if (saves.Count == 0)
        {
            var noSavesLabel = new Label
            {
                Text = "No saved games",
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            noSavesLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
            _savesList.AddChild(noSavesLabel);
        }
        else
        {
            foreach (var save in saves)
            {
                var entry = CreateSaveEntry(save);
                _savesList.AddChild(entry);
            }
        }
    }

    private Control CreateSaveEntry(SaveMetadata save)
    {
        var entry = new HBoxContainer();
        entry.AddThemeConstantOverride("separation", 10);

        var infoBox = new VBoxContainer();
        infoBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        var nameLabel = new Label { Text = save.DisplayName };
        nameLabel.AddThemeFontSizeOverride("font_size", 14);

        var detailLabel = new Label
        {
            Text = $"Day {save.Day} - {save.PawnCount} pawn{(save.PawnCount != 1 ? "s" : "")}",
        };
        detailLabel.AddThemeFontSizeOverride("font_size", 10);
        detailLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));

        var dateLabel = new Label { Text = save.SavedAt.ToLocalTime().ToString("g") };
        dateLabel.AddThemeFontSizeOverride("font_size", 10);
        dateLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));

        infoBox.AddChild(nameLabel);
        infoBox.AddChild(detailLabel);
        infoBox.AddChild(dateLabel);

        var buttonBox = new VBoxContainer();
        buttonBox.AddThemeConstantOverride("separation", 4);

        var loadButton = new Button { Text = "Load", CustomMinimumSize = new Vector2(60, 0) };
        var slotName = save.SlotName;
        loadButton.Pressed += () => OnLoadPressed(slotName);

        var deleteButton = new Button { Text = "Delete", CustomMinimumSize = new Vector2(60, 0) };
        deleteButton.Pressed += () => OnDeletePressed(slotName);

        buttonBox.AddChild(loadButton);
        buttonBox.AddChild(deleteButton);

        entry.AddChild(infoBox);
        entry.AddChild(buttonBox);

        return entry;
    }

    private void OnNewGamePressed()
    {
        EmitSignal(SignalName.NewGameRequested);
    }

    private void OnLoadPressed(string slotName)
    {
        EmitSignal(SignalName.LoadGameRequested, slotName);
    }

    private void OnDeletePressed(string slotName)
    {
        SaveFileManager.DeleteSave(slotName);
        RefreshSavesList();
    }
}

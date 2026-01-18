using Godot;
using SimGame.Core;

namespace SimGame.Godot;

/// <summary>
/// Home screen with a streaming-app style grid of save file thumbnails.
/// First item is "New Game", followed by saves from newest to oldest.
/// </summary>
public partial class HomeScreen : Control
{
    [Signal]
    public delegate void NewGameRequestedEventHandler();

    [Signal]
    public delegate void LoadGameRequestedEventHandler(string slotName);

    [Export]
    public NodePath GridContainerPath { get; set; } = null!;

    // Thumbnail display size (scaled up from 150x100 for visibility)
    private const int ThumbnailDisplayWidth = 450;
    private const int ThumbnailDisplayHeight = 300;

    private HFlowContainer? _gridContainer;
    private ContentRegistry? _content;

    public override void _Ready()
    {
        _gridContainer = GetNodeOrNull<HFlowContainer>(GridContainerPath);

        // If Initialize was called before _Ready, refresh now
        if (_content != null)
        {
            RefreshSavesList();
        }
    }

    /// <summary>
    /// Initialize with content registry for palette access.
    /// </summary>
    public void Initialize(ContentRegistry content)
    {
        _content = content;

        // If _Ready already ran, refresh now; otherwise _Ready will do it
        if (_gridContainer != null)
        {
            RefreshSavesList();
        }
    }

    public void RefreshSavesList()
    {
        GD.Print(
            $"[HomeScreen] RefreshSavesList - GridContainer: {_gridContainer != null}, Content: {_content != null}"
        );

        if (_gridContainer == null)
            return;

        // Clear existing entries
        foreach (var child in _gridContainer.GetChildren())
        {
            child.QueueFree();
        }

        // Add "New Game" button as first item
        var newGameItem = CreateNewGameItem();
        _gridContainer.AddChild(newGameItem);
        GD.Print("[HomeScreen] Added New Game item");

        // Load saves and create thumbnail items
        var saves = SaveFileManager.GetAllSaves();
        GD.Print($"[HomeScreen] Found {saves.Count} saves");

        foreach (var save in saves)
        {
            var item = CreateSaveItem(save);
            _gridContainer.AddChild(item);
        }
    }

    private Control CreateNewGameItem()
    {
        var container = new PanelContainer();
        container.CustomMinimumSize = new Vector2(ThumbnailDisplayWidth, ThumbnailDisplayHeight);

        // Style the panel
        var styleBox = new StyleBoxFlat();
        styleBox.BgColor = new Color(0.15f, 0.35f, 0.15f); // Dark green
        styleBox.SetCornerRadiusAll(8);
        container.AddThemeStyleboxOverride("panel", styleBox);

        // Inner container for centering
        var centerContainer = new CenterContainer();
        centerContainer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        // Plus icon or "+" text
        var label = new Label();
        label.Text = "+";
        label.AddThemeFontSizeOverride("font_size", 64);
        label.AddThemeColorOverride("font_color", new Color(0.4f, 0.7f, 0.4f));

        centerContainer.AddChild(label);
        container.AddChild(centerContainer);

        // Make clickable
        container.GuiInput += (InputEvent @event) =>
        {
            if (@event is InputEventMouseButton mouseButton)
            {
                if (mouseButton.ButtonIndex == MouseButton.Left && mouseButton.Pressed)
                {
                    EmitSignal(SignalName.NewGameRequested);
                }
            }
        };

        // Hover effect
        container.MouseEntered += () =>
        {
            styleBox.BgColor = new Color(0.2f, 0.45f, 0.2f);
        };
        container.MouseExited += () =>
        {
            styleBox.BgColor = new Color(0.15f, 0.35f, 0.15f);
        };

        return container;
    }

    private Control CreateSaveItem(SaveMetadata save)
    {
        var container = new PanelContainer();
        container.CustomMinimumSize = new Vector2(ThumbnailDisplayWidth, ThumbnailDisplayHeight);

        // Style the panel
        var styleBox = new StyleBoxFlat();
        styleBox.BgColor = new Color(0.1f, 0.1f, 0.1f);
        styleBox.SetCornerRadiusAll(8);
        container.AddThemeStyleboxOverride("panel", styleBox);

        // Load full save data to generate thumbnail
        var saveData = SaveFileManager.LoadSave(save.SlotName);

        if (saveData != null && _content != null)
        {
            var texture = SaveThumbnailGenerator.Generate(saveData, _content);

            var textureRect = new TextureRect();
            textureRect.Texture = texture;
            textureRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            textureRect.StretchMode = TextureRect.StretchModeEnum.Scale;
            textureRect.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

            container.AddChild(textureRect);
        }
        else
        {
            // Fallback: show save name if thumbnail generation fails
            var centerContainer = new CenterContainer();
            centerContainer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

            var label = new Label();
            label.Text = save.DisplayName;
            label.AddThemeFontSizeOverride("font_size", 16);

            centerContainer.AddChild(label);
            container.AddChild(centerContainer);
        }

        // Make clickable
        var slotName = save.SlotName;
        container.GuiInput += (InputEvent @event) =>
        {
            if (@event is InputEventMouseButton mouseButton)
            {
                if (mouseButton.ButtonIndex == MouseButton.Left && mouseButton.Pressed)
                {
                    EmitSignal(SignalName.LoadGameRequested, slotName);
                }
            }
        };

        // Hover effect
        container.MouseEntered += () =>
        {
            styleBox.BgColor = new Color(0.2f, 0.2f, 0.2f);
        };
        container.MouseExited += () =>
        {
            styleBox.BgColor = new Color(0.1f, 0.1f, 0.1f);
        };

        return container;
    }
}

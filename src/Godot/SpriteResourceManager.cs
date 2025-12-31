using System.Collections.Generic;
using Godot;

namespace SimGame.Godot;

/// <summary>
/// Manages loading and caching of sprite textures from sprite keys.
/// Maps sprite keys (from Lua content) to actual Godot Texture2D resources.
/// </summary>
public static class SpriteResourceManager
{
    private static readonly Dictionary<string, Texture2D> _textureCache = new();

    // Sprite key to resource path mapping
    private static readonly Dictionary<string, string> _spritePathMap = new()
    {
        // Tile sprites
        ["grass"] = "res://sprites/tiles/grass.png",
        ["dirt"] = "res://sprites/tiles/dirt.png",
        ["wood_floor"] = "res://sprites/tiles/wood_floor.png",
        ["stone"] = "res://sprites/tiles/stone.png",
        ["flat"] = "res://sprites/tiles/flat.png",
        ["block"] = "res://sprites/tiles/block.png",
        ["path"] = "res://sprites/tiles/path.png",
        ["water"] = "res://sprites/tiles/water.png",
        ["wall"] = "res://sprites/tiles/wall.png",
        ["trees"] = "res://sprites/tiles/trees.png",
        ["rock"] = "res://sprites/tiles/rock.png",
        ["plant"] = "res://sprites/tiles/plant.png",

        // Object sprites
        ["fridge"] = "res://sprites/objects/fridge.png",
        ["bed"] = "res://sprites/objects/bed.png",
        ["tv"] = "res://sprites/objects/tv.png",
        ["shower"] = "res://sprites/objects/shower.png",
        ["castle"] = "res://sprites/objects/castle.png",
        ["lamp"] = "res://sprites/objects/lamp.png",

        // Character sprites
        ["character_walk"] = "res://sprites/characters/character_walk.png",
        ["character_idle"] = "res://sprites/characters/idle_strip3.png",
        ["character_axe"] = "res://sprites/characters/axe_strip5.png",
        ["character_pickaxe"] = "res://sprites/characters/pickaxe_strip5.png",
        ["character_look_down"] = "res://sprites/characters/look_down.png",
        ["character_look_up"] = "res://sprites/characters/look_up.png",

        // Expression bubble wrappers (32x32)
        ["thought_bubble"] = "res://sprites/ui/bubbles/thought_bubble.png",
        ["heart_bubble"] = "res://sprites/ui/bubbles/heart_bubble.png",
        ["complaint_bubble"] = "res://sprites/ui/bubbles/complaint_bubble.png",

        // Expression icons (16x16)
        ["heart"] = "res://sprites/ui/icons/heart.png",
        ["exclamation"] = "res://sprites/ui/icons/exclamation.png",
        ["question"] = "res://sprites/ui/icons/question.png",
        ["zzz"] = "res://sprites/ui/icons/zzz.png",
        ["hungry"] = "res://sprites/ui/icons/hungry.png",
    };

    /// <summary>
    /// Load a texture by sprite key. Returns null if sprite key is empty or not found.
    /// Caches loaded textures to avoid repeated loading.
    /// </summary>
    public static Texture2D? GetTexture(string spriteKey)
    {
        if (string.IsNullOrEmpty(spriteKey))
            return null;

        // Check cache first
        if (_textureCache.TryGetValue(spriteKey, out var cached))
            return cached;

        // Look up resource path
        if (!_spritePathMap.TryGetValue(spriteKey, out var resourcePath))
        {
            GD.PushWarning($"SpriteResourceManager: Unknown sprite key '{spriteKey}'");
            return null;
        }

        // Load texture
        var texture = GD.Load<Texture2D>(resourcePath);
        if (texture == null)
        {
            GD.PushError(
                $"SpriteResourceManager: Failed to load texture at '{resourcePath}' for sprite key '{spriteKey}'"
            );
            return null;
        }

        _textureCache[spriteKey] = texture;
        return texture;
    }

    /// <summary>
    /// Clear the texture cache. Useful for hot-reloading assets.
    /// </summary>
    public static void ClearCache()
    {
        _textureCache.Clear();
    }
}

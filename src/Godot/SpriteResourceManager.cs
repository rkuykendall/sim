using Godot;
using System.Collections.Generic;

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
		["concrete"] = "res://sprites/tiles/concrete.png",
		["wood_floor"] = "res://sprites/tiles/wood_floor.png",
		["stone"] = "res://sprites/tiles/stone.png",
		["water"] = "res://sprites/tiles/water.png",
		["flat"] = "res://sprites/tiles/flat.png",
		["block"] = "res://sprites/tiles/block.png",
		["brick"] = "res://sprites/tiles/brick.png",

		// Path autotile atlas
		["path"] = "res://sprites/tiles/path_grid.png",

		// Wall autotiling (future - requires wall_grid.png asset)
		// ["wall"] = "res://sprites/objects/wall_grid.png",
		["wall"] = "res://sprites/objects/wall.png",

		// Object sprites
		["fridge"] = "res://sprites/objects/fridge.png",
		["bed"] = "res://sprites/objects/bed.png",
		["tv"] = "res://sprites/objects/tv.png",
		["shower"] = "res://sprites/objects/shower.png",
		["plant"] = "res://sprites/objects/plant.png",
		["castle"] = "res://sprites/objects/castle.png",
		["lamp"] = "res://sprites/objects/lamp.png",

		// Character sprites
		["character_walk"] = "res://sprites/characters/character_walk.png",
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
			GD.PushError($"SpriteResourceManager: Failed to load texture at '{resourcePath}' for sprite key '{spriteKey}'");
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

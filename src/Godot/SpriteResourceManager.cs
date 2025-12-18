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
		// Tile sprites - all terrain types use the same flat texture for now
		["flat_texture"] = "res://sprites/tiles/flat_texture.png",
		["grass"] = "res://sprites/tiles/flat_texture.png",
		["dirt"] = "res://sprites/tiles/flat_texture.png",
		["concrete"] = "res://sprites/tiles/flat_texture.png",
		["wood_floor"] = "res://sprites/tiles/flat_texture.png",
		["stone"] = "res://sprites/tiles/flat_texture.png",
		["water"] = "res://sprites/tiles/flat_texture.png",

		// Path autotile atlas
		["path"] = "res://sprites/tiles/path_grid.png",

		// Object sprites
		["object_base"] = "res://sprites/objects/object_texture.png",
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using SimGame.Core;

namespace SimGame.Godot;

public static class SaveFileManager
{
    private const string SaveDirectory = "user://saves/";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    public static void EnsureSaveDirectory()
    {
        var globalPath = ProjectSettings.GlobalizePath(SaveDirectory);
        DirAccess.MakeDirRecursiveAbsolute(globalPath);
    }

    public static List<SaveMetadata> GetAllSaves()
    {
        var saves = new List<SaveMetadata>();

        EnsureSaveDirectory();

        using var dir = DirAccess.Open(SaveDirectory);
        if (dir == null)
            return saves;

        dir.ListDirBegin();
        var fileName = dir.GetNext();

        while (!string.IsNullOrEmpty(fileName))
        {
            if (!dir.CurrentIsDir() && fileName.EndsWith(".json"))
            {
                var slotName = fileName.Replace(".json", "");
                var metadata = LoadMetadata(slotName);
                if (metadata != null)
                {
                    saves.Add(metadata);
                }
            }
            fileName = dir.GetNext();
        }

        dir.ListDirEnd();
        return saves.OrderByDescending(s => s.SavedAt).ToList();
    }

    private static SaveMetadata? LoadMetadata(string slotName)
    {
        var data = LoadSave(slotName);
        if (data == null)
            return null;

        return SaveService.ToMetadata(data);
    }

    public static void WriteSave(string slotName, SaveData data)
    {
        EnsureSaveDirectory();

        var path = SaveDirectory + slotName + ".json";
        var json = JsonSerializer.Serialize(data, JsonOptions);

        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (file == null)
        {
            throw new InvalidOperationException($"Failed to open save file for writing: {path}");
        }

        file.StoreString(json);
        GD.Print($"Saved game to: {path}");
    }

    public static SaveData? LoadSave(string slotName)
    {
        var path = SaveDirectory + slotName + ".json";

        if (!FileAccess.FileExists(path))
            return null;

        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            throw new InvalidOperationException($"Failed to open save file for reading: {path}");
        }

        var json = file.GetAsText();

        try
        {
            return JsonSerializer.Deserialize<SaveData>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Failed to parse save file {path}: {ex.Message}",
                ex
            );
        }
    }

    public static void DeleteSave(string slotName)
    {
        var path = SaveDirectory + slotName + ".json";
        var globalPath = ProjectSettings.GlobalizePath(path);

        if (FileAccess.FileExists(path))
        {
            var err = DirAccess.RemoveAbsolute(globalPath);
            if (err != Error.Ok)
            {
                GD.PrintErr($"Failed to delete save file: {path}");
            }
            else
            {
                GD.Print($"Deleted save: {path}");
            }
        }
    }

    /// <summary>
    /// Generate the next available save name (Save 1, Save 2, etc.)
    /// </summary>
    public static string GenerateSaveName()
    {
        var existingSaves = GetAllSaves();
        var existingNumbers = new HashSet<int>();

        foreach (var save in existingSaves)
        {
            if (
                save.DisplayName.StartsWith("Save ")
                && int.TryParse(save.DisplayName.AsSpan(5), out var num)
            )
            {
                existingNumbers.Add(num);
            }
        }

        int nextNumber = 1;
        while (existingNumbers.Contains(nextNumber))
        {
            nextNumber++;
        }

        return $"Save {nextNumber}";
    }
}

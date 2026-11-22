using System.Text.Json;
namespace RMPCS.Core;
public sealed record PreferenceUndo(AppPreferences Before, AppPreferences After)
{
    public string ToJson() => JsonSerializer.Serialize(this);
    public static PreferenceUndo Parse(string value)
    {
        if (value.Length > 16384) throw new InvalidDataException("Undo record exceeds size limit.");
        var record = JsonSerializer.Deserialize<PreferenceUndo>(value, new JsonSerializerOptions { MaxDepth = 10 }) ?? throw new InvalidDataException("Missing undo record.");
        if (record.Before is null || record.After is null) throw new InvalidDataException("Incomplete undo record.");
        return new(record.Before.Normalize(), record.After.Normalize());
    }
    public AppPreferences Restore(AppPreferences current) => current.Normalize() == After.Normalize() ? Before.Normalize() : throw new InvalidOperationException("Preferences changed after this record. Undo refused to overwrite a different state.");
}

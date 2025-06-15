namespace CompressionUI.Models.Nodes;

/// <summary>
/// Standard node categories for organization
/// </summary>
public static class NodeCategories
{
    public const string Data = "Data";
    public const string Model = "Model";
    public const string Math = "Math";
    public const string Utility = "Utility";
    public const string Compression = "Compression";
    public const string Visualization = "Visualization";
    public const string IO = "Input/Output";
    public const string Custom = "Custom";

    public static readonly string[] All = {
        Data, Model, Math, Utility, Compression, Visualization, IO, Custom
    };

    public static bool IsValidCategory(string category) =>
        Array.IndexOf(All, category) >= 0;
}
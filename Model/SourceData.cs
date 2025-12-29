
using Dalamud.Bindings.ImGui;
using Newtonsoft.Json;
using System;

namespace MiniMappingway.Model;

[Serializable]
public class SourceData : IEquatable<SourceData>
{
    public bool Enabled { get; set; } = true;

    public uint Color { get; set; }

    [NonSerialized]
    private uint? _autoBorderColor;

    public float BorderDarkeningAmount { get; set; } = 0.7f;

    public int Priority { get; set; } = 1;

    public bool ShowBorder { get; set; } = true;

    public int CircleSize { get; set; } = 6;

    public bool BorderValid { get; set; } = true;

    public int BorderRadius { get; set; } = 2;

    public uint AutoBorderColour
    {
        get
        {
            if (_autoBorderColor != null && BorderValid)
            {
                return (uint)_autoBorderColor;
            }
            var temp = ImGui.ColorConvertU32ToFloat4(Color);

            temp.Z *= BorderDarkeningAmount;
            temp.X *= BorderDarkeningAmount;
            temp.Y *= BorderDarkeningAmount;
            _autoBorderColor = ImGui.ColorConvertFloat4ToU32(temp);
            BorderValid = true;
            return (uint)_autoBorderColor;
        }
    }

    public SourceData(uint color)
    {
        Color = color;
    }

    public bool Equals(SourceData? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other is null) return false;
        return Enabled == other.Enabled && Color == other.Color && BorderDarkeningAmount.Equals(other.BorderDarkeningAmount) && Priority == other.Priority && ShowBorder == other.ShowBorder && CircleSize == other.CircleSize && BorderRadius == other.BorderRadius;
    }

    public override bool Equals(object? obj) => obj is SourceData other && this.Equals(other);

    public static bool operator ==(SourceData? left, SourceData? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(SourceData? left, SourceData? right)
    {
        return !Equals(left, right);
    }

    public SourceData(SourceData data)
    {
        Color = data.Color;

        BorderDarkeningAmount = data.BorderDarkeningAmount;

        Priority = data.Priority;

        ShowBorder = data.ShowBorder;

        CircleSize = data.CircleSize;

        BorderValid = data.BorderValid;

        BorderRadius = data.BorderRadius;

        Enabled = data.Enabled;
    }

    [JsonConstructor]
    public SourceData(uint color, float borderDarkeningAmount, int priority, bool showBorder, int circleSize, bool borderValid, int borderRadius, bool enabled)
    {
        Color = color;
        BorderDarkeningAmount = borderDarkeningAmount;
        Priority = priority;
        ShowBorder = showBorder;
        CircleSize = circleSize;
        BorderValid = borderValid;
        BorderRadius = borderRadius;
        Enabled = enabled;
    }

    public override int GetHashCode()
    {
        throw new NotImplementedException();
    }
}

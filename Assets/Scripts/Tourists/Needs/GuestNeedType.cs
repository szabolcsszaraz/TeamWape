
public enum GuestNeedType
{
    Rooms,
    Food,
    Drinks,
    Relax,
    OceanView
}

public static class GuestNeedTypeExtensions
{
    public static string ToDisplayName(this GuestNeedType type)
    {
        switch (type)
        {
            case GuestNeedType.OceanView:
                return "Ocean View";

            default:
                return type.ToString();
        }
    }
}

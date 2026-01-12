namespace RooseLabs.Player.Customization
{
    /// <summary>
    /// Defines how a customization item should be applied to the player.
    /// </summary>
    public enum ApplicationMode
    {
        SwapMeshOnly,
        SwapMaterialOnly,
        SwapMeshAndMaterial,
    }

    /// <summary>
    /// Categorizes customization items for organization and slot management.
    /// </summary>
    public enum CustomizationCategory
    {
        Hair,
        Eyes,
        Mouth,
        Ears,
        Hats,
        Glasses,
        Outfit,
        Wands
    }

    /// <summary>
    /// Identifies specific renderers on the player model for customization targeting.
    /// </summary>
    public enum RendererID
    {
        Hair,
        Eyes,
        Mouth,
        Ears,
        HeadAccessories,
        EyeAccessories,
        OutfitVisible,
        OutfitHidden,
        Wand
    }
}

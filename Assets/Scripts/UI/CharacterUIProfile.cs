using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Battle UI/Character UI Profile")]
public class CharacterUIProfile : ScriptableObject
{
    [HideInInspector] public int setupVersion;
    [Header("Artwork bounding size and ground offset")]
    public Vector2 visualSize = new Vector2(384, 384);
    public Vector2 visualOffset;
    [Header("HP and status HUD")]
    public CharacterInfoUI infoPrefab;
    public Vector2 infoSize = new Vector2(320, 84);
    public Vector2 infoOffset = new Vector2(0, -8);
    [Header("Enemy intent above visible artwork")]
    public EnemyActionUI actionPrefab;
    public Vector2 actionSize = new Vector2(240, EnemyActionUI.CompactHeight);
    [Min(0)] public float actionGap = 12;

    public void Validate()
    {
        if (infoPrefab != null) infoPrefab.ValidateBindings();
        if (actionPrefab != null) actionPrefab.ValidateBindings();
        CheckSize(visualSize, nameof(visualSize)); CheckSize(infoSize, nameof(infoSize)); CheckSize(actionSize, nameof(actionSize));
        if (!Finite(visualOffset.x) || !Finite(visualOffset.y) || !Finite(infoOffset.x) || !Finite(infoOffset.y) || !Finite(actionGap) || actionGap < 0)
            throw new ArgumentOutOfRangeException("Profile offsets and gap must be finite, and gap cannot be negative.");
    }

    public int LayoutHash()
    {
        unchecked
        {
            int hash = visualSize.GetHashCode(); hash = hash * 31 + visualOffset.GetHashCode();
            hash = hash * 31 + infoSize.GetHashCode(); hash = hash * 31 + infoOffset.GetHashCode();
            hash = hash * 31 + actionSize.GetHashCode(); hash = hash * 31 + actionGap.GetHashCode();
            hash = hash * 31 + (infoPrefab != null ? infoPrefab.GetInstanceID() : 0);
            return hash * 31 + (actionPrefab != null ? actionPrefab.GetInstanceID() : 0);
        }
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static void CheckSize(Vector2 value, string name)
    {
        if (!Finite(value.x) || !Finite(value.y) || value.x <= 0 || value.y <= 0) throw new ArgumentOutOfRangeException(name);
    }
}

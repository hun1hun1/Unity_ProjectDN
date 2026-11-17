using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public sealed class GameCursor : MonoBehaviour
{
    private static readonly Vector2 Hotspot = new Vector2(2f, 2f);
    private Texture2D defaultTexture;
    private Texture2D hoverTexture;
    private Texture2D appliedTexture;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        Texture2D normal = Resources.Load<Texture2D>("Cursors/Default");
        Texture2D hover = Resources.Load<Texture2D>("Cursors/Hover");
        if (normal == null || hover == null) return;

        var cursor = new GameObject("GameCursor").AddComponent<GameCursor>();
        DontDestroyOnLoad(cursor.gameObject);
        cursor.defaultTexture = normal;
        cursor.hoverTexture = hover;
        cursor.Apply(normal);
    }

    private void LateUpdate()
    {
        if (!Application.isFocused) return;

        // Reuse the UI module's topmost result, including its modal and raycast filtering.
        GameObject hit = null;
        Mouse mouse = Mouse.current;
        if (mouse != null && Cursor.lockState != CursorLockMode.Locked &&
            RunSession.Instance?.BlocksSceneInput != true &&
            EventSystem.current?.currentInputModule is InputSystemUIInputModule module)
            hit = module.GetLastRaycastResult(mouse.deviceId).gameObject;

        Apply(IsHoverTarget(hit) ? hoverTexture : defaultTexture);
    }

    private static bool IsHoverTarget(GameObject hit)
    {
        if (hit == null || !hit.activeInHierarchy) return false;

        Selectable control = hit.GetComponentInParent<Selectable>();
        if (control != null) return control.isActiveAndEnabled && control.IsInteractable();

        CardUI card = hit.GetComponentInParent<CardUI>();
        if (card != null) return card.CanHover;

        DeckCardHoverUI deckCard = hit.GetComponentInParent<DeckCardHoverUI>();
        return deckCard != null && deckCard.isActiveAndEnabled &&
            !CardInspectionUI.BlocksGameplayInput && !SettingsUI.BlocksBattleInput;
    }

    private void Apply(Texture2D texture)
    {
        if (appliedTexture == texture) return;
        Cursor.SetCursor(texture, Hotspot, CursorMode.Auto);
        appliedTexture = texture;
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused) RestoreSystemCursor();
    }

    private void OnDisable() => RestoreSystemCursor();

    private void RestoreSystemCursor()
    {
        if (appliedTexture == null) return;
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        appliedTexture = null;
    }
}

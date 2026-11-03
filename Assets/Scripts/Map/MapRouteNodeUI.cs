using UnityEngine;

[DisallowMultipleComponent]
public class MapRouteNodeUI : MonoBehaviour
{
    [SerializeField] private UnityEngine.UI.Image marker;
    [SerializeField] private UnityEngine.UI.Image connector;
    [SerializeField] private Sprite completedSprite;
    [SerializeField] private Sprite currentSprite;
    [SerializeField] private Sprite pendingSprite;

    public void SetState(bool completed, bool current, bool connectsToPrevious)
    {
        marker.sprite = completed ? completedSprite : current ? currentSprite : pendingSprite;
        connector.gameObject.SetActive(connectsToPrevious);
    }
}

using TMPro;
using UnityEngine;

public class AmmoUI : MonoBehaviour
{
    [SerializeField] private AmmoSystem ammoSystem;
    [SerializeField] private TMP_Text ammoText;
    [SerializeField] private UnityEngine.UI.Image ammoFill;
    private AmmoSystem subscribedAmmoSystem;

    private void OnEnable()
    {
        if (ammoSystem == null) ammoSystem = GetComponentInParent<AmmoSystem>();
        Bind(ammoSystem);
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    public void Bind(AmmoSystem source)
    {
        Unsubscribe();
        ammoSystem = source;
        if (ammoSystem != null && isActiveAndEnabled)
        {
            subscribedAmmoSystem = ammoSystem;
            subscribedAmmoSystem.AmmoChanged += SetAmmo;
        }
        SetAmmo(ammoSystem != null ? ammoSystem.CurrentAmmo : 0,
            ammoSystem != null ? ammoSystem.MaxAmmo : 0);
    }

    private void Unsubscribe()
    {
        if (subscribedAmmoSystem != null) subscribedAmmoSystem.AmmoChanged -= SetAmmo;
        subscribedAmmoSystem = null;
    }

    private void SetAmmo(int current, int maximum)
    {
        maximum = Mathf.Max(0, maximum);
        current = Mathf.Clamp(current, 0, maximum);
        if (ammoText != null) ammoText.text = $"총알 {current} / {maximum}";
        if (ammoFill != null) ammoFill.fillAmount = Mathf.Clamp01(current / 20f);
    }
}

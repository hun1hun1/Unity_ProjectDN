using System.Collections.Generic;
using TMPro;
using UnityEngine;

internal sealed class HealthChangeFeedback
{
    private const float Duration = .60f;
    private const float EntryDuration = .10f;
    private const float FadeDuration = .20f;
    private const float RiseDistance = 28f;
    private const float ColumnSpacing = 72f;
    private static readonly Color DamageColor = new Color(1f, .47f, .43f);
    private static readonly Color HealingColor = new Color(.68f, 1f, .60f);

    private sealed class Number
    {
        public readonly TextMeshProUGUI Label;
        public int Slot;
        public int Frame;
        public float StartedAt;
        public long Amount;

        public Number(TextMeshProUGUI label) => Label = label;

        public void RefreshText() => Label.text = Amount > 0 ? $"+{Amount}" : $"−{-Amount}";
    }

    private readonly RectTransform root;
    private readonly TextMeshProUGUI prefab;
    private readonly List<Number> active = new List<Number>();
    private readonly Stack<Number> available = new Stack<Number>();

    internal HealthChangeFeedback(RectTransform root)
    {
        GameObject template = Resources.Load<GameObject>("HealthChangeNumber");
        if (template != null) prefab = template.GetComponent<TextMeshProUGUI>();
        if (prefab == null) return;

        var layer = new GameObject("HealthChangeNumbers", typeof(RectTransform), typeof(Canvas));
        layer.layer = root.gameObject.layer;
        this.root = (RectTransform)layer.transform;
        this.root.SetParent(root, false);
        this.root.anchorMin = Vector2.zero;
        this.root.anchorMax = Vector2.one;
        this.root.offsetMin = this.root.offsetMax = Vector2.zero;

        Canvas canvas = layer.GetComponent<Canvas>();
        canvas.overrideSorting = true;
        // The reward canvas opens in the same frame as a lethal hit at order 2000.
        canvas.sortingOrder = 2001;
    }

    internal void Play(int delta, Vector2 origin)
    {
        if (prefab == null) return;

        Update(origin);
        int frame = Time.frameCount;
        foreach (Number number in active)
        {
            if (number.Frame != frame || (number.Amount > 0) != (delta > 0)) continue;
            number.Amount += delta;
            number.RefreshText();
            return;
        }

        // Use the first free slot so surviving numbers keep their positions.
        int slot = 0;
        while (slot < active.Count && active[slot].Slot == slot) slot++;
        Number entry = available.Count > 0 ? available.Pop() : CreateNumber();
        entry.Slot = slot;
        entry.Frame = frame;
        entry.StartedAt = Time.unscaledTime;
        entry.Amount = delta;
        entry.RefreshText();
        active.Insert(slot, entry);
        ApplyMotion(entry, origin, 0f);
        entry.Label.gameObject.SetActive(true);
    }

    private Number CreateNumber()
    {
        TextMeshProUGUI label = Object.Instantiate(prefab, root, false);
        label.name = prefab.name;
        return new Number(label);
    }

    internal void Update(Vector2 origin)
    {
        float now = Time.unscaledTime;
        for (int index = active.Count - 1; index >= 0; index--)
        {
            Number number = active[index];
            float elapsed = now - number.StartedAt;
            if (elapsed >= Duration) Release(index);
            else ApplyMotion(number, origin, elapsed);
        }
    }

    private static void ApplyMotion(Number number, Vector2 origin, float elapsed)
    {
        bool healing = number.Amount > 0;
        float entry = Mathf.Clamp01(elapsed / EntryDuration);
        float rise = Mathf.InverseLerp(EntryDuration, Duration, elapsed) * RiseDistance;
        float fade = 1f - Mathf.InverseLerp(Duration - FadeDuration, Duration, elapsed);
        float scale = healing ? 1f : 1f + .22f * Mathf.Sin(Mathf.PI * entry);
        float column = ((number.Slot + 1) / 2) * (number.Slot % 2 == 0 ? -ColumnSpacing : ColumnSpacing);
        RectTransform rect = number.Label.rectTransform;
        rect.anchoredPosition = origin + new Vector2(column, rise);
        rect.localScale = Vector3.one * scale;

        Color color = healing ? HealingColor : DamageColor;
        color.a = fade * (healing ? Mathf.Lerp(.25f, 1f, entry) : 1f);
        number.Label.color = color;
    }

    internal void Hide()
    {
        for (int index = active.Count - 1; index >= 0; index--) Release(index);
    }

    private void Release(int index)
    {
        Number number = active[index];
        active.RemoveAt(index);
        number.Label.gameObject.SetActive(false);
        available.Push(number);
    }
}

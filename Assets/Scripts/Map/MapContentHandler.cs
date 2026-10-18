using System;
using UnityEngine;

// Add a concrete handler to MapScene when a nonbattle content system is available.
public abstract class MapContentHandler : MonoBehaviour
{
    public abstract MapContentType ContentType { get; }
    public abstract void Begin(MapChoice choice, RunProgress progress, Action completed);
}

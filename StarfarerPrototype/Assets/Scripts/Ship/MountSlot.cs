using UnityEngine;

/// <summary>
/// Gemi üzerindeki bir komponent slotu.
/// </summary>
public class MountSlot : MonoBehaviour
{
    public enum SlotType { Universal, WeaponOnly, Fixed }

    public SlotType slotType;

    [field: SerializeField]
    public ShipComponentBase installedComponent { get; private set; }

    public bool IsOccupied => installedComponent != null;
    public bool IsFixed    => slotType == SlotType.Fixed;

    public bool Install(ShipComponentBase component)
    {
        if (IsFixed)    return false;
        if (IsOccupied) return false;

        component.transform.SetParent(transform, false);
        component.transform.localPosition = Vector3.zero;
        installedComponent = component;
        return true;
    }

    public ShipComponentBase Uninstall()
    {
        if (IsFixed || !IsOccupied) return null;

        var comp = installedComponent;
        installedComponent = null;
        comp.transform.SetParent(null);
        return comp;
    }
}

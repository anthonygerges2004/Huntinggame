using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Mouse events for one inventory slot. The inventory owns all the logic; this only forwards
/// hover, click, drag and drop with the slot's index.
/// </summary>
public class InventorySlotView : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    public int index;
    public Inventory owner;

    public void OnPointerEnter(PointerEventData e) { if (owner != null) owner.SlotHover(index, true); }
    public void OnPointerExit(PointerEventData e) { if (owner != null) owner.SlotHover(index, false); }

    public void OnPointerClick(PointerEventData e)
    {
        if (owner == null || e.dragging)
            return;
        if (e.button == PointerEventData.InputButton.Left)
            owner.SlotClicked(index);
        else if (e.button == PointerEventData.InputButton.Right)
            owner.SlotUse(index);
    }

    public void OnBeginDrag(PointerEventData e)
    {
        if (owner != null && e.button == PointerEventData.InputButton.Left)
            owner.SlotBeginDrag(index, e.position);
    }

    public void OnDrag(PointerEventData e)
    {
        if (owner != null)
            owner.SlotDragMove(e.position);
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (owner != null)
            owner.SlotEndDrag(e.position);
    }

    public void OnDrop(PointerEventData e)
    {
        if (owner != null)
            owner.SlotDrop(index);
    }
}

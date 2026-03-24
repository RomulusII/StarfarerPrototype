using UnityEngine;

/// <summary>
/// Ekranın ortasında sabit duran ana gemi.
/// Sprite Unity'nin built-in "Sprites/Default" kaynağından alınır.
/// </summary>
public class PlayerShip : MonoBehaviour
{
    void Awake()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        sr.sprite = Resources.GetBuiltinResource<Sprite>("Sprites/Default");
        sr.color = Color.white;
    }
}

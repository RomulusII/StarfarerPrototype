using UnityEngine;

public enum CrewMember
{
    Captain,   // Ana silah slotu
    Engineer,  // Jeneratör slotu
    Pilot,     // Hangar slotu
}

[System.Serializable]
public struct DialogueLine
{
    public CrewMember speaker;
    [TextArea(1, 3)]
    public string     text;
    [Tooltip("Bir sonraki satıra geçmeden önce bekleme (saniye).")]
    public float      displayDuration;
}

using UnityEngine;

public enum SpawnSide { Right, Top, Bottom, Left }

[System.Serializable]
public struct RoleSlot
{
    public EnemyRole role;
    public Vector2   offset;   // normalize (-1..1), SpawnSide'a göre dönüştürülür
}

/// <summary>
/// Bir formasyon şablonu: hangi rollerin nerede durduğunu ve
/// hangi giriş yönünden geldiğini tanımlar.
/// EnemyTypeData.role → RoleSlot.role eşleşmesiyle düşmanlar yerleştirilir.
/// </summary>
[CreateAssetMenu(fileName = "Formation_New", menuName = "Starfarer/Formation Template")]
public class FormationTemplate : ScriptableObject
{
    [Header("Kimlik")]
    public string formationName;

    [Header("Giriş Yönü")]
    public SpawnSide defaultSide = SpawnSide.Right;

    [Header("Rol Slotları (offset normalize -1..1)")]
    public RoleSlot[] slots;

    [Header("Tercih Edilen Roller (bu roller varsa bu şablon seçilir)")]
    public EnemyRole[] preferredRoles;

    // ── Built-in factory metodları ────────────────────────────────────────────

    public static FormationTemplate CreateArrow()
    {
        var f = CreateInstance<FormationTemplate>();
        f.formationName  = "Arrow";
        f.defaultSide    = SpawnSide.Right;
        f.preferredRoles = new[] { EnemyRole.Vanguard, EnemyRole.Flank };
        f.slots = new RoleSlot[]
        {
            new RoleSlot { role = EnemyRole.Vanguard, offset = new Vector2(0.6f,  0f)   },
            new RoleSlot { role = EnemyRole.Flank,    offset = new Vector2(0.2f,  0.4f) },
            new RoleSlot { role = EnemyRole.Flank,    offset = new Vector2(0.2f, -0.4f) },
            new RoleSlot { role = EnemyRole.Center,   offset = new Vector2(0f,    0f)   },
            new RoleSlot { role = EnemyRole.Rear,     offset = new Vector2(-0.4f, 0.3f) },
            new RoleSlot { role = EnemyRole.Rear,     offset = new Vector2(-0.4f,-0.3f) },
            new RoleSlot { role = EnemyRole.Vanguard, offset = new Vector2(0.4f,  0.6f) },
            new RoleSlot { role = EnemyRole.Vanguard, offset = new Vector2(0.4f, -0.6f) },
        };
        return f;
    }

    public static FormationTemplate CreateColumn()
    {
        var f = CreateInstance<FormationTemplate>();
        f.formationName  = "Column";
        f.defaultSide    = SpawnSide.Right;
        f.preferredRoles = new[] { EnemyRole.Rear, EnemyRole.Center };
        f.slots = new RoleSlot[]
        {
            new RoleSlot { role = EnemyRole.Vanguard, offset = new Vector2(0f,  0.8f)  },
            new RoleSlot { role = EnemyRole.Flank,    offset = new Vector2(0f,  0.4f)  },
            new RoleSlot { role = EnemyRole.Center,   offset = new Vector2(0f,  0f)    },
            new RoleSlot { role = EnemyRole.Rear,     offset = new Vector2(0f, -0.4f)  },
            new RoleSlot { role = EnemyRole.Rear,     offset = new Vector2(0f, -0.8f)  },
        };
        return f;
    }

    public static FormationTemplate CreateBroadFront()
    {
        var f = CreateInstance<FormationTemplate>();
        f.formationName  = "BroadFront";
        f.defaultSide    = SpawnSide.Right;
        f.preferredRoles = new[] { EnemyRole.Vanguard };
        f.slots = new RoleSlot[]
        {
            new RoleSlot { role = EnemyRole.Flank,    offset = new Vector2(0f,  0.9f)  },
            new RoleSlot { role = EnemyRole.Vanguard, offset = new Vector2(0f,  0.5f)  },
            new RoleSlot { role = EnemyRole.Center,   offset = new Vector2(0f,  0f)    },
            new RoleSlot { role = EnemyRole.Vanguard, offset = new Vector2(0f, -0.5f)  },
            new RoleSlot { role = EnemyRole.Flank,    offset = new Vector2(0f, -0.9f)  },
        };
        return f;
    }

    /// <summary>
    /// Bariyer önde, filo arkasında. Bariyerin bütün anlamı arkasındakilere
    /// siper olmasıdır — formasyonun en önünde durmazsa hiçbir şey ifade etmez.
    /// </summary>
    public static FormationTemplate CreateShieldWall()
    {
        var f = CreateInstance<FormationTemplate>();
        f.formationName  = "ShieldWall";
        f.defaultSide    = SpawnSide.Right;
        f.preferredRoles = new[] { EnemyRole.Barrier };
        f.slots = new RoleSlot[]
        {
            new RoleSlot { role = EnemyRole.Barrier,  offset = new Vector2( 0.7f,  0f)   },
            new RoleSlot { role = EnemyRole.Barrier,  offset = new Vector2( 0.6f,  0.5f) },
            new RoleSlot { role = EnemyRole.Barrier,  offset = new Vector2( 0.6f, -0.5f) },
            new RoleSlot { role = EnemyRole.Center,   offset = new Vector2( 0f,    0f)   },
            new RoleSlot { role = EnemyRole.Vanguard, offset = new Vector2( 0f,    0.5f) },
            new RoleSlot { role = EnemyRole.Vanguard, offset = new Vector2( 0f,   -0.5f) },
            new RoleSlot { role = EnemyRole.Rear,     offset = new Vector2(-0.5f,  0.3f) },
            new RoleSlot { role = EnemyRole.Rear,     offset = new Vector2(-0.5f, -0.3f) },
        };
        return f;
    }

    public static FormationTemplate CreateEscort()
    {
        var f = CreateInstance<FormationTemplate>();
        f.formationName  = "Escort";
        f.defaultSide    = SpawnSide.Right;
        f.preferredRoles = new[] { EnemyRole.Center, EnemyRole.Rear };
        f.slots = new RoleSlot[]
        {
            new RoleSlot { role = EnemyRole.Center,   offset = new Vector2(0f,    0f)   },
            new RoleSlot { role = EnemyRole.Vanguard, offset = new Vector2(0.5f,  0f)   },
            new RoleSlot { role = EnemyRole.Flank,    offset = new Vector2(0f,    0.5f) },
            new RoleSlot { role = EnemyRole.Flank,    offset = new Vector2(0f,   -0.5f) },
            new RoleSlot { role = EnemyRole.Rear,     offset = new Vector2(-0.5f, 0f)   },
        };
        return f;
    }

    public static FormationTemplate CreateScattered()
    {
        var f = CreateInstance<FormationTemplate>();
        f.formationName  = "Scattered";
        f.defaultSide    = SpawnSide.Right;
        f.preferredRoles = new[] { EnemyRole.Vanguard };
        // Dağınık: offsetler runtime'da hafif rastgele bozulur (ChapterManager yapar)
        f.slots = new RoleSlot[]
        {
            new RoleSlot { role = EnemyRole.Vanguard, offset = new Vector2( 0.1f,  0.7f) },
            new RoleSlot { role = EnemyRole.Vanguard, offset = new Vector2( 0.3f,  0.3f) },
            new RoleSlot { role = EnemyRole.Vanguard, offset = new Vector2( 0.0f,  0.0f) },
            new RoleSlot { role = EnemyRole.Vanguard, offset = new Vector2( 0.3f, -0.3f) },
            new RoleSlot { role = EnemyRole.Vanguard, offset = new Vector2( 0.1f, -0.7f) },
            new RoleSlot { role = EnemyRole.Flank,    offset = new Vector2(-0.2f,  0.5f) },
            new RoleSlot { role = EnemyRole.Flank,    offset = new Vector2(-0.2f, -0.5f) },
        };
        return f;
    }
}

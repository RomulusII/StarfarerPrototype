using System;
using System.Collections.Generic;
using UnityEngine;

// ═════════════════════════════════════════════════════════════════════════════
// Oyun dünyasının TAM kaydı — bkz. WorldSave.
//
// Her sahne nesnesi kendi kaydını yazar ve okur (CaptureState / RestoreState).
// Buradaki sınıflar yalnızca veridir: JsonUtility polimorfizm desteklemediği
// için her nesne tipinin ayrı bir listesi var.
//
// ZAMAN KURALI: mutlak Time.time değeri ASLA yazılmaz. Uygulama yeniden
// açıldığında Time.time sıfırdan başlar; "şu saniyede ateş et" diye yazılmış
// bir kayıt, açılışta ya sonsuza kadar bekler ya da hemen ateş ederdi. Yazılan
// her zaman ölçüsü ya KALAN süre ya da GEÇEN süredir.
//
// KİMLİK KURALI: nesneler arası referans (hedef, formasyon, toplanan enkaz)
// kayıt anında verilen tamsayı kimlikle yazılır. 0 = yok, -1 = ana gemi.
// Komponent referansı SLOT numarasıyla yazılır (+1; 0 = yok).
// ═════════════════════════════════════════════════════════════════════════════

[Serializable]
public class MovementState
{
    public Vector2 velocity;
    public float   facing;
    public float   wanderPhase;
    public float   wanderCurrent;
}

[Serializable]
public class BrainState
{
    public int   state;
    public float stateTimer;
    public float approachAngle;
    public int   orbitDir;
    public bool  strafeInbound;
    public float escapeOffset;
    public int   escapeSide;
    public int   target;
}

[Serializable]
public class EnemyState
{
    public int    id;
    public string dataJson;     // ölçeklenmiş tip verisinin kendisi — bkz. WorldSave.RebuildEnemyData
    public string typeName;     // JsonUtility ScriptableObject adını YAZMAZ; tip kimliği ad
    public Vector2 pos;
    public Vector2 velocity;

    public float hp, maxHp;
    public float shieldHP, maxShieldHP, shieldRechargeTimer;

    public int     screenPhase;
    public Vector2 guardDir;
    public float   guardScanTimer, screenLateral;
    public float   screenSwayPhase;   // Time.time katkısı ÇIKARILMIŞ faz

    public float fireTimer, targetScanTimer, fireScanTimer;
    public int   fireTarget;
    public int   formation;
    public float barrelRotation;

    public int     approachPhase;
    public float   approachFireTimer;
    public int     approachShotsLeft;
    public Vector2 approachHoverPos, approachEscapeDir;

    public float bombRunFireTimer;
    public float arDisengageTimer, arEscapeAngle;
    public int   arPhase, arTargetSlot;

    public int   escapeSide;
    public float age, sinceFirstHit, damageTaken;
    public float phaseTimer, phaseCooldown, auraTimer;

    public MovementState movement;
    public bool          hasBrain;
    public BrainState    brain;
}

[Serializable]
public class FormationState
{
    public int           id;
    public Vector2       anchor, target;
    public float         speed, timer;
    public List<int>     members = new();
    public List<Vector2> offsets = new();
}

[Serializable]
public class HardpointState
{
    public float hp, maxHp, fireDamage, fireTimer;
    public bool  dead;
}

[Serializable]
public class BossState
{
    public int    id;
    public string bossName;
    public Vector2 pos;
    public float  maxHP, hullHP, shieldHP, shieldRechargeTimer;
    public int    phaseIndex;
    public float  droneSpawnTimer, targetY, yChangeTimer;
    public bool   dead;
    public int    deathPiecesLeft;
    public float  deathTimer;
    public MovementState        movement;
    public List<HardpointState> hardpoints = new();
}

[Serializable]
public class AsteroidState
{
    public int     id;
    public int     size;
    public Vector2 pos;
    public float   rotation, hp, maxHp, spin;
    public Vector2 drift, separation;
}

[Serializable]
public class DebrisState
{
    public int     id;
    public Vector2 pos;
    public int     type, origin, variant;
    public float   amount, life;
    public Vector2 scatter;
}

[Serializable]
public class BombState
{
    public int     id;
    public Vector2 pos, dir;
    public float   speed, damage, hp, life;
}

[Serializable]
public class EnemyBulletState
{
    public Vector2 pos, dir;
    public float   speed, damage, life, scale;
    public bool    bypass;
    public int     targetSlot;
}

[Serializable]
public class PlayerBulletState
{
    public Vector2 pos;
    public float   rotation, scale, speed, damage, zoom, life;
    public int     weaponType, boost;
}

[Serializable]
public class TurretBulletState
{
    public Vector2 pos, dir;
    public float   speed, damage, turnRate, hp, zoom, life;
    public int     weaponType, visual;
    public bool    guided;
    public int     target;
}

[Serializable]
public class LaserBeamState
{
    public int     ownerSlot;    // turret: slot + 1
    public int     ownerEnemy;   // düşman: kimlik
    public bool    onBarrel;
    public Vector3 localPos;
    public float   localRot;
    public float   damage, burnDuration, remaining, maxRange, energyPerSecond;
    public int     weaponType;
    public bool    hitsPlayer;
}

[Serializable]
public class PlasmaBeamState
{
    public Vector3 origin, firingDir;
    public float   rotAngle, head, tail, emitTimer, initialEnergy, totalEnergy;
    public float   width, maxLength, speed, dps, emitDuration;
    public int     phase, weaponType;
    public float   sparkTimer;
}

[Serializable]
public class CollectorState
{
    public int     id;
    public Vector2 pos;
    public int     hangarSlot, phase, target;
    public float   hp, maxHp, salvageRate, hoverPhase, cargoTotal;
    public float[] cargo;
    public MovementState movement;
}

[Serializable]
public class FighterState
{
    public int     id;
    public Vector2 pos;
    public int     hangarSlot, target;
    public float   hp, maxHp, fireRate, damage, fireTimer, scanTimer;
    public Vector3 patrolPoint;
    public MovementState movement;
    public BrainState    brain;
}

/// <summary>
/// Kurulu bir komponentin ÇALIŞMA ANI durumu. Kurulumun kendisi (tip, statlar)
/// <see cref="SaveSystem.SaveData"/> içinde; burası yalnızca o komponentin şu
/// anki hâli. Tek sınıf, alt tipler kendi alanlarını doldurur.
/// </summary>
[Serializable]
public class ComponentRuntimeState
{
    public int   slot;
    public float hp;
    public bool  deactivated;

    // Kalkan jeneratörü
    public float shield;
    public bool  depleted, reactivating;
    public float reactivateTimer;

    // Turret
    public float fireTimer, reloadTimer, rotation, retargetTimer;
    public int   magazine, lockedTarget;
    public bool  reloading;

    // Hangar
    public float collectorTimer, fighterTimer;
}

[Serializable]
public class ChapterRunState
{
    public int   waveIndex, phase, pending;
    public float pendingTimer, levelElapsed;
}

[Serializable]
public class AsteroidFieldState
{
    public bool  present;
    public int   targetCount;
    public float interval, timer;
}

[Serializable]
public class WorldState
{
    public const int CurrentVersion = 1;

    public int    version = CurrentVersion;
    public string mode;      // "kampanya" | "serbest"
    public int    level;

    public SaveSystem.SaveData ship;

    public float  hull;
    public float  energy;
    public int    boost;
    public float  orphanShield, orphanMaxShield;
    public float  weaponCooldown;
    public string rng;

    public EnemySpawner.FreeRunState free;
    public ChapterRunState    chapter;
    public AsteroidFieldState asteroidField;

    public List<ComponentRuntimeState> components   = new();
    public List<FormationState>        formations   = new();
    public List<EnemyState>            enemies      = new();
    public List<BossState>             bosses       = new();
    public List<AsteroidState>         asteroids    = new();
    public List<DebrisState>           debris       = new();
    public List<BombState>             bombs        = new();
    public List<EnemyBulletState>      enemyBullets = new();
    public List<PlayerBulletState>     bullets      = new();
    public List<TurretBulletState>     turretBullets = new();
    public List<LaserBeamState>        lasers       = new();
    public List<PlasmaBeamState>       plasma       = new();
    public List<CollectorState>        collectors   = new();
    public List<FighterState>          fighters     = new();
}

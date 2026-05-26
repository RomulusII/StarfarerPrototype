public enum EnemyMovementKind
{
    Charge,      // ShipBrain Orbit — hedefe doğru yörünge + ateş
    HoverFire,   // ShipBrain HoverFire — uzakta durur, ateş eder
    Approach,    // Yaklaş → bekle → burst → geri çekil (eski Bomber)
    Strafe,      // Hedefe paralel kayma
    Stationary,  // Hareket etmez
    BombRun,     // Düz çizgide geçer, bomba atar — Point Defence'in öncelikli hedefi
    AttackRun,   // Yavaş dön, baktığı yöne it, yakında ateş et — geniş kavisler (Bomber)
}

public enum EnemyMovementKind
{
    Charge,      // ShipBrain Orbit — hedefe doğru yörünge + ateş
    HoverFire,   // ShipBrain HoverFire — uzakta durur, ateş eder
    Approach,    // Yaklaş → bekle → burst → geri çekil (Bomber)
    Strafe,      // Hedefe paralel kayma
    Stationary,  // Hareket etmez
}

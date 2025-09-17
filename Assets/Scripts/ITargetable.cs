using UnityEngine;

public interface ITargetable
{
    Transform GetTransform();   // позиция объекта
    bool IsAlive();             // для пауков — жив ли, для объектов — сломан ли
    void SetTargeted(bool active);
}

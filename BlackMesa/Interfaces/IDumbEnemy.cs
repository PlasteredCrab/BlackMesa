namespace BlackMesa.Interfaces;

public interface IDumbEnemy : IHittable
{
    void Stun(float time);
}
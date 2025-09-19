public interface IPowerConsumer
{
    // Сколько потребляет устройство (ед/сек), когда активно.
    float GetConsumptionPerSecond();

    // Активно ли устройство прямо сейчас (работает/жрет питание).
    bool IsConsuming();

    // Сообщение от сети: питание появилось/пропало.
    void OnPowerChanged(bool hasPower);
}

using System.Text.Json.Serialization;

namespace BrightnessSensor.ConsoleApp.Configuration;

// Processing parameters: ADC range, inversion, smoothing, and hysteresis.
internal sealed class ProcessingSettings
{
    /// <summary>
    /// Нижняя граница полезного диапазона ADC.
    /// Значения ниже этого порога обрезаются, что задает "нулевую" точку нормализации
    /// и влияет на чувствительность в темной зоне.
    /// </summary>
    [JsonPropertyName("adcMin")]
    public int AdcMin { get; init; }

    /// <summary>
    /// Верхняя граница полезного диапазона ADC.
    /// Значения выше этого порога обрезаются; вместе с adcMin формирует шкалу нормализации
    /// и определяет, как быстро яркость выходит на максимум.
    /// </summary>
    [JsonPropertyName("adcMax")]
    public int AdcMax { get; init; }

    /// <summary>
    /// Инвертирует нормализованную шкалу (1 - x).
    /// Нужен для датчиков/схем, где при большем свете ADC уменьшается.
    /// Меняет направление зависимости "освещенность -> яркость".
    /// </summary>
    [JsonPropertyName("invert")]
    public bool Invert { get; init; }

    /// <summary>
    /// Коэффициент экспоненциального сглаживания EMA в диапазоне (0, 1].
    /// Чем выше значение, тем быстрее реакция на изменения света;
    /// чем ниже, тем плавнее и инертнее итоговая яркость.
    /// </summary>
    [JsonPropertyName("emaAlpha")]
    public double EmaAlpha { get; init; }

    /// <summary>
    /// Минимальное изменение яркости в процентах для применения нового значения.
    /// Пока разница меньше порога, изменение игнорируется — это уменьшает дрожание
    /// яркости при небольшом шуме входного сигнала.
    /// </summary>
    [JsonPropertyName("hysteresisPercent")]
    public int HysteresisPercent { get; init; }

    /// <summary>
    /// Дополнительная нелинейная коррекция после EMA (степенная функция x^gamma).
    /// null отключает коррекцию (линейная кривая).
    /// Обычно 1.8..2.2 делает рост яркости в темной зоне более мягким и
    /// уменьшает ощущение "залипания" в светлой зоне.
    /// </summary>
    [JsonPropertyName("gamma")]
    public double? Gamma { get; init; }
}

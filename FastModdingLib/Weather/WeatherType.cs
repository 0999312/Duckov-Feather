namespace FeatherMod
{
    /// <summary>
    /// FML 自有天气类型枚举。封装游戏原生 Weather 枚举，隐藏 Snow=22 等实现细节。
    /// </summary>
    public enum WeatherType
    {
        Sunny,
        Cloudy,
        Rainy,
        Snow,
        Stormy,
        SevereStormy
    }
}

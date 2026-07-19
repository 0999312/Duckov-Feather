namespace FeatherMod.Events.GameEvents
{
    /// <summary>
    /// 主场景加载完成事件。桥接自游戏原生 <c>SceneLoader.onFinishedLoadingScene</c>
    /// （Curtain 卸载、目标主场景激活后触发），覆盖新游戏与读档进入主场景两条路径。
    /// 仅观察用途，不支持取消。
    /// </summary>
    public sealed class MainSceneLoadedEvent : Event
    {
    }
}
